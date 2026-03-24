using ChatApp_RAG.Services.Ingestion;
using Microsoft.Extensions.VectorData;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace ChatApp_RAG.Services;

public class SemanticSearch(
    VectorStoreCollection<string, IngestedChunk> vectorCollection,
    [FromKeyedServices("ingestion_directory")] DirectoryInfo ingestionDirectory,
    DataIngestor dataIngestor)
{
    private Task? _ingestionTask;

    public async Task LoadDocumentsAsync() => await ( _ingestionTask ??= dataIngestor.IngestDataAsync(ingestionDirectory, searchPattern: "*.*"));

    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(string text, string? documentIdFilter, int maxResults)
    {
        // Ensure documents have been loaded before searching
        await LoadDocumentsAsync();

        var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
        {
            Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
        });

        return await nearest.Select(result => result.Record).ToListAsync();
    }

    /// <summary>
    /// Returns true when at least one relevant document exists in the vector store for <paramref name="text"/>.
    /// Used to decide whether to perform retrieval (RAG) or send the question directly to the model.
    /// </summary>
    public async Task<bool> HasRelevantDocumentsAsync(string text, string? documentIdFilter, int maxResults = 1)
    {
        try
        {
            await LoadDocumentsAsync();

            var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
            {
                Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
            });

            return await nearest.AnyAsync();
        } catch (Exception ex)
        {
            // Use Serilog to capture full exception details. Include guidance for common 401 issues.
            Log.Error(ex, "Error during HasRelevantDocumentsAsync. If this is an HTTP 401 (Unauthorized), verify your model provider credentials and endpoint configuration (appsettings/user-secrets). Exception: {Message}", ex.Message);
            return false; 
        }       
    }
}
