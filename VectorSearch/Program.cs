using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI;
using System;
using System.ClientModel;
using System.Numerics.Tensors;
using VectorSearch.Model;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");

var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}

var credential = new ApiKeyCredential(token) ?? throw new InvalidOperationException("invalid token");

var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

// create an embedding generation client
var openAiClient = new OpenAIClient(credential, openAIOptions);
var embeddingGenerator = openAiClient.GetEmbeddingClient("openai/text-embedding-3-small");

// Create and populate an in-memory vector store
var vectorStore = new InMemoryVectorStore();
var movieStore = vectorStore.GetCollection<int, Movie>("movies");

await movieStore.EnsureCollectionExistsAsync();
foreach(var movie in MovieData.GetMovies())
{
    var embeddingResponse = await embeddingGenerator.GenerateEmbeddingsAsync(new[] { movie.Description });
    var vector = embeddingResponse.Value[0].ToFloats().ToArray();
    movie.Vector = vector;
    await movieStore.UpsertAsync(movie);
}

// Embedded the user's query
var query = "I want to watch the animals movie.";
var queryEmbeddingResponse = await embeddingGenerator.GenerateEmbeddingsAsync(new[] { query });
var queryVector = queryEmbeddingResponse.Value[0].ToFloats().ToArray();

// Search the knowledge store based on the user's prompt.
var searchResults = movieStore.SearchAsync(queryVector, top: 2);

// Look the results
await foreach(var result in searchResults)
{
    Console.WriteLine($"Movie title: {result.Record.Title}");
    Console.WriteLine($"Movie description: {result.Record.Description}");
    Console.WriteLine($"Movie score: {result.Score}");
    Console.WriteLine();
}
