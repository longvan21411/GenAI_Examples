using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

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
//    Model = "openai/gpt-4o-mini",
//    Temperature = (float)0.5,
//    MaxTokens = 4096,
//};

//Response<ChatCompletions> response = client.Complete(requestOptions);
//System.Console.WriteLine(response.Value.Content);

#endregion

#region Chat app

//// Interactive chat using the openai/gpt-4o-mini model
//var chatOptions = new ChatCompletionsOptions()
//{
//    Messages =
//    {
//        new ChatRequestSystemMessage("You are a helpful assistant. Respond concisely and clearly."),
//    },
//    Model = "openai/gpt-4o-mini",
//    Temperature = (float)0.5,
//    MaxTokens = 4096,
//};

//// Toggle to true to render the model response as a streaming output (simulated by printing characters incrementally).
//// Note: If your SDK provides a true streaming API, replace the simulated block with the SDK streaming call.
//bool useSimulatedStreaming = true;

//Console.WriteLine("gpt-4o-mini chat (type 'exit' to quit)");
//
//while (true)
//{
//    Console.Write("You: ");
//    var userInput = Console.ReadLine();
//
//    if (userInput == null) // input stream closed
//        break;
//
//    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
//        break;
//
//    if (string.IsNullOrWhiteSpace(userInput))
//    {
//        Console.WriteLine("Please enter a message or type 'exit' to quit.");
//        continue;
//    }
//
//    // add user message to history
//    chatOptions.Messages.Add(new ChatRequestUserMessage(userInput));
//
//    try
//    {
//        Response<ChatCompletions> response = client.Complete(chatOptions);
//        var assistantReply = response.Value.Content ?? string.Empty;
//
//        Console.WriteLine();
//        Console.WriteLine("Assistant:");
//
//        if (useSimulatedStreaming)
//        {
//            // print characters one by one to simulate streaming
//            foreach (var ch in assistantReply)
//            {
//                Console.Write(ch);
//                Thread.Sleep(8); // adjust delay to taste
//            }
//            Console.WriteLine();
//        }
//        else
//        {
//            Console.WriteLine(assistantReply);
//        }
//
//        Console.WriteLine();
//
//        // keep assistant reply in history so model has context
//        chatOptions.Messages.Add(new ChatRequestAssistantMessage(assistantReply));
//
//        // truncate history if it grows too large
//        if (chatOptions.Messages.Count > 30)
//        {
//            var systemMessage = chatOptions.Messages.First();
//            var recent = chatOptions.Messages.Skip(Math.Max(1, chatOptions.Messages.Count - 21)).ToList();
//            chatOptions.Messages.Clear();
//            chatOptions.Messages.Add(systemMessage);
//            foreach (var m in recent) chatOptions.Messages.Add(m);
//        }
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Error calling model: {ex.Message}");
//    }
//}

#endregion

#region Invoke function using model response (get_weather example)

// Local function that returns a JSON string with weather info.
// First prompt to model so some field data in the response may be missing or invalid.
// The next prompt will include the function result and then send to model again.
// In the response to the second prompt, the model can confirm it received the function result and use that data to answer the original question more accurately.
string GetWeather(string location, string date)
{
    // Normalize inputs
    location = location?.Trim() ?? "Unknown";
    date = date?.Trim() ?? string.Empty;

    DateTime targetDate;
    if (!DateTime.TryParse(date, out targetDate))
    {
        targetDate = DateTime.UtcNow.Date;
    }

    // Create a deterministic seed from location+date using SHA256 and take 4 bytes.
    using var sha = SHA256.Create();
    var seedSource = $"{location.ToLowerInvariant()}|{targetDate:yyyy-MM-dd}";
    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seedSource));
    int seed = BitConverter.ToInt32(hash, 0);

    var rng = new Random(seed);

    // Generate plausible sample weather values
    int temperatureC = rng.Next(-10, 36); // -10 to 35 C
    double precipitationProbability = Math.Round(rng.NextDouble() * 100, 1); // 0.0 - 100.0 %
    double windKph = Math.Round(rng.NextDouble() * 60, 1); // 0 - 60 kph
    var summaries = new[] { "Clear", "Sunny", "Partly Cloudy", "Cloudy", "Light Rain", "Heavy Rain", "Snow", "Windy", "Fog" };
    string summary = summaries[rng.Next(summaries.Length)];

    var resultObj = new
    {
        location,
        date = targetDate.ToString("yyyy-MM-dd"),
        temperatureC,
        temperatureF = (int)Math.Round(temperatureC * 9.0 / 5.0 + 32),
        summary,
        precipitationProbability,
        windKph,
        units = new { temperature = "C", wind = "kph", precipitation = "percent" }
    };

    return JsonSerializer.Serialize(resultObj);
}

var functionChatOptions = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatRequestSystemMessage(
            "You are a JSON-only responder. When asked to call an internal function, emit exactly one JSON object with 'name' and 'arguments' keys.\n" +
            "For weather queries, use: {\"name\":\"get_weather\",\"arguments\":{\"location\":\"<city or place>\",\"temperatureC\":\"<temperatureC>\",\"temperatureF\":\"<temperatureF>\",\"wind\":\"<windKph>\",\"date\":\"YYYY-MM-DD (optional)\"}}"),
        new ChatRequestUserMessage("What's the weather for Hanoi on 2026-01-10?")
    },
    Model = "openai/gpt-4o-mini",
    Temperature = 0f,
    MaxTokens = 512,
};

try
{
    Response<ChatCompletions> firstResponse = client.Complete(functionChatOptions);
    var assistantPayload = firstResponse.Value.Content ?? string.Empty;
    Console.WriteLine("Assistant payload:");
    Console.WriteLine(assistantPayload);

    // Find JSON start and parse
    var startIdx = assistantPayload.IndexOf('{');
    if (startIdx < 0)
    {
        Console.WriteLine("No JSON payload found in assistant response.");
    }
    else
    {
        var json = assistantPayload.Substring(startIdx);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameEl) && nameEl.GetString() == "get_weather")
            {
                if (root.TryGetProperty("arguments", out var argumentsEl))
                {
                    var location = argumentsEl.GetProperty("location").GetString() ?? string.Empty;
                    var date = argumentsEl.TryGetProperty("date", out var dEl) ? dEl.GetString() ?? string.Empty : string.Empty;

                    var functionResultJson = GetWeather(location, date);

                    // Append the function result to the conversation so model can produce a confirmation.
                    functionChatOptions.Messages.Add(new ChatRequestUserMessage($"Function 'get_weather' executed with result: {functionResultJson}"));

                    Response<ChatCompletions> confirmation = client.Complete(functionChatOptions);
                    Console.WriteLine("Assistant confirmation:");
                    Console.WriteLine(confirmation.Value.Content);
                }
                else
                {
                    Console.WriteLine("'arguments' object missing in function call JSON.");
                }
            }
            else
            {
                Console.WriteLine("Assistant did not request 'get_weather'.");
            }
        }
        catch (JsonException je)
        {
            Console.WriteLine($"Failed to parse assistant JSON: {je.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error during function-calling flow: {ex.Message}");
}

#endregion