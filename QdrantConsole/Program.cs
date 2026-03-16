using Microsoft.Extensions.Configuration;
using OpenAI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/qdrant-console-.log", rollingInterval: Serilog.RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting Qdrant console app");

var client = new QdrantClient("localhost", 6334);

#region Upsert points through QdrantClient.

//const string desiredCollection = "demo_01_custom";
//var points = new List<PointStruct>();
//var p1 = new PointStruct { Id = 1, Vectors = new[] { 0.9f, 0.1f, 0.1f, 0.2f } };
//p1.Payload["color"] = "red";
//points.Add(p1);

//var p2 = new PointStruct { Id = 2, Vectors = new[] { 0.9f, 0.2f, 0.1f, 0.1f } };
//p2.Payload["color"] = "pink";
//points.Add(p2);

//var p3 = new PointStruct { Id = 3, Vectors = new[] { 0.1f, 0.9f, 0.1f, 0.1f } };
//p3.Payload["color"] = "green";
//points.Add(p3);

//var p4 = new PointStruct { Id = 4, Vectors = new[] { 0.1f, 0.9f, 0.1f, 0.2f } };
//p4.Payload["color"] = "blue";
//points.Add(p4);

//var p5 = new PointStruct { Id = 5, Vectors = new[] { 0.1f, 0.9f, 0.1f, 0.1f } };

//p5.Payload["color"] = "violet";
//points.Add(p5);

//var p6 = new PointStruct { Id = 6, Vectors = new[] { 0.1f, 0.8f, 0.1f, 0.1f } };
//p6.Payload["color"] = "yellow";
//points.Add(p6);

//var p7 = new PointStruct { Id = 7, Vectors = new[] { 0.1f, 0.8f, 0.1f, 0.2f } };
//p7.Payload["color"] = "orrange";
//points.Add(p7);

//var p8 = new PointStruct { Id = 8, Vectors = new[] { 0.1f, 0.1f, 0.1f, 0.1f } };
//p8.Payload["color"] = "white";
//points.Add(p8);

//var p9 = new PointStruct { Id = 9, Vectors = new[] { 0.1f, 0.1f, 0.1f, 0.2f } };
//p9.Payload["color"] = "black";
//points.Add(p9);

//var p10 = new PointStruct { Id = 10, Vectors = new[] { 0.1f, 0.1f, 0.3f, 0.2f } };
//p10.Payload["color"] = "brown";
//points.Add(p10);

//var p11 = new PointStruct { Id = 11, Vectors = new[] { 0.1f, 0.1f, 0.3f, 0.3f } };
//p11.Payload["color"] = "gray";
//points.Add(p11);

//try
//{
//    await client.UpsertAsync(collectionName: desiredCollection, points: points);
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error upserting points to Qdrant");
//}

//Log.Information("Finished upserting points to Qdrant");

#endregion

#region Search for similar points using the QdrantClient and print results
//try
//{    
//    var queryVector = new float[] { 0.1f, 0.9f, 0.1f, 0.1f };

//    var results = await client.SearchAsync(collectionName: "demo_01_custom",
//        vector: queryVector,
//        limit: 10
//        );
//    foreach (var item in results)
//    {
//        string color = "(none)";
//        string vectorStr = "(none)";
//        if (item.Payload != null && item.Payload.TryGetValue("color", out var colorObj))
//        {
//            color = colorObj?.ToString() ?? "(null)";
//        }
//        Log.Information("Point Id={Id} Color={Color} Vector={Vector}", item.Id, color, vectorStr);
//    }
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error searching points in Qdrant using QdrantClient");
//}
#endregion

#region Retrieve some points using the QdrantClient and print results
//try
//{    
//    var retrieved = await client.RetrieveAsync(collectionName: desiredCollection,
//        ids: [1, 5, 6, 9],
//        withPayload: true,
//        withVectors: true);

//    foreach (var item in retrieved)
//    {
//        string color = "(none)";
//        string vectorStr = "(none)";
//        if (item.Payload != null && item.Payload.TryGetValue("color", out var colorObj))
//        {
//            color = colorObj?.ToString() ?? "(null)";
//        }                
//        Log.Information("Point Id={Id} Color={Color} Vector={Vector}", item.Id, color, vectorStr);
//    }
//}
//catch(Exception ex)
//{
//    Log.Error(ex, "Error retrieving points from Qdrant using QdrantClient");
//}
#endregion

#region Retrieve some points using the Qdrant REST API (scroll endpoint) and print results
//try
//{
//    using var http = new HttpClient { BaseAddress = new Uri("http://localhost:6333/") };
//    var payload = new { limit = 20 };
//    var res = await http.PostAsJsonAsync($"collections/{desiredCollection}/points/scroll", payload);
//    var body = await res.Content.ReadAsStringAsync();

