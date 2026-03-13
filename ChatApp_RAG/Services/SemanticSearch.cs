using ChatApp_RAG.Services.Ingestion;
using Microsoft.Extensions.VectorData;
using System.Linq;
using System.Threading.Tasks;

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
        await LoadDocumentsAsync();

        var nearest = vectorCollection.SearchAsync(text, maxResults, new VectorSearchOptions<IngestedChunk>
        {
            Filter = documentIdFilter is { Length: > 0 } ? record => record.DocumentId == documentIdFilter : null,
        });

        return await nearest.AnyAsync();
    }
}
