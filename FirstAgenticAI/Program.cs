using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");

var credential = new AzureKeyCredential(token);

// implement an agentic AI that can plan, execute actions, and learn from feedback using the openai/gpt-5-mini model


// This file implements a small, extensible agent scaffold. It uses a simple HTTP-based model client
// that forwards prompts to a model inference endpoint. The code is purposely minimal and focuses
// on the plan -> execute -> learn loop. The actual payload shape for the remote model may vary by
// provider; adjust the `HttpModelClient` accordingly to match the API your endpoint expects.

var endpoint = config["GitHubAIModels:Endpoint"] ?? "https://models.github.ai/inference";
using var http = new HttpClient { BaseAddress = new Uri(endpoint) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

IModelClient modelClient = new HttpModelClient(http, "openai/gpt-5-mini");
var agent = new Agent(modelClient);

Console.WriteLine("Agentic AI ready. Enter a high-level goal (empty to exit):");
while (true)
{
    Console.Write("Goal> ");
    var goal = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(goal))
        break;

    try
    {
        var result = await agent.RunAsync(goal);
        Console.WriteLine("--- Final agent output ---");
        Console.WriteLine(result);
        Console.WriteLine("--------------------------\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Agent error: {ex.Message}");
    }
}

// ----------------- Supporting types -----------------

interface IModelClient
{
    Task<string> GenerateAsync(string prompt, int maxTokens = 256);
}

// A very small HTTP model client that POSTs a JSON payload to the inference endpoint.
// Note: Adjust the request body/response parsing to match your inference service API.
class HttpModelClient : IModelClient
{
    private readonly HttpClient _http;
    private readonly string _modelName;

    public HttpModelClient(HttpClient http, string modelName)
    {
        _http = http;
        _modelName = modelName;
    }

    public async Task<string> GenerateAsync(string prompt, int maxTokens = 256)
    {
        var payload = new
        {
            model = _modelName,
            input = prompt,
            max_tokens = maxTokens
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var res = await _http.PostAsync(_http.BaseAddress, content);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            // Return the raw body for easier debugging but also throw so caller can decide what to do.
            throw new InvalidOperationException($"Model request failed ({res.StatusCode}): {body}");
        }

        // The exact response shape depends on the provider. If the service returns plain text we
        // simply return it. If JSON, attempt to extract a reasonable text field.
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Common possibilities: { "output": "text..." } or OpenAI-like { "choices": [{ "text": "..." }] }
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("output", out var outEl) && outEl.ValueKind == JsonValueKind.String)
                    return outEl.GetString() ?? string.Empty;

                if (root.TryGetProperty("choices", out var choicesEl) && choicesEl.ValueKind == JsonValueKind.Array && choicesEl.GetArrayLength() > 0)
                {
                    var first = choicesEl[0];
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                        return textEl.GetString() ?? string.Empty;
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("message", out var msgEl) && msgEl.TryGetProperty("content", out var contEl) && contEl.ValueKind == JsonValueKind.String)
                        return contEl.GetString() ?? string.Empty;
                }

                // Fallback: return the raw JSON string
                return body;
            }

            // If response is plain string or array, return raw
            return body;
        }
        catch
        {
            // Not JSON — return body as-is
            return body;
        }
    }
}

class Agent
{
    private readonly IModelClient _modelClient;
    private readonly List<string> _memory = new();

    public Agent(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    public async Task<string> RunAsync(string goal)
    {
        // 1) Planning
        var planPrompt = $"You are an assistant that generates a short plan (max 5 steps) to achieve the user's goal.\nGoal: {goal}\nRespond with a numbered list of steps.";
        var plan = await _modelClient.GenerateAsync(planPrompt, maxTokens: 200);
        Console.WriteLine("Plan:\n" + plan);

        // 2) Execute - for demonstration we ask the model to simulate executing the plan
        var execPrompt = $"You are an executor. Given the following plan, provide a concise execution summary for each step and indicate success/failure where applicable.\\nPlan:\n{plan}";
        var execution = await _modelClient.GenerateAsync(execPrompt, maxTokens: 400);
        Console.WriteLine("Execution summary:\n" + execution);

        // 3) Get feedback from the user
        Console.WriteLine("Please enter feedback on the execution (or press Enter to skip):");
        var feedback = Console.ReadLine() ?? string.Empty;

        // 4) Learn - simple memory: store the triplet (goal, plan, execution) plus feedback
        var memoryEntry = $"GOAL: {goal}\nPLAN: {plan}\nEXECUTION: {execution}\nFEEDBACK: {feedback}";
        _memory.Add(memoryEntry);

        // Optionally ask the model to refine the plan based on feedback
        if (!string.IsNullOrWhiteSpace(feedback))
        {
            var refinePrompt = $"The user provided this feedback: {feedback}. Based on the previous plan:\n{plan}\nProduce a refined plan (max 5 steps) that addresses the feedback.";
            var refinedPlan = await _modelClient.GenerateAsync(refinePrompt, maxTokens: 200);
            Console.WriteLine("Refined plan:\n" + refinedPlan);

            // store refined plan
            _memory.Add($"REFINED_PLAN: {refinedPlan}");
            return refinedPlan;
        }

        return execution;
    }
}
