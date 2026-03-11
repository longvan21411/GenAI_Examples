using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System;
using System.ClientModel;
using System.Numerics.Tensors;

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

#region Generate a single embedding
string[] textToEmbed = new[] { "Hello world!" };
var embeddingResponse = embeddingGenerator.GenerateEmbeddingsAsync(textToEmbed).Result;

if (embeddingResponse != null)
{
    for (int i = 0; i < embeddingResponse.Value.Count; i++)
    {
        var vectorMemory = embeddingResponse.Value[i].ToFloats();
        foreach (var value in vectorMemory.Span)
        {
            Console.Write(value);
        }
        var vector = vectorMemory.ToArray();
        Console.WriteLine($"\n");
        Console.WriteLine($"Input: {textToEmbed[i]}, Embedding vector length: {vector.Length}");
    }
}
#endregion

#region Compare multiple embeddings using cosine similarity

//var catEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "cat"}).Result;
//var catVector = catEmbedding.Value[0].ToFloats().Span;

//var kittenEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "kitten" }).Result;
//var kittenVector = kittenEmbedding.Value[0].ToFloats().Span;

//var dogEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "dog" }).Result;
//var dogVector = dogEmbedding.Value[0].ToFloats().Span;

//var puppyEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "puppy" }).Result;
//var puppyVector = puppyEmbedding.Value[0].ToFloats().Span;

//var lionEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "lion" }).Result;
//var lionVector = lionEmbedding.Value[0].ToFloats().Span;

//var elephantEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "elephant" }).Result;
//var elephantVector = elephantEmbedding.Value[0].ToFloats().Span;

//var sunEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { "sun" }).Result;
//var sunVector = sunEmbedding.Value[0].ToFloats().Span;

//Console.WriteLine($"Cosine similarity between 'cat' and 'kitten': {TensorPrimitives.CosineSimilarity(catVector, kittenVector)}");
//Console.WriteLine($"Cosine similarity between 'cat' and 'dog': {TensorPrimitives.CosineSimilarity(catVector, dogVector)}");
//Console.WriteLine($"Cosine similarity between 'cat' and 'puppy': {TensorPrimitives.CosineSimilarity(catVector, puppyVector)}");
//Console.WriteLine($"Cosine similarity between 'dog' and 'puppy': {TensorPrimitives.CosineSimilarity(dogVector, puppyVector)}");

//Console.WriteLine($"Cosine similarity between 'cat' and 'lion': {TensorPrimitives.CosineSimilarity(catVector, lionVector)}");
//Console.WriteLine($"Cosine similarity between 'lion' and 'elephant': {TensorPrimitives.CosineSimilarity(lionVector, elephantVector)}");
//Console.WriteLine($"Cosine similarity between 'elephant' and 'sun': {TensorPrimitives.CosineSimilarity(elephantVector, sunVector)}");
#endregion



