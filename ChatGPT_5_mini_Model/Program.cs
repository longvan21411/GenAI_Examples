// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

//Console.WriteLine("Hello, World!");
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()    
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");
var credential = new ApiKeyCredential("GitHub_Models:Token")?? throw new InvalidOperationException("invalid token");
var model = "openai/gpt-5-mini";
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

// create a chat client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();

// send prompts to the model
var response = await client.GetResponseAsync("What is AI? Explain in max 200 worlds");
Console.WriteLine(response);

