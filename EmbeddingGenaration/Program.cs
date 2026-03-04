
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");
}
var credential = new AzureKeyCredential(token);

var endpoint = new Uri("https://models.github.ai/inference");
var openAi = new OpenAIClient(endpoint, new AzureKeyCredential(token));


#region Embedding generation

string model = "gpt-4o-mini"; // model to use for embedding
var words = new[] { "cat", "dog", "kitten", "puppy", "cow", "ox", "zebu" };
Console.WriteLine($"Requesting embeddings from model: {model}");
var resp = await openAi.GetEmbeddingsAsync(model, words);

// Extract embeddings as float[] per input
var embeddings = resp.Value.Data
    .Select(d => d.Embedding.ToArray())
    .ToArray();

for (int i = 0; i < words.Length; i++)
{
    Console.WriteLine($"{words[i]} embedding length: {embeddings[i].Length}");
    // preview first 8 dims
    Console.WriteLine($"Preview: [{string.Join(", ", embeddings[i].Take(8))}...]");
}

#endregion

#region Calculate cosine similarity between two vectors

//if (embedding1 != null && embedding2 != null && embedding1.Length == embedding2.Length)
//{
//    var similarity = CosineSimilarity(embedding1, embedding2);
//    Console.WriteLine($"Cosine similarity between inputs: {similarity:F6}");
//}
//else
//{
//    Console.WriteLine("Could not compute cosine similarity because embeddings are missing or have different lengths.");
//}

#endregion
