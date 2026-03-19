using ChatApp_Ollama.Data;
using ChatApp_Ollama.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

// build a DbContext for SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "chat_history.db");
var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var db = new ChatDbContext(optionsBuilder.Options);
// ensure database created
db.Database.EnsureCreated();

Console.WriteLine("--- Ollama Chat (Type 'exit' to quit) ---");

// create or load session
Console.Write("Session Id (leave empty to create new): ");
var sessionIdInput = Console.ReadLine();
var sessionId = string.IsNullOrWhiteSpace(sessionIdInput) ? Guid.NewGuid().ToString() : sessionIdInput.Trim();
Console.WriteLine($"Using SessionId: {sessionId}");

// 2. Keep the conversation history in memory
List<ChatMessage> chatHistory = new();

// helper mapping between MessageRole and Microsoft.Extensions.AI.ChatRole
Microsoft.Extensions.AI.ChatRole ToChatRole(MessageRole role)
{
    return role switch
    {
        MessageRole.user => Microsoft.Extensions.AI.ChatRole.User,
        MessageRole.assistant => Microsoft.Extensions.AI.ChatRole.Assistant,
        _ => Microsoft.Extensions.AI.ChatRole.System,
    };
}

MessageRole FromChatRole(Microsoft.Extensions.AI.ChatRole role)
{
    if (role == Microsoft.Extensions.AI.ChatRole.User)
        return MessageRole.user;
    if (role == Microsoft.Extensions.AI.ChatRole.Assistant)
        return MessageRole.assistant;
    return MessageRole.system;
}

// Load existing history from DB for this session and add to chatHistory
var historyMessages = db.HistoryMessages
    .Where(h => h.SessionId == sessionId)
    .OrderBy(h => h.Timestamp)
    .ToList();

if (historyMessages.Count > 0)
{
    Console.WriteLine("Loaded history messages from local database:");
    foreach (var message in historyMessages)
    {
        Console.WriteLine($"[{message.Timestamp:u}] {message.Role}: {message.Content}");
        // map stored MessageRole to Microsoft.Extensions.AI.ChatRole for ChatMessage
        var parsedRole = ToChatRole(message.Role);
        chatHistory.Add(new ChatMessage(parsedRole, message.Content));
    }
}
else
{
    var system = new ChatMessage(Microsoft.Extensions.AI.ChatRole.System, "You are a master person who can help to answer any questions.");
    chatHistory.Add(system);
    db.HistoryMessages.Add(new HistoryMessage { SessionId = sessionId, Role = MessageRole.system, Content = system.Text, Timestamp = DateTime.UtcNow });
    db.SaveChanges();
}

IChatClient client = new OllamaChatClient(new Uri("http://localhost:11434"));

while (true)
{
    Console.Write("\nYou: ");
    var userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
        break;

    // 3. Add user message to history
    chatHistory.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userInput));
    db.HistoryMessages.Add(new HistoryMessage { SessionId = sessionId, Role = MessageRole.user, Content = userInput, Timestamp = DateTime.UtcNow });
    db.SaveChanges();

    Console.Write("AI: ");

    // 4. Stream the response
    string fullResponse = "";
    try
    {
        var options = new ChatOptions { ModelId = "llama3.2" };
        await foreach (var msg in client.GetStreamingResponseAsync(chatHistory, options))
        {
            Console.Write(msg.Text);
            fullResponse += msg.Text;
        }
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Ollama request failed: {ex.Message}. Check `ollama list` on the server and the model name used in `OllamaChatClient`.");
    }

    // 5. Add AI response to history for next context
    chatHistory.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, fullResponse));
    db.HistoryMessages.Add(new HistoryMessage { SessionId = sessionId, Role = MessageRole.assistant, Content = fullResponse, Timestamp = DateTime.UtcNow });
    db.SaveChanges();
}