//    if (!res.IsSuccessStatusCode)
//    {
//        Log.Warning("Failed to retrieve points from Qdrant. Status={Status} Response={Response}", res.StatusCode, body);
//    }
//    else
//    {
//        using var doc = JsonDocument.Parse(body ?? "{}");
//        JsonElement pointsEl = default;
//        bool found = false;

//        if (doc.RootElement.ValueKind == JsonValueKind.Object)
//        {
//            if (doc.RootElement.TryGetProperty("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.Object && resultEl.TryGetProperty("points", out var p1El))
//            {
//                pointsEl = p1El;
//                found = true;
//            }
//            else if (doc.RootElement.TryGetProperty("points", out var p2El))
//            {
//                pointsEl = p2El;
//                found = true;
//            }
//        }

//        if (found && pointsEl.ValueKind == JsonValueKind.Array)
//        {
//            foreach (var item in pointsEl.EnumerateArray())
//            {
//                string idStr = "";
//                if (item.TryGetProperty("id", out var idEl))
//                {
//                    idStr = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString() ?? idEl.GetRawText();
//                }

//                string color = "(none)";
//                if (item.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.Object)
//                {
//                    if (payloadEl.TryGetProperty("color", out var colorEl))
//                        color = colorEl.GetString() ?? colorEl.GetRawText();
//                }

//                Log.Information("Point Id={Id} Color={Color}", idStr, color);
//            }
//        }
//        else
//        {
//            Log.Information("No points found in collection {Collection}. Raw response: {Raw}", desiredCollection, body);
//        }
//    }
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error retrieving points from Qdrant");
//}
#endregion

#region Get a point through the REST API and print the result
//try
//{
//    using var http = new HttpClient { BaseAddress = new Uri("http://localhost:6333/") };

//    var idToGet = 3;
//    var res = await http.GetAsync($"collections/{desiredCollection}/points/{idToGet}");
//    var body = await res.Content.ReadAsStringAsync();

//    if (!res.IsSuccessStatusCode)
//    {
//        Log.Warning("Failed to GET point from Qdrant. Status={Status} Response={Response}", res.StatusCode, body);
//    }
//    else
//    {
//        using var doc = JsonDocument.Parse(body ?? "{}");
//        var root = doc.RootElement;
//        JsonElement pointEl = default;

//        // Qdrant may wrap the result in a `result` object
//        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.Object)
//            pointEl = resultEl;
//        else
//            pointEl = root;

//        string idStr = "";
//        if (pointEl.ValueKind == JsonValueKind.Object && pointEl.TryGetProperty("id", out var idEl))
//        {
//            idStr = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString() ?? idEl.GetRawText();
//        }

//        string payloadStr = "(none)";
//        if (pointEl.ValueKind == JsonValueKind.Object && pointEl.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.Object)
//        {
//            var parts = new List<string>();
//            foreach (var p in payloadEl.EnumerateObject())
//            {
//                parts.Add($"{p.Name}: {p.Value.ToString()}");
//            }
//            payloadStr = string.Join(", ", parts);
//        }

//        string vectorStr = "(none)";
//        if (pointEl.ValueKind == JsonValueKind.Object && pointEl.TryGetProperty("vector", out var vectorEl) && vectorEl.ValueKind == JsonValueKind.Array)
//        {
//            var list = new List<string>();
//            foreach (var n in vectorEl.EnumerateArray())
//            {
//                if (n.ValueKind == JsonValueKind.Number)
//                    list.Add(n.GetSingle().ToString("G"));
//            }
//            vectorStr = "[" + string.Join(", ", list) + "]";
//        }
//        else if (pointEl.ValueKind == JsonValueKind.Object && pointEl.TryGetProperty("vectors", out var vectorsEl) && vectorsEl.ValueKind == JsonValueKind.Object)
//        {
//            var parts = new List<string>();
//            foreach (var prop in vectorsEl.EnumerateObject())
//            {
//                if (prop.Value.ValueKind == JsonValueKind.Array)
//                {
//                    var arr = prop.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetSingle().ToString("G")).ToArray();
//                    parts.Add($"{prop.Name}: [{string.Join(", ", arr)}]");
//                }
//                else
//                {
//                    parts.Add($"{prop.Name}: {prop.Value.ToString()}");
//                }
//            }
//            vectorStr = string.Join("; ", parts);
//        }

//        Log.Information("GET point result: Id={Id} Payload={Payload} Vector={Vector}", idStr, payloadStr, vectorStr);
//    }
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error getting point from Qdrant");
//}
#endregion

IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}

