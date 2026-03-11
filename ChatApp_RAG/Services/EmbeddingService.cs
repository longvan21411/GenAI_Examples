using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using Serilog;

namespace ChatApp_RAG.Services;

public sealed class EmbeddingService
{
    private readonly object _embeddingClient;

    public EmbeddingService(object embeddingClient)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
    }

    // Returns the first embedding vector for the provided input
    public async Task<float[]?> GetEmbeddingAsync(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        try
        {
            // First, attempt the exact pattern from EmbeddingGenaration: call GenerateEmbeddingsAsync and use Value[0].ToFloats()
            try
            {
                dynamic gen = _embeddingClient;
                var task = gen.GenerateEmbeddingsAsync(new[] { input });
                await task.ConfigureAwait(false);
                var resp = task.GetAwaiter().GetResult();
                if (resp != null)
                {
                    // Try resp.Value or resp.value
                    object? valueObj = null;
                    try { valueObj = resp.Value; } catch { }
                    if (valueObj == null)
                    {
                        var rv = resp.GetType().GetProperty("Value")?.GetValue(resp) ?? resp.GetType().GetProperty("value")?.GetValue(resp);
                        valueObj = rv;
                    }

                    if (valueObj is System.Collections.IEnumerable enumVals)
                    {
                        var first = enumVals.Cast<object?>().FirstOrDefault();
                        if (first is not null)
                        {
                            // Prefer ToFloats() method on the item
                            var toFloats = first.GetType().GetMethod("ToFloats", BindingFlags.Instance | BindingFlags.Public);
                            if (toFloats != null)
                            {
                                var tf = toFloats.Invoke(first, null);
                                if (tf is System.ReadOnlyMemory<float> rom)
                                    return rom.ToArray();
                                if (tf is IEnumerable<float> ef)
                                    return ef.ToArray();

                                var tfType = tf?.GetType();
                                if (tfType != null)
                                {
                                    var toArray = tfType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public);
                                    if (toArray != null)
                                    {
                                        var arr = toArray.Invoke(tf, null);
                                        if (arr is float[] fa)
                                            return fa;
                                    }
                                }
                            }

                            // Fallback: property 'embedding' on the first item
                            var embProp = first.GetType().GetProperty("embedding") ?? first.GetType().GetProperty("Embedding");
                            if (embProp != null)
                            {
                                var embVal = embProp.GetValue(first);
                                if (embVal is IEnumerable<float> fv)
                                    return fv.ToArray();
                                if (embVal is float[] farr)
                                    return farr;
                            }
                        }
                    }
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { /* method not available on this client shape */ }
            catch (Exception ex)
            {
                Log.Warning(ex, "EmbeddingService: direct GenerateEmbeddingsAsync extraction failed, will try generic extractor");
            }

            // Generic/reflection-based path (existing implementation)
            var result = await InvokeGenerateAsync(_embeddingClient, new[] { input });
            if (result is null)
                return null;

            // Try to extract embedding vector from result object
            var vec = ExtractEmbeddingVector(result);
            if (vec != null)
                return vec;

            // Fallback: serialize and try to parse JSON shapes
            try
            {
                var serialized = JsonSerializer.Serialize(result);
                using var doc = JsonDocument.Parse(serialized);
                var root = doc.RootElement;

                // common shapes: data[0].embedding or value[0].embedding
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array && dataEl.GetArrayLength() > 0)
                    {
                        var first = dataEl[0];
                        if (first.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
                        {
                            return ParseFloatArray(embEl);
                        }
                    }

                    if (root.TryGetProperty("value", out var valueEl) && valueEl.ValueKind == JsonValueKind.Array && valueEl.GetArrayLength() > 0)
                    {
                        var first = valueEl[0];
                        if (first.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
                        {
                            return ParseFloatArray(embEl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "EmbeddingService: JSON parse fallback failed");
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EmbeddingService: error generating embeddings");
            return null;
        }
    }

    private static float[]? ParseFloatArray(JsonElement embEl)
    {
        var list = new List<float>();
        foreach (var item in embEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetSingle(out var f))
                list.Add(f);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    private static float[]? ExtractEmbeddingVector(object? embedResponse)
    {
        if (embedResponse == null)
            return null;

        try
        {
            // direct IEnumerable<float>
            if (embedResponse is IEnumerable<float> floats)
                return floats.ToArray();

            if (embedResponse is float[] arr)
                return arr;

            var type = embedResponse.GetType();

            // Common SDK result may have Value or Data properties
            var valueProp = type.GetProperty("Value") ?? type.GetProperty("value");
            if (valueProp != null)
            {
                var value = valueProp.GetValue(embedResponse);
                if (value is IEnumerable<object> coll)
                {
                    var first = coll.Cast<object>().FirstOrDefault();
                    if (first != null)
                    {
                        // first.embedding or first.Embedding
                        var firstType = first.GetType();
                        var embProp = firstType.GetProperty("embedding") ?? firstType.GetProperty("Embedding");
                        if (embProp != null)
                        {
                            var embVal = embProp.GetValue(first);
                            if (embVal is IEnumerable<float> f)
                                return f.ToArray();
                            if (embVal is float[] fa)
                                return fa;
                        }

                        // try ToFloats on first
                        var toFloats = firstType.GetMethod("ToFloats", BindingFlags.Instance | BindingFlags.Public);
                        if (toFloats != null)
                        {
                            var tf = toFloats.Invoke(first, null);
                            if (tf is IEnumerable<float> ff)
                                return ff.ToArray();
                            var tfType = tf?.GetType();
                            if (tfType != null)
                            {
                                var toArray = tfType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public);
                                if (toArray != null)
                                {
                                    var a = toArray.Invoke(tf, null);
                                    if (a is float[] farr)
                                        return farr;
                                }
                            }
                        }
                    }
                }
            }

            // direct property named embedding on root
            var embRoot = type.GetProperty("embedding") ?? type.GetProperty("Embedding");
            if (embRoot != null)
            {
                var val = embRoot.GetValue(embedResponse);
                if (val is IEnumerable<float> fv)
                    return fv.ToArray();
                if (val is float[] farr)
                    return farr;
            }

            // Try ToFloats on root
            var toFloatsRoot = type.GetMethod("ToFloats", BindingFlags.Instance | BindingFlags.Public);
            if (toFloatsRoot != null)
            {
                var tf = toFloatsRoot.Invoke(embedResponse, null);
                if (tf is IEnumerable<float> ff)
                    return ff.ToArray();
                var tfType = tf?.GetType();
                if (tfType != null)
                {
                    var toArray = tfType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public);
                    if (toArray != null)
                    {
                        var a = toArray.Invoke(tf, null);
                        if (a is float[] farr)
                            return farr;
                    }
                }
            }
        }
        catch
        {
            // ignore and fallback
        }

        return null;
    }

    private static async Task<object?> InvokeGenerateAsync(object client, string[] inputs)
    {
        if (client == null)
            return null;

        var type = client.GetType();

        // collect candidate methods (instance + public static extension holders)
        var methods = new List<MethodInfo>();
        methods.AddRange(type.GetMethods(BindingFlags.Instance | BindingFlags.Public));

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (!t.IsSealed || !t.IsAbstract) // static class check
                    continue;
                methods.AddRange(t.GetMethods(BindingFlags.Static | BindingFlags.Public));
            }
        }

        // method name candidates
        var names = new[] { "GenerateEmbeddingsAsync", "GenerateEmbeddingAsync", "GenerateAsync", "Generate" };

        foreach (var m in methods)
        {
            if (!names.Any(n => m.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || m.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                continue;

            var parameters = m.GetParameters();
            int inputIndex = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i].ParameterType;
                if (typeof(IEnumerable<string>).IsAssignableFrom(p) || p == typeof(string[]))
                {
                    inputIndex = i;
                    break;
                }
            }
            if (inputIndex == -1)
                continue;

            object? invokeTarget = null;
            var args = new object?[parameters.Length];

            if (m.IsStatic)
            {
                // first param should be provider
                if (parameters.Length == 0)
                    continue;
                var firstParam = parameters[0];
                if (!firstParam.ParameterType.IsAssignableFrom(type) && !(firstParam.ParameterType.IsInterface && type.GetInterfaces().Any(i => i == firstParam.ParameterType)))
                    continue;
                args[0] = client;
            }
            else
            {
                invokeTarget = client;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == inputIndex)
                {
                    args[i] = inputs;
                    continue;
                }
                var pType = parameters[i].ParameterType;
                if (pType == typeof(System.Threading.CancellationToken))
                    args[i] = System.Threading.CancellationToken.None;
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

        return null;
    }
}
