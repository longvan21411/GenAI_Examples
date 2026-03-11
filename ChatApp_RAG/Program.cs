using Microsoft.Extensions.AI;
using OpenAI;
using ChatApp_RAG.Components;
using ChatApp_RAG.Services;
using ChatApp_RAG.Services.Ingestion;
using Microsoft.Extensions.DataIngestion;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Include user secrets in the application's configuration so Serilog (and other components) can read them.
builder.Configuration.AddUserSecrets<Program>();

// Configure Serilog from configuration and default sinks
var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logsDirectory);
var logFilePath = Path.Combine(logsDirectory, "log-.log");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // Write rolling daily logs to Logs/log-YYYYMMDD.log, keep 14 files
    .WriteTo.File(
        path: logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();

IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}

var credential = new System.ClientModel.ApiKeyCredential(token);
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri("https://models.inference.ai.azure.com")
};

var ghModelsClient = new OpenAIClient(credential, openAIOptions);
var chatClient = ghModelsClient.GetChatClient("gpt-4o-mini").AsIChatClient();

// Get the raw embedding client and also the typed IEmbeddingGenerator for DI consumers
var rawEmbeddingClient = ghModelsClient.GetEmbeddingClient("text-embedding-3-small");
var embeddingGenerator = rawEmbeddingClient.AsIEmbeddingGenerator();

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

// Register EmbeddingService with the raw client so it can call GenerateEmbeddingsAsync like EmbeddingGenaration project
builder.Services.AddSingleton(sp => new EmbeddingService(rawEmbeddingClient));

// Register Qdrant chat store and HttpClient. QDRANT_BASE_URL should be set in configuration (e.g. user secrets) to http://localhost:6333
var qdrantBase = builder.Configuration["Qdrant:BaseUrl"] ?? "http://localhost:6333";
if (!qdrantBase.EndsWith("/"))
{
    qdrantBase += "/";
}

// Ensure the QdrantChatStore constructor receives HttpClient and the embedding func
builder.Services.AddHttpClient<QdrantChatStore>(client =>
{
    client.BaseAddress = new Uri(qdrantBase);
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

// Use a try/catch to log startup exceptions and ensure Serilog flushes on exit.
try
{
    var app = builder.Build();

    // Ensure Qdrant collection exists on startup
    using (var scope = app.Services.CreateScope())
    {
        var qdrant = scope.ServiceProvider.GetRequiredService<QdrantChatStore>();
        await qdrant.EnsureCollectionExistsAsync();
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.UseStaticFiles();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("Starting web host");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
