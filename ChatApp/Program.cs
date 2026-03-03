using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

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

// NOTE: confirm ApiKeyCredential is provided by the OpenAI package you installed
var credential = new ApiKeyCredential(token) ?? throw new InvalidOperationException("invalid token");
var model = "openai/gpt-5-mini";
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

// create a chat client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();

// Start the conversation with context for the AI model
List<ChatMessage> chatHistory = new()
{
    new ChatMessage(ChatRole.System, """
        You are a friendly hiking enthusiast who helps people discover fun hikes in their area.
        You introduce yourself when first saying hello.
        When helping people out, you always ask them for this information to inform the hiking recommendation you provide:

        1. The location where they would like to hike
        2. What hiking intensity they are looking for

        You will then provide three suggestions for nearby hikes that vary in length after you get that information.
        You will also share an interesting fact about the l     ocal nature on the hikes when making a recommendation.
        At the end of your response, ask if there anything else you can help with.
    """)
};

while (true)
{
    Console.Write("Your prompt (type 'exit' to quit): ");
    var userPrompt = Console.ReadLine();

    if (userPrompt == null) // input stream closed
        break;

    if (userPrompt.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (string.IsNullOrWhiteSpace(userPrompt))
    {
        Console.WriteLine("Please enter a prompt or type 'exit' to quit.");
        continue;
    }

    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

    Console.WriteLine("AI response:");
    var response = string.Empty;

    try
    {
        await foreach (ChatResponseUpdate item in client.GetStreamingResponseAsync(chatHistory, options: null, CancellationToken.None))
        {
            Console.Write(item.Text);
            response += item.Text;
        }
       
        Console.WriteLine();
        // Keep assistant reply in history for context
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nRequest cancelled.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError while streaming response: {ex.Message}");        
    }
}
