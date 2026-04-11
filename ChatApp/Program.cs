using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()// only run and impact on development environment, ignored in production
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");

var token = config["GitHubAIModels:Token"];// this should be set in user secrets with the key "GitHubAIModels:Token"
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}

// NOTE: confirm ApiKeyCredential is provided by the OpenAI package you installed
var credential = new ApiKeyCredential(token) ?? throw new InvalidOperationException("invalid token");
//var model = "openai/gpt-5-mini";
var model = "openai/gpt-4o-mini";
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

// create a chat client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();

// Start the conversation with context for the AI model
List<ChatMessage> chatHistories = new()
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
Console.WriteLine("The context for chat is: ");
Console.WriteLine(chatHistories.First().Text);

while (true)
{
    Console.Write("Your question: ");
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

    if (userPrompt.Trim().Equals("chat_history", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("=======================Chat history===============");
        foreach (var message in chatHistories)
        {
            Console.WriteLine($"{message.Role}: {message.Text}");
            Console.WriteLine($"\n");
        }
        Console.WriteLine("=======================End history===============");
        continue;
    }

    chatHistories.Add(new ChatMessage(ChatRole.User, userPrompt));

    Console.WriteLine("AI response:");
    var response = string.Empty;

    try
    {
        await foreach (ChatResponseUpdate item in client.GetStreamingResponseAsync(chatHistories, options: null, CancellationToken.None))
        {
            Console.Write(item.Text);
            response += item.Text;
        }
       
        Console.WriteLine();
        // Keep assistant reply in history for context
        chatHistories.Add(new ChatMessage(ChatRole.Assistant, response));
    }
    catch (ClientResultException cre) when (cre.Status == 429)
    {
        Console.WriteLine("\nRate limited. Waiting 60 seconds before retrying...");       
        chatHistories.RemoveAt(chatHistories.Count - 1);
        await Task.Delay(TimeSpan.FromSeconds(60));
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
