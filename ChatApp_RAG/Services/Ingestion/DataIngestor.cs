using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.Logging;

namespace ChatApp_RAG.Services.Ingestion;

public class DataIngestor(
    ILogger<DataIngestor> logger,
    ILoggerFactory loggerFactory,
    VectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public async Task IngestDataAsync(DirectoryInfo directory, string searchPattern)
    {
        using var writer = new VectorStoreWriter<string>(vectorStore, dimensionCount: IngestedChunk.VectorDimensions, new()
        {
            CollectionName = IngestedChunk.CollectionName,
            DistanceFunction = IngestedChunk.VectorDistanceFunction,
            IncrementalIngestion = false,
        });

        // Create tokenizer and chunker so we can log their configuration
        var tokenizerModel = "gpt-4o";
        var tokenizer = TiktokenTokenizer.CreateForModel(tokenizerModel);
        var chunker = new SemanticSimilarityChunker(embeddingGenerator, new(tokenizer));

        logger.LogInformation("Initializing ingestion pipeline. ChunkerType={ChunkerType}, TokenizerModel={TokenizerModel}",
            chunker.GetType().FullName, tokenizerModel);

        using var pipeline = new IngestionPipeline<string>(
            reader: new DocumentReader(directory),
            chunker: chunker,
            writer: writer,
            loggerFactory: loggerFactory);

        await foreach (var result in pipeline.ProcessAsync(directory, searchPattern))
        {
            logger.LogInformation("Completed processing '{id}'. Succeeded: '{succeeded}'.", result.DocumentId, result.Succeeded);
        }
    }
}
