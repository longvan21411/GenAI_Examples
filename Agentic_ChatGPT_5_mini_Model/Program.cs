using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// setup configuration to read API token from user secrets
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = new Uri("https://models.github.ai/inference");

var token = config["GitHubAIModels:Token"];// this should be set in user secrets with the key "GitHubAIModels:Token"
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}
var credential = new ApiKeyCredential(token) ?? throw new InvalidOperationException("invalid token");
//var model = "openai/gpt-5-mini";
var model = "openai/gpt-4o-mini";
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint
};

// create a chat agent client
var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsAIAgent();

// Reuse a single Agent instance for the application's lifetime to avoid extra initialization
var agent = new Agent(client);

while (true)
{
    Console.Write("Goal> ");
    var goal = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(goal))
        break;

    try
    {
        Console.WriteLine("Wait in few minutes to response....\n");
        var result = await agent.CustomRunAsync(goal);
        Console.WriteLine("--- Final agent output ---");
        Console.WriteLine(result);
        Console.WriteLine("--------------------------\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Agent error: {ex.Message}");
    }
}


class Agent( ChatClientAgent chatClient)
{
    private readonly ChatClientAgent _client = chatClient;
    private readonly List<string> _memory = new();

    // Simple in-memory cache to avoid repeating requests for the same goal within TTL.
    private readonly Dictionary<string, (string Response, DateTimeOffset Expiry)> _cache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    // Rate limiting: ensure at least this interval between requests to avoid spamming the server
    private readonly TimeSpan _minIntervalBetweenRequests = TimeSpan.FromSeconds(1);
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _requestLock = new(1, 1);


    public async Task<string> CustomRunAsync(string goal)
    {
        // Check cache first
        if (_cache.TryGetValue(goal, out var entry) && entry.Expiry > DateTimeOffset.UtcNow)
        {
            Console.WriteLine("Returning cached result (within TTL).");
            return entry.Response;
        }

        // Compose a single combined prompt that asks the model to return both a short plan and an execution summary
        var combinedPrompt = $"You are an assistant. Given the user's goal, produce a JSON object with two fields: 'plan' (a numbered list with max 5 steps) and 'execution' (a concise execution summary for each step indicating success or failure).\nGoal: {goal}\nRespond with valid JSON only. Example: {{\"plan\": \"1. ...\\n2. ...\", \"execution\": \"Step 1: ...; Step 2: ...\"}}";

        // Ensure we don't bombard the server: serialize requests and enforce minimal interval
        await _requestLock.WaitAsync();
        try
        {
            var sinceLast = DateTimeOffset.UtcNow - _lastRequest;
            if (sinceLast < _minIntervalBetweenRequests)
            {
                var wait = _minIntervalBetweenRequests - sinceLast;
                await Task.Delay(wait);
            }

            // Execute the single request with retries
            var combinedResponse = await RetryAsync(async () => await _client.RunAsync(combinedPrompt), retries: 3);
            _lastRequest = DateTimeOffset.UtcNow;

            var combinedText = combinedResponse?.Text ?? combinedResponse?.ToString() ?? string.Empty;

            // Try to parse JSON from response; if parsing fails, fall back to separate calls
            string planText = string.Empty;
            string executionText = string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(combinedText);
                var root = doc.RootElement;
                if (root.TryGetProperty("plan", out var planEl))
                    planText = planEl.GetString() ?? string.Empty;
                if (root.TryGetProperty("execution", out var execEl))
                    executionText = execEl.GetString() ?? string.Empty;

                // If JSON parsing yields something reasonable, use it
                if (!string.IsNullOrWhiteSpace(planText) || !string.IsNullOrWhiteSpace(executionText))
                {
                    Console.WriteLine("Plan:\n" + planText);
                    Console.WriteLine("Execution summary:\n" + executionText);

                    // 3) Get feedback from the user
                    Console.WriteLine("Please enter feedback on the execution (or press Enter to skip):");
                    var feedback = Console.ReadLine() ?? string.Empty;

                    var memoryEntry = $"GOAL: {goal}\nPLAN: {planText}\nEXECUTION: {executionText}\nFEEDBACK: {feedback}";
                    _memory.Add(memoryEntry);

                    // store in cache
                    var final = string.IsNullOrWhiteSpace(feedback) ? executionText : feedback;
                    _cache[goal] = (final, DateTimeOffset.UtcNow.Add(_cacheTtl));

                    // If feedback provided, perform a targeted refine request (costs extra request)
                    if (!string.IsNullOrWhiteSpace(feedback))
                    {
                        var refinePrompt = $"The user provided this feedback: {feedback}. Based on the previous plan:\n{planText}\nProduce a refined plan (max 5 steps) that addresses the feedback.";
                        // apply rate limiting again for refine
                        var refineResponse = await RetryAsync(async () => await _client.RunAsync(refinePrompt), retries: 2);
                        _lastRequest = DateTimeOffset.UtcNow;
                        var refinedText = refineResponse?.Text ?? refineResponse?.ToString() ?? string.Empty;
                        Console.WriteLine("Refined plan:\n" + refinedText);
                        _memory.Add($"REFINED_PLAN: {refinedText}");
                        _cache[goal] = (refinedText, DateTimeOffset.UtcNow.Add(_cacheTtl));
                        return refinedText;
                    }

                    return executionText;
                }
            }
            catch (JsonException)
            {
                // parsing failed — we'll fall back to separate calls below
            }

            // Fallback: if combined approach didn't work, call planning and execution separately but still with retries
            var planResp = await RetryAsync(async () => await _client.RunAsync($"You are an assistant that generates a short plan (max 5 steps) to achieve the user's goal.\nGoal: {goal}\nRespond with a numbered list of steps."), retries: 3);
            var plan = planResp?.Text ?? planResp?.ToString() ?? string.Empty;
            Console.WriteLine("Plan:\n" + plan);

            var execResp = await RetryAsync(async () => await _client.RunAsync($"You are an executor. Given the following plan, provide a concise execution summary for each step and indicate success/failure where applicable.\\nPlan:\n{plan}"), retries: 3);
            var execution = execResp?.Text ?? execResp?.ToString() ?? string.Empty;
            Console.WriteLine("Execution summary:\n" + execution);

            Console.WriteLine("Please enter feedback on the execution (or press Enter to skip):");
            var fb = Console.ReadLine() ?? string.Empty;
            var mem = $"GOAL: {goal}\nPLAN: {plan}\nEXECUTION: {execution}\nFEEDBACK: {fb}";
            _memory.Add(mem);
            _cache[goal] = (string.IsNullOrWhiteSpace(fb) ? execution : fb, DateTimeOffset.UtcNow.Add(_cacheTtl));

            if (!string.IsNullOrWhiteSpace(fb))
            {
                var refineResp = await RetryAsync(async () => await _client.RunAsync($"The user provided this feedback: {fb}. Based on the previous plan:\n{plan}\nProduce a refined plan (max 5 steps) that addresses the feedback."), retries: 2);
                _lastRequest = DateTimeOffset.UtcNow;
                var refined = refineResp?.Text ?? refineResp?.ToString() ?? string.Empty;
                Console.WriteLine("Refined plan:\n" + refined);
                _memory.Add($"REFINED_PLAN: {refined}");
                _cache[goal] = (refined, DateTimeOffset.UtcNow.Add(_cacheTtl));
                return refined;
            }

            return execution;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> operation, int retries = 3)
    {
        var delay = 500; // ms
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception) when (attempt < retries)
            {
                await Task.Delay(delay);
                delay *= 2;
            }
        }
    }
}