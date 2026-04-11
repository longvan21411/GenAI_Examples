using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ChatApp_RAG.Services;

public sealed record ImageStoreResult(
    Guid PointId,
    string FileName,
    string RelativePath,
    string PhysicalPath,
    string Description);

public sealed class QdrantImageService
{
    public const string CollectionName = "data-chatapp_rag-images";
    public const string ImageVectorName = "image_embedding";
    public const string DescriptionVectorName = "description_embedding";

    private const int ImageVectorDimensions = 64;
    private const int DescriptionVectorDimensions = 1536;

    private readonly QdrantClient _qdrantClient;
    private readonly EmbeddingService _embeddingService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QdrantImageService> _logger;
    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private bool _collectionReady;

    public QdrantImageService(
        QdrantClient qdrantClient,
        EmbeddingService embeddingService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<QdrantImageService> logger)
    {
        _qdrantClient = qdrantClient;
        _embeddingService = embeddingService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ImageStoreResult> StoreImageAsync(
        string fileName,
        string contentType,
        byte[] content,
        string? description,
        string? category,
        CancellationToken cancellationToken = default)
    {
        if (content is null || content.Length == 0)
        {
            throw new ArgumentException("Image content is empty.", nameof(content));
        }

        await EnsureCollectionAsync(cancellationToken);

        var uploadRoot = _configuration["ChatApp_RAG:ImageUpload:RelativePath"] ?? "uploads/images";
        var safeFileName = Path.GetFileName(fileName);
        var fileStem = Path.GetFileNameWithoutExtension(safeFileName);
        var fileExtension = Path.GetExtension(safeFileName);
        var imageId = Guid.NewGuid();
        var storedFileName = $"{imageId:N}_{fileStem}{fileExtension}";

        var physicalDirectory = Path.Combine(_environment.WebRootPath, uploadRoot);
        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, storedFileName);
        await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

        var relativePath = Path.Combine(uploadRoot, storedFileName).Replace('\\', '/');
        var descriptionText = string.IsNullOrWhiteSpace(description)
            ? string.Join(" ", new[] { fileStem, category }.Where(value => !string.IsNullOrWhiteSpace(value)))
            : description.Trim();

        var imageVector = EnsureVectorSize(ComputeImageEmbedding(content), ImageVectorDimensions);
        var descriptionVector = EnsureVectorSize(
            await _embeddingService.GetEmbeddingAsync(descriptionText).ConfigureAwait(false) ?? [],
            DescriptionVectorDimensions);

        var point = new PointStruct
        {
            Id = imageId,
            Vectors = new Dictionary<string, float[]>
            {
                [ImageVectorName] = imageVector,
                [DescriptionVectorName] = descriptionVector,
            }
        };

        point.Payload["fileName"] = safeFileName;
        point.Payload["storedPath"] = relativePath;
        point.Payload["physicalPath"] = physicalPath;
        point.Payload["contentType"] = contentType;
        point.Payload["description"] = descriptionText;
        point.Payload["category"] = category ?? string.Empty;
        point.Payload["createdAt"] = DateTimeOffset.UtcNow.ToString("O");

        await _qdrantClient.UpsertAsync(CollectionName, [point], wait: true, cancellationToken: cancellationToken);

        _logger.LogInformation("Stored image {FileName} in {PhysicalPath} and upserted Qdrant point {PointId}.", safeFileName, physicalPath, imageId);

        return new ImageStoreResult(imageId, safeFileName, relativePath, physicalPath, descriptionText);
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionReady)
        {
            return;
        }

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionReady)
            {
                return;
            }

            try
            {
                await _qdrantClient.GetCollectionInfoAsync(CollectionName, cancellationToken);
                _collectionReady = true;
                return;
            }
            catch
            {
                // Collection will be created below.
            }

            var vectorsConfig = new VectorParamsMap
            {
                Map =
                {
                    [ImageVectorName] = new VectorParams { Size = ImageVectorDimensions, Distance = Distance.Cosine },
                    [DescriptionVectorName] = new VectorParams { Size = DescriptionVectorDimensions, Distance = Distance.Cosine },
                }
            };

            await _qdrantClient.CreateCollectionAsync(
                collectionName: CollectionName,
                vectorsConfig: vectorsConfig,
                cancellationToken: cancellationToken);

            _collectionReady = true;
            _logger.LogInformation("Created Qdrant image collection {CollectionName}.", CollectionName);
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    private static float[] ComputeImageEmbedding(byte[] content)
    {
        var vector = new float[ImageVectorDimensions];

        if (content.Length == 0)
        {
            return vector;
        }

        for (var index = 0; index < content.Length; index++)
        {
            vector[index % vector.Length] += content[index];
        }

        NormalizeInPlace(vector);
        return vector;
    }

    private static float[] EnsureVectorSize(float[] vector, int size)
    {
        if (vector.Length == size)
        {
            return vector;
        }

        var resized = new float[size];
        Array.Copy(vector, resized, Math.Min(vector.Length, size));
        NormalizeInPlace(resized);
        return resized;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        double sum = 0;
        foreach (var value in vector)
        {
            sum += value * value;
        }

        if (sum <= 0)
        {
            return;
        }

        var norm = Math.Sqrt(sum);
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= (float)norm;
        }
    }
}
