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
using Qdrant.Client;

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

// Read configuration values from appsettings.json (and user secrets)
var token = builder.Configuration["GitHubAIModels:Token"] ?? builder.Configuration["ChatApp_RAG:GitHubModel:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubAIModels:Token' is missing or empty.");
}

var endpointString = builder.Configuration["ChatApp_RAG:GitHubModel:Endpoint"] ?? "https://models.inference.ai.azure.com";
if (!Uri.TryCreate(endpointString, UriKind.Absolute, out var endpointUri))
{
    throw new InvalidOperationException($"Invalid endpoint URI: {endpointString}");
}

var llmModel = builder.Configuration["ChatApp_RAG:GitHubModel:LLMModel"] ?? "gpt-4o-mini";
var embeddingModel = builder.Configuration["ChatApp_RAG:GitHubModel:EmbeddingModel"] ?? "text-embedding-3-small";
var vectorSize = int.TryParse(builder.Configuration["ChatApp_RAG:GitHubModel:VectorSize"], out var vs) ? vs : 1536;
var maxToken = int.TryParse(builder.Configuration["ChatApp_RAG:GitHubModel:MaxToken"], out var mt) ? mt : 4096;

var credential = new System.ClientModel.ApiKeyCredential(token);
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpointUri
};

var ghModelsClient = new OpenAIClient(credential, openAIOptions);
var chatClient = ghModelsClient.GetChatClient(llmModel).AsIChatClient();

// Get the raw embedding client and also the typed IEmbeddingGenerator for DI consumers
var rawEmbeddingClient = ghModelsClient.GetEmbeddingClient(embeddingModel);
var embeddingGenerator = rawEmbeddingClient.AsIEmbeddingGenerator();

// Configure vector store based on appsettings. Support LocalDatabase (SQLite) by default.
var activeDb = builder.Configuration["ChatApp_RAG:VectorDatabase:ActiveVectorDatabase"] ?? "LocalDatabase";
string vectorStoreConnectionString;
if (string.Equals(activeDb, "QdrantDatabase", StringComparison.OrdinalIgnoreCase))
{
    var client = new QdrantClient("localhost", 6334);
    //builder.Services.Add<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);
}
else
{
    vectorStoreConnectionString = builder.Configuration["ChatApp_RAG:VectorDatabase:LocalDatabase:ConnectionString"];
    if (string.IsNullOrWhiteSpace(vectorStoreConnectionString))
    {
        var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
        vectorStoreConnectionString = $"Data Source={vectorStorePath}";
    }

    builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
    builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);
}

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

// Register EmbeddingService with the raw client so it can call GenerateEmbeddingsAsync like EmbeddingGenaration project
builder.Services.AddSingleton(sp => new EmbeddingService(rawEmbeddingClient));

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
