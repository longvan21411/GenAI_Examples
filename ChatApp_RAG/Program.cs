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

// Log masked token/endpoint to help diagnose 401 without leaking secrets
try
{
    var maskedToken = token?.Length > 8 ? token.Substring(0, 4) + new string('*', Math.Max(0, token.Length - 8)) + token.Substring(token.Length - 4) : token;
    Log.Information("Using model endpoint {Endpoint} and token {TokenPreview}", endpointUri, maskedToken);
}
catch { }

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
var qdrantHost = builder.Configuration["ChatApp_RAG:VectorDatabase:QdrantDatabase:Host"] ?? "localhost";
var qdrantPortString = builder.Configuration["ChatApp_RAG:VectorDatabase:QdrantDatabase:GrpcAPIPort"];
if (!int.TryParse(qdrantPortString, out var qdrantPort))
{
    qdrantPort = 6334;
}

builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
builder.Services.AddSingleton<QdrantImageService>();

if (string.Equals(activeDb, "QdrantDatabase", StringComparison.OrdinalIgnoreCase))
{
    // If you intend to use Qdrant you must register a Qdrant-backed VectorStore here.
    // The previous implementation did not register the VectorStore services for Qdrant,
    // which causes DI resolution failures for DataIngestor and SemanticSearch.
    //
    // Two options:
    // 1) Register Qdrant-backed vector store services here (recommended if Qdrant is required).
    //    Example (pseudo-code, replace with your project's Qdrant integration):
    //      builder.Services.AddQdrantVectorStore(qdrantClientOrConnectionString);
    //      builder.Services.AddQdrantCollection<string, IngestedChunk>(IngestedChunk.CollectionName, qdrantOptions);
    //
    // 2) Fall back to the local SQLite vector store if Qdrant registration is not available.
    //    The code below falls back to the LocalDatabase registration to avoid DI errors.
    //
    // If you prefer strict behavior, replace the fallback with an exception that instructs how to register Qdrant.
    // NOTE: The project previously did not register Qdrant-backed VectorStore types.
    // If you have extension methods to register Qdrant vector stores, call them here.
    // As a safe fallback, register the sqlite-based services so DI consumers resolve.
    var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
    var fallbackConnectionString = $"Data Source={vectorStorePath}";

    builder.Services.AddSqliteVectorStore(_ => fallbackConnectionString);
    builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, fallbackConnectionString);
}
else
{
    var vectorStoreConnectionString = builder.Configuration["ChatApp_RAG:VectorDatabase:LocalDatabase:ConnectionString"];
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
