using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using System.Net;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");
}

var endpoint = new Uri("https://models.github.ai/inference");
var credential = new AzureKeyCredential(token);
var model = "microsoft/Phi-4-mini-reasoning";
var client = new ChatCompletionsClient(
    endpoint,
    credential,
    new AzureAIInferenceClientOptions());

var requestOptions = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatRequestUserMessage("What is the capital of Teheran?"),
    },
    Temperature = 1.0f,
    NucleusSamplingFactor = 1.0f,
    MaxTokens = 1000,
    Model = model
};

Response<ChatCompletions> response = client.Complete(requestOptions);
Console.WriteLine(response.Value.Content);