var credential = new System.ClientModel.ApiKeyCredential(token);
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri("https://models.github.ai/inference")
};

// create an embedding generation client
var openAiClient = new OpenAIClient(credential, openAIOptions);
var embeddingGenerator = openAiClient.GetEmbeddingClient("openai/text-embedding-3-small");

#region Upsert a point through QdrantClient with vector data call from `text-embedding-3-small` model

var points = new List<PointStruct>();
var catEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "cat" }).Result;
var catVector = catEmbedding.Value[0].ToFloats().Span;
var catPoint = new PointStruct { Id = 1, Vectors = catVector.ToArray() };
catPoint.Payload["animal"] = "cat";
//points.Add(catPoint);

//var kittenEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "kitten" }).Result;
//var kittenVector = kittenEmbedding.Value[0].ToFloats().Span;
//var kittenPoint = new PointStruct { Id = 2, Vectors = kittenVector.ToArray() };
//kittenPoint.Payload["animal"] = "kitten";
//points.Add(kittenPoint);

//var dogEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "dog" }).Result;
//var dogVector = dogEmbedding.Value[0].ToFloats().Span;
//var dogPoint = new PointStruct { Id = 3, Vectors = dogVector.ToArray() };
//dogPoint.Payload["animal"] = "dog";
//points.Add(dogPoint);

//var puppyEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "puppy" }).Result;
//var puppyVector = puppyEmbedding.Value[0].ToFloats().Span;
//var puppyPoint = new PointStruct { Id = 4, Vectors = puppyVector.ToArray() };
//puppyPoint.Payload["animal"] = "puppy";
//points.Add(puppyPoint);

//var lionEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "lion" }).Result;
//var lionVector = lionEmbedding.Value[0].ToFloats().Span;
//var lionPoint = new PointStruct { Id = 5, Vectors = lionVector.ToArray() };
//lionPoint.Payload["animal"] = "lion";
//points.Add(lionPoint);

//var elephantEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "elephant" }).Result;
//var elephantVector = elephantEmbedding.Value[0].ToFloats().Span;
//var elephantPoint = new PointStruct { Id = 6, Vectors = elephantVector.ToArray() };
//elephantPoint.Payload["animal"] = "elephant";
//points.Add(elephantPoint);

//var leopardEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "leopard" }).Result;
//var leopardVector = leopardEmbedding.Value[0].ToFloats().Span;
//var leopardPoint = new PointStruct { Id = 7, Vectors = leopardVector.ToArray() };
//leopardPoint.Payload["animal"] = "leopard";
//points.Add(leopardPoint);

//var tigerEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "tiger" }).Result;
//var tigerVector = tigerEmbedding.Value[0].ToFloats().Span;
//var tigerPoint = new PointStruct { Id = 8, Vectors = tigerVector.ToArray() };
//tigerPoint.Payload["animal"] = "tiger";
//points.Add(tigerPoint);

//var caracalEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "caracal" }).Result;
//var caracalVector = caracalEmbedding.Value[0].ToFloats().Span;
//var caracalPoint = new PointStruct { Id = 9, Vectors = caracalVector.ToArray() };
//caracalPoint.Payload["animal"] = "caracal";
//points.Add(caracalPoint);

//var jaguarEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "jaguar" }).Result;
//var jaguarVector = jaguarEmbedding.Value[0].ToFloats().Span;
//var jaguarPoint = new PointStruct { Id = 10, Vectors = jaguarVector.ToArray() };
//jaguarPoint.Payload["animal"] = "jaguar";
//points.Add(jaguarPoint);

//var cheetahEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "cheetah" }).Result;
//var cheetahVector = cheetahEmbedding.Value[0].ToFloats().Span;
//var cheetahPoint = new PointStruct { Id = 11, Vectors = cheetahVector.ToArray() };
//cheetahPoint.Payload["animal"] = "cheetah";
//points.Add(cheetahPoint);

//var pumaEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "puma" }).Result;
//var pumaVector = pumaEmbedding.Value[0].ToFloats().Span;
//var pumaPoint = new PointStruct { Id = 12, Vectors = pumaVector.ToArray() };
//pumaPoint.Payload["animal"] = "puma";
//points.Add(pumaPoint);

//var hippoEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "hippo" }).Result;
//var hipoVector = hippoEmbedding.Value[0].ToFloats().Span;
//var hipoPoint = new PointStruct { Id = 13, Vectors = hipoVector.ToArray() };
//hipoPoint.Payload["animal"] = "hippo";
//points.Add(hipoPoint);

