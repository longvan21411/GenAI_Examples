using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
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

// create a chat client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();

// Start the conversation with context for the AI model
List<ChatMessage> chatHistory = new() { 
    new ChatMessage(ChatRole.System,"""
        You are a friendly hiking enthusiast who helps people discover fun hikes in their area.
        You introduce yourself when first saying hello.
        When helping people out, you always ask them for this information to inform the hiking recommendation you provide:

        1. The location where they would like to hike
        2. What hiking intensity they are looking for

        You woll then provide three suggestions for nearby hikes that vary in length after you get that information.
        You will also share an interesting fact about the local nature on the hikes when making a recommendation.
        At the end of your response, ask if there is anything else you can help with.
    """)
};

while (true)
{
    // Get user prompt and add to chat history
    Console.WriteLine($"Your prompt:");
    var userPrompt = Console.ReadLine();
    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

    // Stream the AI response and add to chat history
    Console.WriteLine($"AI response:");
    var response = string.Empty;
    await foreach(var item in client.GetStreamingResponseAsync(chatHistory))
    {
        Console.WriteLine(item.Text);
        response = response.Concat(item.Text).ToString();
    }

    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}
