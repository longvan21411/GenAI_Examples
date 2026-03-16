using ChatappRAG_Qdrant.Model;
using ChatappRAG_Qdrant.Service;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using OpenAI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/qdrant-console-.log", rollingInterval: Serilog.RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting Qdrant console app");

try
{
    // Read the API token for GitHub AI Models from user secrets
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

    // Create a Qdrant client to connect to the local Qdrant instance (gRPC client for management)
    var qdrantClient = new QdrantClient("localhost", 6334);

    var collectionName = Movie.CollectionName;        
    var collections = await qdrantClient.ListCollectionsAsync();
    var collectionExists = collections.Contains(collectionName);

    #region Create Qdrant collection and points -- seed data
    //if (!collectionExists)
    //{
    //    await qdrantClient.CreateCollectionAsync(collectionName, new VectorParams { Size = 1536, Distance = Distance.Cosine });
    //}
    //else
    //{
    //    Log.Information("Collection '{Collection}' already exists.", collectionName);
    //}

    //// Load movie data JSON
    //var jsonPath = Path.Combine("Data", "MovieData.json");
    //if (!File.Exists(jsonPath))
    //{
    //    Log.Error("Movie data file not found at {Path}", jsonPath);
    //    return;
    //}

    //using var stream = File.OpenRead(jsonPath);
    //using var doc = await JsonDocument.ParseAsync(stream);
    //if (!doc.RootElement.TryGetProperty("movies", out var moviesEl) || moviesEl.ValueKind != JsonValueKind.Array)
    //{
    //    Log.Error("Movie data JSON does not contain a 'movies' array.");
    //    return;
    //}

    //var restPoints = new List<object>();
    //var points = new List<PointStruct>();
    //long countItem = 0;
    //foreach (var item in moviesEl.EnumerateArray())
    //{
    //    try
    //    {

    //        var id = item.GetProperty("id").GetInt32();
    //        var title = item.GetProperty("title").GetString() ?? string.Empty;
    //        var year = item.GetProperty("year").GetString() ?? string.Empty;
    //        var genres = item.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
    //            ? g.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
    //            : Array.Empty<string>();
    //        var director = item.TryGetProperty("director", out var d) ? d.GetString() ?? string.Empty : string.Empty;
    //        var actors = item.TryGetProperty("actors", out var a) ? a.GetString() ?? string.Empty : string.Empty;
    //        var plot = item.TryGetProperty("plot", out var p) ? p.GetString() ?? string.Empty : string.Empty;
    //        var poster = item.TryGetProperty("posterUrl", out var pu) ? pu.GetString() ?? string.Empty : string.Empty;

    //        var textForEmbedding = $"{title} {year} {string.Join(' ', genres)} {director} {actors} {plot}";

    //        // Generate embedding           
    //        var movieEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { textForEmbedding }).Result;
    //        var movieVector = movieEmbedding.Value[0].ToFloats().Span;

    //        countItem++;
    //        var point = new PointStruct
    //        {
    //            Id = new PointId { Num = (ulong)countItem },
    //            Vectors = new Vectors { Vector = new Qdrant.Client.Grpc.Vector { Data = { movieVector.ToArray() } } }
    //        };
    //        // Add payload entries individually to the read-only MapField
    //        point.Payload.Add("title", new Value { StringValue = title });
    //        point.Payload.Add("year", new Value { StringValue = year });
    //        point.Payload.Add("director", new Value { StringValue = director });
    //        point.Payload.Add("actors", new Value { StringValue = actors });
    //        point.Payload.Add("plot", new Value { StringValue = plot });
    //        point.Payload.Add("posterUrl", new Value { StringValue = poster });

    //        points.Add(point);
    //    }
    //    catch (Exception ex)
    //    {
    //        Log.Warning(ex, "Failed processing a movie element");
    //    }
    //}

    //await qdrantClient.UpsertAsync(collectionName: collectionName, points: points);
    //Log.Information("Finished upserting points to Qdrant with vector size = 1536");
    #endregion

    #region Chat with Qdrant using the RAG approach
    // create a chat client
    var model = "openai/gpt-5-mini";
    var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();
    var systemMessage = new ChatMessage(ChatRole.System, content: "You are a helpful assistant specialized in moive knowledge");
    var history = new ConversationMemory();
    while (true)
    {
        Console.Write("\nYour question: ");
        var query = Console.ReadLine();

        if (query == null) 
            break;

        if (query.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Exiting the application. Goodbye!");
            break;
        }           

        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Please enter a prompt or type 'exit' to quit.");
            continue;
        }

        var queryEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { query }).Result;
        var queryVector = queryEmbedding.Value[0].ToFloats().Span;
        var results = await qdrantClient.SearchAsync(collectionName: collectionName,
            vector: queryVector.ToArray(),
            limit: 10
            );

        var searchResult = new HashSet<string>();
        StringBuilder movieInfo = new StringBuilder();
        foreach (var movie in results)
        {
            
            if (movie.Payload != null)
            {
                movie.Payload.TryGetValue("title", out var title);
                movieInfo.Append("\n");
                movieInfo.Append("title of movie: " +title?.ToString() ?? string.Empty);

                movie.Payload.TryGetValue("year", out var year);
                movieInfo.Append("\n");
                movieInfo.Append("year: " + year?.ToString() ?? string.Empty);

                movie.Payload.TryGetValue("director", out var director);
                movieInfo.Append("\n");
                movieInfo.Append("directed by: " + director?.ToString() ?? string.Empty);

                movie.Payload.TryGetValue("actors", out var actors);
                movieInfo.Append("\n");
                movieInfo.Append("actors: " + actors?.ToString() ?? string.Empty);

                movie.Payload.TryGetValue("plot", out var plot);
                movieInfo.Append("\n");
                movieInfo.Append("plot: " + plot?.ToString() ?? string.Empty);

                movie.Payload.TryGetValue("posterUrl", out var posterUrl);
                movieInfo.Append("\n");
                movieInfo.Append("poster: " + posterUrl?.ToString() ?? string.Empty);
            }
            Log.Information("Point Id={Id} Movie information ={info}", movie.Id, movieInfo.ToString());
        }

        var context = string.Join(Environment.NewLine, searchResult);
        var previousMessage = string.Join(Environment.NewLine, history.GetMessages());

        var prompt = $"""
            Context:
            {context}

            Based on the context above, please answer the following question.
            If the context doesn't provide the answer, say you don't know based on the provide information.

            User question: {query}

            Answer: 
            """;

        var userMsg = new ChatMessage(ChatRole.User, prompt);
        history.AddMessage(query.Trim());

        var responseText = new StringBuilder();
        var responses = client.GetStreamingResponseAsync([systemMessage, userMsg], options: null, CancellationToken.None);
        await foreach(var response in responses)
        {
            Console.Write(response.Text);
            responseText.Append(response.Text);
        }

        history.AddMessage(responseText.ToString().Trim());
        Console.WriteLine("\n");
        
    }
    #endregion

}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled error in Qdrant upsert program");
}
finally
{
    Log.Information("Finish Qdrant console app");
}