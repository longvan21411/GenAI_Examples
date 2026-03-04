using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using ChatApp_RAG.Components;
using ChatApp_RAG.Services;
using ChatApp_RAG.Services.Ingestion;
using Microsoft.Extensions.DataIngestion;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
var token = config["GitHubAIModels:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("API token 'GitHubModels:Token' is missing or empty.");
}

var credential = new ApiKeyCredential(token);
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri("https://models.inference.ai.azure.com")
};

var ghModelsClient = new OpenAIClient(credential, openAIOptions);
var chatClient = ghModelsClient.GetChatClient("gpt-4o-mini").AsIChatClient();
var embeddingGenerator = ghModelsClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

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


app.Run();
