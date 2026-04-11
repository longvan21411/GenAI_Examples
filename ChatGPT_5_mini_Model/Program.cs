// See https://aka.ms/new-console-template for more information
using ChatGPT_5_mini_Model.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()    
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");

var token = config["GitHubAIModels:Token"];// this should be set in user secrets with the key "GitHubAIModels:Token"
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
 


#region Basic in completion

// send prompts to the model
var prompt = "What is AI? Explain in max 200 words.";
Console.WriteLine($"user >>> {prompt}");

try {
    var response = await client.GetResponseAsync(prompt);

    Console.WriteLine($"assistant >>> {response}");
    Console.WriteLine($"\n");
    Console.WriteLine("-----------------------------");
    Console.WriteLine($"Tokens used: {response.Usage?.InputTokenCount}");
    Console.WriteLine($"Tokens out: {response.Usage?.OutputTokenCount}");
    Console.WriteLine($"Total tokens: {response.Usage?.TotalTokenCount} ");
    Console.WriteLine("-----------------------------");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

#endregion

#region Streaming response
//var prompt = "What is AI? Explain in max 200 words.";
//Console.WriteLine($"user >>> {prompt}");

//Console.WriteLine($"assistant >>>"); 
//var responses =  client.GetStreamingResponseAsync(prompt);
//await foreach (var message in responses)
//{
//    Console.Write(message.Text);
//}
//Console.WriteLine($"\n");
#endregion

#region Classification
//var prompt = "Classify the following text into one of the following categories: [Technology, Health, Finance, Education]. Text: 'The stock market is experiencing significant volatility due to economic uncertainties.'";
//Console.WriteLine($"user >>> {prompt}");
//Console.WriteLine($"assistant >>>");
//var response = await client.GetResponseAsync(prompt);
//Console.WriteLine(response);
#endregion

#region Summarization
//var prompt = "Summarize the following text in one sentence: 'Artificial Intelligence (AI) is a branch of computer science that focuses on creating intelligent machines that can perform tasks that typically require human intelligence, such as visual perception, speech recognition, decision-making, and language translation.'";
//Console.WriteLine($"user >>> {prompt}");
//Console.WriteLine($"assistant >>>");
//var response = await client.GetResponseAsync(prompt);
//Console.WriteLine(response);
#endregion

#region Sentiment Analysis
//var prompt = "Analyze the sentiment of the following text: 'I love using AI models, they are amazing and very helpful!'";
//Console.WriteLine($"user >>> {prompt}");
//Console.WriteLine($"assistant >>>");
//var response = await client.GetResponseAsync(prompt);
//Console.WriteLine(response);
#endregion


#region Structured output

//var productListing = new[]
//{
//    "This is an Iphone 17 smartphone, Apple product, it costs $699, and it is available in black and white colors. It made in China and already sold",
//    "This is a Vinfast e34 car, Vin Group, it costs $53699, and it is available in red and brown colors. It made in Vietnam and it's available to sale",
//    "This is a Lenovo laptop, it costs $3699, and it is available in green and blue colors. It made in Vietnam it's available to sale",
//    "This is a LG smart television, LG Group, it costs $2699, and it is available in purple and violet colors. It made in Vietnam it's already sold",
//    "This is a Gingong bike, Toyota, it costs $2899, and it is available in yellow and red colors. It made in China it's available",
//    "This is a comic book, it costs $299, and it is available to edit by KimDong manufacture",
//};

//foreach (var product in productListing)
//{   
//    var prompt = $"""
//        Convert the following product listing into a JSON object matching this C# schema:
//        ProductId: [integer, auto-incremented starting from 1]
//        Name: [product name]
//        Model: [product model]
//        Price: [product price]
//        Description: [exactly 20 worlds to summarize this product]
//        Color: [product color]
//        Status: [available, sold]
//        Here is the product: {product}
//        """;

//    var rawResponse = await client.GetResponseAsync(prompt);

//    var jsTextReponse = rawResponse.ToString();
//    var productResult = System.Text.Json.JsonSerializer.Deserialize<Product>(jsTextReponse);

//    if (productResult != null)
//    {      
//        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(productResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
//    }
//    else
//    {
//        Console.WriteLine("Response was not in the expected format.");
//    }
//}
#endregion