//var bearEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "bear" }).Result;
//var bearVector = bearEmbedding.Value[0].ToFloats().Span;
//var bearPoint = new PointStruct { Id = 14, Vectors = hipoVector.ToArray() };
//bearPoint.Payload["animal"] = "bear";
//points.Add(bearPoint);

//try
//{
//    await client.UpsertAsync(collectionName: "RAG_01", points: points);
//    Log.Information("Finished upserting points to Qdrant with vector size = 1536");
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error upserting points to Qdrant");
//}

#endregion

#region Upsert a solar system point through QdrantClient with vector data call from `text-embedding-3-small` model
//var points = new List<PointStruct>();

//var solarEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "sun" }).Result;
//var solarVector = solarEmbedding.Value[0].ToFloats().Span;
//var solarPoint = new PointStruct { Id = 15, Vectors = solarVector.ToArray() };
//solarPoint.Payload["solar"] = "sun";
//points.Add(solarPoint);

//var moonEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "moon" }).Result;
//var moonVector = moonEmbedding.Value[0].ToFloats().Span;
//var moonPoint = new PointStruct { Id = 16, Vectors = moonVector.ToArray() };
//moonPoint.Payload["solar"] = "moon";
//points.Add(moonPoint);

//var earthEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "earth" }).Result;
//var earthVector = earthEmbedding.Value[0].ToFloats().Span;
//var earthPoint = new PointStruct { Id = 17, Vectors = earthVector.ToArray() };
//earthPoint.Payload["solar"] = "earth";
//points.Add(earthPoint);

//var marsEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "mars" }).Result;
//var marsVector = marsEmbedding.Value[0].ToFloats().Span;
//var marsPoint = new PointStruct { Id = 18, Vectors = marsVector.ToArray() };
//marsPoint.Payload["solar"] = "mars";
//points.Add(marsPoint);

//var venusEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "venus" }).Result;
//var venusVector = venusEmbedding.Value[0].ToFloats().Span;
//var venusPoint = new PointStruct { Id = 19, Vectors = venusVector.ToArray() };
//venusPoint.Payload["solar"] = "venus";
//points.Add(venusPoint);

//var jupiterEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "jupiter" }).Result;
//var jupiterVector = jupiterEmbedding.Value[0].ToFloats().Span;
//var jupiterPoint = new PointStruct { Id = 20, Vectors = jupiterVector.ToArray() };
//jupiterPoint.Payload["solar"] = "jupiter";
//points.Add(jupiterPoint);

//var saturnEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "saturn" }).Result;
//var saturnVector = saturnEmbedding.Value[0].ToFloats().Span;
//var saturnPoint = new PointStruct { Id = 21, Vectors = saturnVector.ToArray() };
//saturnPoint.Payload["solar"] = "saturn";
//points.Add(saturnPoint);

//var uranusEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "uranus" }).Result;
//var uranusVector = uranusEmbedding.Value[0].ToFloats().Span;
//var uranusPoint = new PointStruct { Id = 22, Vectors = uranusVector.ToArray() };
//uranusPoint.Payload["solar"] = "uranus";
//points.Add(uranusPoint);

//var neptuneEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "neptune" }).Result;
//var neptuneVector = neptuneEmbedding.Value[0].ToFloats().Span;
//var neptunePoint = new PointStruct { Id = 23, Vectors = neptuneVector.ToArray() };
//neptunePoint.Payload["solar"] = "neptune";
//points.Add(neptunePoint);

//var plutoEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "pluto" }).Result;
//var plutoVector = plutoEmbedding.Value[0].ToFloats().Span;
//var plutoPoint = new PointStruct { Id = 24, Vectors = plutoVector.ToArray() };
//plutoPoint.Payload["solar"] = "pluto";
//points.Add(plutoPoint);

//try
//{
//    await client.UpsertAsync(collectionName: "RAG_01", points: points);
//    Log.Information("Finished upserting solar system point to Qdrant with vector size = 1536");
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error upserting solar system point to Qdrant");
//}
#endregion

#region Search for similar points using the QdrantClient and print results
//try
//{
//    var queryEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "sun" }).Result;
//    var queryVector = queryEmbedding.Value[0].ToFloats().Span;
//    var results = await client.SearchAsync(collectionName: "RAG_01",
//        vector: queryVector.ToArray(),
//        limit: 10
//        );
//    foreach (var item in results)
//    {
//        string solar = "(none)";
//        if (item.Payload != null && item.Payload.TryGetValue("solar", out var animalObj))
//        {
//            solar = animalObj?.ToString() ?? "(null)";
//        }
//        Log.Information("Point Id={Id} Solar={solar}", item.Id, solar);
//    }
//}
//catch (Exception ex)
//{
//    Log.Error(ex, "Error searching points in Qdrant using QdrantClient");
//}
#endregion
