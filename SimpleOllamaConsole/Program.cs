using System.Net.Http.Json;
using System.Text.Json;

var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";

// Reuse HttpClient for the life of the app
using var http = new HttpClient { BaseAddress = new Uri(ollamaBaseUrl) };

Console.Write("Prompt: ");
var prompt = Console.ReadLine() ?? "Hello!";

// Non-streaming request (simple)
var request = new
{
    model = "llama3.2",
    prompt = prompt,
    stream = false
};

using var resp = await http.PostAsJsonAsync("/api/generate", request);
resp.EnsureSuccessStatusCode();

using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
var text = doc.RootElement.GetProperty("response").GetString();

Console.WriteLine("\n---\nModel response:\n");
Console.WriteLine(text);