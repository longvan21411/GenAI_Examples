using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Threading;
using System.Text.Json;
using System.Net;

namespace ChatApp_RAG.Services;

public sealed class QdrantChatStore
{
    private readonly HttpClient _http;
    private readonly EmbeddingService _embeddingService;
    private readonly object? _embeddingsObj;
    private readonly ILogger<QdrantChatStore> _logger;
    private readonly string _collectionName = "RAG_01"; //"chat-messages-RAG-01";
    private readonly int _vectorSize = IngestedChunk.VectorDimensions;
    private bool _useNamedVectors = true;
    private string _vectorName = "default";

    public QdrantChatStore(HttpClient http, EmbeddingService embeddingService, IServiceProvider services, ILogger<QdrantChatStore> logger)
    {
        _http = http;
        _logger = logger;
        _embeddingService = embeddingService ?? throw new InvalidOperationException("EmbeddingService not registered");
        _embeddingsObj = services.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>));
    }

    public async Task EnsureCollectionExistsAsync()
    {
        try
        {
            var request = new
            {
                vectors = new
                {
                    @default = new
                    {
                        size = _vectorSize,
                        distance = IngestedChunk.VectorDistanceFunction
                    }
                }
            };

            Log.Debug("Ensuring Qdrant collection exists. Collection={Collection} Vectors.Size={Size} Vectors.Distance={Distance}", _collectionName, _vectorSize, IngestedChunk.VectorDistanceFunction);
            var response = await _http.PutAsJsonAsync($"collections/{_collectionName}", request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("EnsureCollectionExistsAsync(): Failed to ensure Qdrant collection exists. Collection={Collection} Status={Status} Response={Response}", _collectionName, response.StatusCode, body);
            }
            else
            {
                Log.Information("EnsureCollectionExistsAsync(): Ensured Qdrant collection exists. Collection={Collection}", _collectionName);
            }

            var info = await GetCollectionInfoRawAsync();
            if (!string.IsNullOrWhiteSpace(info))
            {
                try
                {
                    using var doc = JsonDocument.Parse(info);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("result", out var resultEl))
                        root = resultEl;

                    if (root.TryGetProperty("vectors", out var vectorsEl) && vectorsEl.ValueKind == JsonValueKind.Object)
                    {
                        // if named vectors, pick first name
                        if (vectorsEl.TryGetProperty("size", out var _))
                        {
                            _useNamedVectors = false;
                            _vectorName = string.Empty;
                        }
                        else
                        {
                            var first = vectorsEl.EnumerateObject().FirstOrDefault();
                            if (!string.IsNullOrEmpty(first.Name))
                            {
                                _useNamedVectors = true;
                                _vectorName = first.Name;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "EnsureCollectionExistsAsync(): Could not parse collection info");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EnsureCollectionExistsAsync(): Error while ensuring Qdrant collection exists. Collection={Collection}", _collectionName);
        }
    }

    public async Task UpsertMessageAsync(Guid id, string text, string role)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            float[]? embeddingVector = null;
            try
            {
                embeddingVector = await _embeddingService.GetEmbeddingAsync(text);
                Log.Debug("UpsertMessageAsync(): EmbeddingService returned length={Len}", embeddingVector?.Length ?? 0);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "UpsertMessageAsync(): EmbeddingService failed, will attempt fallback");
            }

            if (embeddingVector is null && _embeddingsObj is not null)
            {
                var embedResponse = await InvokeEmbeddingAsync(_embeddingsObj, new[] { text });
                if (embedResponse is not null)
                    embeddingVector = ExtractEmbeddingVector(embedResponse);
            }

            if (embeddingVector is null)
            {
                Log.Warning("UpsertMessageAsync(): Unable to obtain embedding vector for message. Id={Id} Role={Role}", id, role);
                return;
            }

            if (embeddingVector.Length != _vectorSize)
            {
                Log.Warning("UpsertMessageAsync(): Embedding vector length {Len} does not match expected {Expected}. Upsert may fail.", embeddingVector.Length, _vectorSize);
            }

            object point;
            if (_useNamedVectors)
            {
                var vectorsPayload = new Dictionary<string, float[]>
                {
                    [_vectorName] = embeddingVector
                };

                point = new Dictionary<string, object>
                {
                    ["id"] = id.ToString(),
                    ["vectors"] = vectorsPayload,
                    ["payload"] = new { text = text, role = role }
                };
            }
            else
            {
                point = new
                {
                    id = id.ToString(),
                    vector = embeddingVector,
                    payload = new { text = text, role = role }
                };
            }

            var upsert = new { points = new[] { point } };

            var opts = new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false };
            var payloadJson = JsonSerializer.Serialize(upsert, opts);
            Log.Debug("UpsertMessageAsync(): Upsert payload JSON: {Json}", payloadJson);

            // Log collection info to help diagnose NotFound
            try
            {
                var colInfo = await GetCollectionInfoRawAsync();
                Log.Debug("UpsertMessageAsync(): Collection info before upsert: {Info}", colInfo);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "UpsertMessageAsync(): Failed to fetch collection info before upsert");
            }

            // Build absolute URIs for diagnostics
            var uriPoints = new Uri(_http.BaseAddress, $"collections/{_collectionName}/points");
            var uriPointsUpsert = new Uri(_http.BaseAddress, $"collections/{_collectionName}/points/upsert");
            Log.Information("UpsertMessageAsync(): Will POST to {UriPoints} and fallback to {UriUpsert}", uriPoints, uriPointsUpsert);

            using var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

            // Try preferred endpoint first
            var res = await _http.PostAsync(uriPoints, content);
            var body = await res.Content.ReadAsStringAsync();
            Log.Information("UpsertMessageAsync(): /points response status={Status} body={Body}", res.StatusCode, body);
            if (res.IsSuccessStatusCode)
            {
                Log.Information("UpsertMessageAsync(): Upserted point into Qdrant via /points. Collection={Collection} Id={Id} Role={Role}", _collectionName, id, role);
            }
            else
            {
                Log.Warning("UpsertMessageAsync(): /points failed. Status={Status} Response={Response}", res.StatusCode, body);

                // If endpoint not found, try the alternate '/points/upsert'
                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Debug("UpsertMessageAsync(): /points returned 404, trying /points/upsert");
                    using var content2 = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
                    var res2 = await _http.PostAsync(uriPointsUpsert, content2);
                    var body2 = await res2.Content.ReadAsStringAsync();
                    Log.Debug("UpsertMessageAsync(): /points/upsert response status={Status} body={Body}", res2.StatusCode, body2);
                    if (res2.IsSuccessStatusCode)
                    {
                        Log.Information("UpsertMessageAsync(): Upserted point into Qdrant via /points/upsert. Collection={Collection} Id={Id} Role={Role}", _collectionName, id, role);
                        return;
                    }

                    Log.Warning("UpsertMessageAsync(): /points/upsert failed. Status={Status} Response={Response}", res2.StatusCode, body2);

                    // If server complains about missing ids, build alternate payload shape
                    if (!string.IsNullOrEmpty(body2) && body2.Contains("missing field `ids`", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Debug("UpsertMessageAsync(): Server expects top-level ids/vectors/payloads. Building alternate payload.");
                        object altPayload;
                        if (_useNamedVectors)
                        {
                            altPayload = new
                            {
                                ids = new[] { id.ToString() },
                                vectors = new Dictionary<string, float[][]> { [_vectorName] = new[] { embeddingVector } },
                                payloads = new[] { new { text = text, role = role } }
                            };
                        }
                        else
                        {
                            altPayload = new
                            {
                                ids = new[] { id.ToString() },
                                vectors = new[] { embeddingVector },
                                payloads = new[] { new { text = text, role = role } }
                            };
                        }

                        var payloadJson3 = JsonSerializer.Serialize(altPayload, opts);
                        using var content3 = new StringContent(payloadJson3, System.Text.Encoding.UTF8, "application/json");
                        var res3 = await _http.PostAsync(uriPointsUpsert, content3);
                        var body3 = await res3.Content.ReadAsStringAsync();
                        Log.Debug("UpsertMessageAsync(): alternate payload response status={Status} body={Body}", res3.StatusCode, body3);
                        if (res3.IsSuccessStatusCode)
                        {
                            Log.Information("UpsertMessageAsync(): Upserted point with alternate payload. Collection={Collection} Id={Id}", _collectionName, id);
                        }
                        else
                        {
                            Log.Warning("UpsertMessageAsync(): Alternate payload failed. Status={Status} Response={Response}", res3.StatusCode, body3);
                        }
                    }

                    return;
                }

                // If /points returned other error and mentions missing ids, attempt alternate payload on /points/upsert
                if (!string.IsNullOrEmpty(body) && body.Contains("missing field `ids`", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("UpsertMessageAsync(): /points returned missing ids, trying alternate payload on /points/upsert");
                    object altPayload;
                    if (_useNamedVectors)
                    {
                        altPayload = new
                        {
                            ids = new[] { id.ToString() },
                            vectors = new Dictionary<string, float[][]> { [_vectorName] = new[] { embeddingVector } },
                            payloads = new[] { new { text = text, role = role } }
                        };
                    }
                    else
                    {
                        altPayload = new
                        {
                            ids = new[] { id.ToString() },
                            vectors = new[] { embeddingVector },
                            payloads = new[] { new { text = text, role = role } }
                        };
                    }

                    var payloadJson3 = JsonSerializer.Serialize(altPayload, opts);
                    using var content3 = new StringContent(payloadJson3, System.Text.Encoding.UTF8, "application/json");
                    var res3 = await _http.PostAsync(uriPointsUpsert, content3);
                    var body3 = await res3.Content.ReadAsStringAsync();
                    Log.Debug("UpsertMessageAsync(): alternate payload response status={Status} body={Body}", res3.StatusCode, body3);
                    if (res3.IsSuccessStatusCode)
                    {
                        Log.Information("UpsertMessageAsync(): Upserted point with alternate payload. Collection={Collection} Id={Id}", _collectionName, id);
                    }
                    else
                    {
                        Log.Warning("UpsertMessageAsync(): Alternate payload failed. Status={Status} Response={Response}", res3.StatusCode, body3);
                    }
                }
                else
                {
                    Log.Warning("UpsertMessageAsync(): Failed to upsert point into Qdrant. Collection={Collection} Id={Id} Role={Role} Status={Status} Response={Response}", _collectionName, id, role, res.StatusCode, body);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UpsertMessageAsync(): Error while upserting message into Qdrant. Collection={Collection}", _collectionName);
        }
    }

    public async Task UpsertMessagesAsync(IEnumerable<(Guid Id, string Text, string Role)> messages)
    {
        var points = new List<object>();
        foreach (var m in messages)
        {
            if (string.IsNullOrWhiteSpace(m.Text))
                continue;

            float[]? embeddingVector = null;
            try
            {
                embeddingVector = await _embeddingService.GetEmbeddingAsync(m.Text);
            }
            catch
            {
            }

            if (embeddingVector is null && _embeddingsObj is not null)
            {
                var embedResponse = await InvokeEmbeddingAsync(_embeddingsObj, new[] { m.Text });
                if (embedResponse is not null)
                    embeddingVector = ExtractEmbeddingVector(embedResponse);
            }

            if (embeddingVector is null)
                continue;

            if (_useNamedVectors)
            {
                var vectorsPayload = new Dictionary<string, float[]>
                {
                    [_vectorName] = embeddingVector
                };

                points.Add(new Dictionary<string, object>
                {
                    ["id"] = m.Id.ToString(),
                    ["vectors"] = vectorsPayload,
                    ["payload"] = new { text = m.Text, role = m.Role }
                });
            }
            else
            {
                points.Add(new
                {
                    id = m.Id.ToString(),
                    vector = embeddingVector,
                    payload = new { text = m.Text, role = m.Role }
                });
            }
        }

        if (!points.Any())
            return;

        var upsert = new { points };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false };
        var payloadJson = JsonSerializer.Serialize(upsert, opts);
        Log.Information("UpsertMessagesAsync(): Bulk upsert payload JSON: {Json}", payloadJson);
        Log.Information("UpsertMessagesAsync(): Base URL: {Url}", $"{_http.BaseAddress}collections/{_collectionName}/points/upsert?wait=true");
        using var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
        var res = await _http.PostAsync($"collections/{_collectionName}/points/upsert?wait=true", content);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            Log.Warning("UpsertMessagesAsync(): Failed to bulk upsert points into Qdrant. Collection={Collection} Status={Status} Response={Response}", _collectionName, res.StatusCode, body);
        }
        else
        {
            Log.Information("UpsertMessagesAsync(): Bulk upserted {Count} messages into Qdrant collection {Collection}", points.Count, _collectionName);
        }
    }

    private async Task<object?> InvokeEmbeddingAsync(object embeddingsObj, string[] inputs)
    {
        if (embeddingsObj is null)
            return null;

        var type = embeddingsObj.GetType();
        var methods = new List<MethodInfo>();
        methods.AddRange(type.GetMethods(BindingFlags.Instance | BindingFlags.Public));

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (!t.IsSealed || !t.IsAbstract)
                    continue;
                methods.AddRange(t.GetMethods(BindingFlags.Static | BindingFlags.Public));
            }
        }

        foreach (var m in methods)
        {
            if (!m.Name.Contains("Generate", StringComparison.OrdinalIgnoreCase) && !m.Name.Contains("Embedding", StringComparison.OrdinalIgnoreCase))
                continue;

            var parameters = m.GetParameters();
            int inputParamIndex = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                if (typeof(IEnumerable<string>).IsAssignableFrom(pType) || pType == typeof(string[]))
                {
                    inputParamIndex = i;
                    break;
                }
            }

            if (inputParamIndex == -1)
                continue;

            object? invokeTarget = null;
            object?[] args = new object?[parameters.Length];

            if (m.IsStatic)
            {
                if (parameters.Length == 0)
                    continue;
                var firstParam = parameters[0];
                if (!firstParam.ParameterType.IsAssignableFrom(type) && !(firstParam.ParameterType.IsInterface && type.GetInterfaces().Any(i => i == firstParam.ParameterType)))
                    continue;
                args[0] = embeddingsObj;
            }
            else
            {
                invokeTarget = embeddingsObj;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == inputParamIndex)
                {
                    args[i] = inputs;
                    continue;
                }

                var pType = parameters[i].ParameterType;
                if (pType == typeof(CancellationToken))
                    args[i] = CancellationToken.None;
                else if (pType.IsValueType)
                    args[i] = Activator.CreateInstance(pType);
                else
                    args[i] = null;
            }

            try
            {
                var ret = m.Invoke(invokeTarget, args);
                if (ret == null)
                    continue;

                var retType = ret.GetType();
                if (typeof(Task).IsAssignableFrom(retType))
                {
                    var task = (Task)ret;
                    await task.ConfigureAwait(false);
                    var resultProp = retType.GetProperty("Result") ?? retType.GetProperty("Value");
                    if (resultProp != null)
                        return resultProp.GetValue(ret);
                    return null;
                }

                if (retType.Name.Contains("ValueTask"))
                {
                    var asTask = retType.GetMethod("AsTask", BindingFlags.Instance | BindingFlags.Public);
                    if (asTask != null)
                    {
                        var asTaskObj = asTask.Invoke(ret, Array.Empty<object>());
                        if (asTaskObj is Task t)
                        {
                            await t.ConfigureAwait(false);
                            var resultProp = t.GetType().GetProperty("Result");
                            if (resultProp != null)
                                return resultProp.GetValue(t);
                        }
                    }
                    return ret;
                }

                return ret;
            }
            catch
            {
                continue;
            }
        }

        _logger.LogWarning("InvokeEmbeddingAsync(): No suitable embedding generation method found on provider type {Type}", type.FullName);
        return null;
    }

    private float[]? ExtractEmbeddingVector(object? embedResponse)
    {
        if (embedResponse is null)
            return null;

        try
        {
            if (embedResponse is IEnumerable<float> floats)
                return floats.ToArray();
            if (embedResponse is float[] arr)
                return arr;

            var type = embedResponse.GetType();

            var embProp = type.GetProperty("Embedding") ?? type.GetProperty("embedding");
            if (embProp != null)
            {
                var val = embProp.GetValue(embedResponse);
                if (val is IEnumerable<float> ev)
                    return ev.ToArray();
            }

            foreach (var name in new[] { "Value", "value", "Data", "data" })
            {
                var prop = type.GetProperty(name);
                if (prop == null)
                    continue;
                var val = prop.GetValue(embedResponse);
                if (val is IEnumerable<object> coll)
                {
                    var first = coll.Cast<object>().FirstOrDefault();
                    if (first == null)
                        continue;
                    var firstType = first.GetType();
                    var firstEmbProp = firstType.GetProperty("Embedding") ?? firstType.GetProperty("embedding");
                    if (firstEmbProp != null)
                    {
                        var fpVal = firstEmbProp.GetValue(first);
                        if (fpVal is IEnumerable<float> fl)
                            return fl.ToArray();
                    }

                    var toFloats = firstType.GetMethod("ToFloats", BindingFlags.Instance | BindingFlags.Public);
                    if (toFloats != null)
                    {
                        var tf = toFloats.Invoke(first, null);
                        if (tf is IEnumerable<float> fseq)
                            return fseq.ToArray();
                        var tfType = tf?.GetType();
                        if (tfType != null)
                        {
                            var toArray = tfType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public);
                            if (toArray != null)
                            {
                                var a = toArray.Invoke(tf, null);
                                if (a is float[] fa)
                                    return fa;
                            }
                        }
                    }
                }
            }

            var toFloatsRoot = type.GetMethod("ToFloats", BindingFlags.Instance | BindingFlags.Public);
            if (toFloatsRoot != null)
            {
                var tf = toFloatsRoot.Invoke(embedResponse, null);
                if (tf is IEnumerable<float> fseq)
                    return fseq.ToArray();
                var tfType = tf?.GetType();
                if (tfType != null)
                {
                    var toArray = tfType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public);
                    if (toArray != null)
                    {
                        var a = toArray.Invoke(tf, null);
                        if (a is float[] fa)
                            return fa;
                    }
                }
            }

            try
            {
                var serialized = JsonSerializer.Serialize(embedResponse);
                using var doc = JsonDocument.Parse(serialized ?? "{}");
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array && dataEl.GetArrayLength() > 0)
                    {
                        var first = dataEl[0];
                        if (first.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<float>();
                            foreach (var item in embEl.EnumerateArray())
                                if (item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var f))
                                    list.Add(f);
                            if (list.Count > 0)
                                return list.ToArray();
                        }
                    }

                    if (root.TryGetProperty("value", out var valueEl) && valueEl.ValueKind == JsonValueKind.Array && valueEl.GetArrayLength() > 0)
                    {
                        var first = valueEl[0];
                        if (first.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<float>();
                            foreach (var item in embEl.EnumerateArray())
                                if (item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var f))
                                    list.Add(f);
                            if (list.Count > 0)
                                return list.ToArray();
                        }
                    }
                }
            }
            catch { }

            if (embedResponse is string s)
            {
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                        {
                            var first = dataEl[0];
                            if (first.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
                            {
                                var list = new List<float>();
                                foreach (var item in embEl.EnumerateArray())
                                    list.Add(item.GetSingle());
                                return list.ToArray();
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ExtractEmbeddingVector(): Error extracting embedding vector from response");
        }

        return null;
    }

    public async Task<string> GetCollectionInfoRawAsync()
    {
        try
        {
            var res = await _http.GetAsync($"collections/{_collectionName}");
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                Log.Warning("GetCollectionInfoRawAsync(): Failed to get collection info from Qdrant. Collection={Collection} Status={Status} Response={Response}", _collectionName, res.StatusCode, body);
            }
            else
            {
                Log.Debug("GetCollectionInfoRawAsync(): Collection info: {Info}", body);
            }
            return body ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetCollectionInfoRawAsync(): Error fetching collection info for {Collection}", _collectionName);
            return string.Empty;
        }
    }

    public async Task<bool> CollectionHasPointsAsync(int sampleLimit = 5)
    {
        try
        {
            var payload = new { limit = sampleLimit };
            var res = await _http.PostAsJsonAsync($"collections/{_collectionName}/points/scroll", payload);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Log.Warning("CollectionHasPointsAsync(): Scroll request failed. Status={Status} Response={Response}", res.StatusCode, body);
                return false;
            }

            using var doc = JsonDocument.Parse(body ?? "{}");
            if (doc.RootElement.TryGetProperty("points", out var pointsElement) && pointsElement.ValueKind == JsonValueKind.Array)
            {
                var count = pointsElement.GetArrayLength();
                Log.Information("CollectionHasPointsAsync(): Collection '{Collection}' contains {CountSample} points returned by scroll (requested {Requested}).", _collectionName, count, sampleLimit);
                return count > 0;
            }

            Log.Information("CollectionHasPointsAsync(): Scroll returned no 'points' array. Raw response: {Raw}", body);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CollectionHasPointsAsync(): Error while checking points in collection {Collection}", _collectionName);
            return false;
        }
    }
}
