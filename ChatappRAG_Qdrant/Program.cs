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

//Log.Information("Starting Qdrant console app");

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

    // execute ceed movie data JSON
    var jsonPath = Path.Combine("Data", "MovieData.json");
    if (File.Exists(jsonPath))
    {
       QdrantService.ExecuteCeedData(qdrantClient, embeddingGenerator, jsonPath);
    }

    #endregion

    #region Chat with Qdrant using the RAG approach
    //ChatService.ChatAsync(credential, openAIOptions, qdrantClient, embeddingGenerator, collectionName).Wait();
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

        if (query.Trim().Equals("chat_history", StringComparison.OrdinalIgnoreCase))
        {
            Console.Write("=================================================== ");
            Console.Write("\nChat history: ");
            foreach (var chatHistory in history.GetMessages())
            {
                Console.Write("\n");
                Console.Write(chatHistory);
            }
            Console.Write("\n=================================================== ");
            break;
        }

        var queryEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { query }).Result;
        var queryVector = queryEmbedding.Value[0].ToFloats().Span;
        var results = qdrantClient.SearchAsync(collectionName: collectionName,
            vector: queryVector.ToArray(),
            limit: 3
            ).Result;

        var searchResult = new HashSet<string>();
        StringBuilder movieInfo = new StringBuilder();
        foreach (var movie in results)
        {
            if (movie.Payload != null)
            {
                movie.Payload.TryGetValue("movie", out var title);
                movieInfo.Append("\n");
                movieInfo.Append("movie infomation: " + title?.StringValue ?? string.Empty);
                movieInfo.AppendLine("\n==============================================");
                movieInfo.AppendLine();
            }
            //Log.Information("Point Id={Id} Movie information ={info}", movie.Id, movieInfo.ToString());
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
        await foreach (var response in responses)
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