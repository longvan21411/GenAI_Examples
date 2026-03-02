// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()    
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");

var token = config["GitHubModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");
}
var credential = new ApiKeyCredential(token) ?? throw new InvalidOperationException("invalid token");
var model = "openai/gpt-5-mini";
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

#region Basic in completion
// create a chat client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();

// send prompts to the model
var prompt  = "What is AI? Explain in max 200 words.";
Console.WriteLine($"user >>> {prompt}");
Console.WriteLine($"\n");

var response = await client.GetResponseAsync(prompt);

Console.WriteLine($"assistant >>> {response}");
Console.WriteLine($"\n");
Console.WriteLine("-----------------------------");
Console.WriteLine($"Tokens used: {response.Usage?.InputTokenCount}");
Console.WriteLine($"Tokens out: {response.Usage?.OutputTokenCount}");
Console.WriteLine($"Total tokens: {response.Usage?.TotalTokenCount} ");
Console.WriteLine("-----------------------------");
#endregion

