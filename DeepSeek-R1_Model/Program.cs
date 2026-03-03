using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");

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
//    Model = "deepseek/DeepSeek-R1",    
//    MaxTokens = 2048
//};

//Response<ChatCompletions> response = client.Complete(requestOptions);
//System.Console.WriteLine(response.Value.Content);
#endregion

#region Chat app
// Interactive chat application using the deepseek/DeepSeek-R1 model
var chatOptions = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatRequestSystemMessage("You are a helpful assistant. Respond concisely and clearly."),
    },
    Model = "deepseek/DeepSeek-R1",
    Temperature = (float)0.5,
    MaxTokens = 1024,
};

Console.WriteLine("DeepSeek-R1 chat (type 'exit' to quit)");

while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();

    if (userInput == null) // input stream closed
        break;

    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (string.IsNullOrWhiteSpace(userInput))
    {
        Console.WriteLine("Please enter a message or type 'exit' to quit.");
        continue;
    }

    // add user message to history
    chatOptions.Messages.Add(new ChatRequestUserMessage(userInput));

    try
    {
        Response<ChatCompletions> response = client.Complete(chatOptions);
        var assistantReply = response.Value.Content ?? string.Empty;

        Console.WriteLine();
        Console.WriteLine("Assistant:");
        Console.WriteLine(assistantReply);
        Console.WriteLine();

        // keep assistant reply in history so model has context
        chatOptions.Messages.Add(new ChatRequestAssistantMessage(assistantReply));

        // Optionally truncate history if it grows too large
        if (chatOptions.Messages.Count > 30)
        {
            // keep system message and last 20 messages
            var systemMessage = chatOptions.Messages[0];
            var recent = chatOptions.Messages.Skip(Math.Max(1, chatOptions.Messages.Count - 21)).ToList();
            chatOptions.Messages.Clear();
            chatOptions.Messages.Add(systemMessage);
            foreach (var m in recent)
                chatOptions.Messages.Add(m);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error calling model: {ex.Message}");
    }
}
#endregion