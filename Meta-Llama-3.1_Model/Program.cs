using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}
var credential = new AzureKeyCredential(token);

var client = new ChatCompletionsClient(
    new Uri("https://models.github.ai/inference"),
    credential,
    new AzureAIInferenceClientOptions());

#region Basic text completion
//var requestOptions = new ChatCompletionsOptions()
//{
//    Messages =
//    {
//        new ChatRequestSystemMessage(""),
//        new ChatRequestUserMessage("Can you explain the basics of machine learning?"),
//    },
//    Model = "meta/Meta-Llama-3.1-8B-Instruct",
//    Temperature = (float) 0.8,
//    MaxTokens = 2048,

//};

//Response<ChatCompletions> response = client.Complete(requestOptions);
//System.Console.WriteLine(response.Value.Content);
#endregion

#region Streaming chat app response

// Interactive streaming-style chat using the meta/Meta-Llama-3.1-8B-Instruct model
var chatOptions = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatRequestSystemMessage("You are an assistant that answers clearly and concisely. Use helpful examples when appropriate."),
    },
    Model = "meta/Meta-Llama-3.1-8B-Instruct",
    Temperature = (float)0.8,
    MaxTokens = 2048,
};

// If your SDK provides a real streaming API, replace the simulated streaming below with the SDK streaming call.
bool useSimulatedStreaming = true;

Console.WriteLine("Meta-Llama-3.1 chat (type 'exit' to quit)");

while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();

    if (userInput == null)
        break;

    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (string.IsNullOrWhiteSpace(userInput))
    {
        Console.WriteLine("Please enter a message or type 'exit' to quit.");
        continue;
    }

    chatOptions.Messages.Add(new ChatRequestUserMessage(userInput));

    try
    {
        // Non-streaming SDK call to get full content, then render as streaming (character by character)
        Response<ChatCompletions> response = client.Complete(chatOptions);
        var assistantReply = response.Value.Content ?? string.Empty;

        Console.WriteLine();
        Console.WriteLine("Assistant:");

        if (useSimulatedStreaming)
        {
            // Simulate streaming by printing small chunks with a short delay
            const int chunkSize = 8; // characters per chunk
            for (int i = 0; i < assistantReply.Length; i += chunkSize)
            {
                var len = Math.Min(chunkSize, assistantReply.Length - i);
                Console.Write(assistantReply.Substring(i, len));
                Thread.Sleep(15); // adjust delay to taste
            }
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine(assistantReply);
        }

        Console.WriteLine();

        // keep assistant reply in history
        chatOptions.Messages.Add(new ChatRequestAssistantMessage(assistantReply));

        // truncate history to keep payload reasonable
        if (chatOptions.Messages.Count > 40)
        {
            var system = chatOptions.Messages.First();
            var recent = chatOptions.Messages.Skip(Math.Max(1, chatOptions.Messages.Count - 31)).ToList();
            chatOptions.Messages.Clear();
            chatOptions.Messages.Add(system);
            foreach (var m in recent) chatOptions.Messages.Add(m);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error calling model: {ex.Message}");
    }
}

#endregion