using ChatappRAG_Qdrant.Model;
using OpenAI.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Serilog;
using System.Text.Json;

namespace ChatappRAG_Qdrant.Service
{
    public static class QdrantService
    {
        public static IList<PointStruct> ExecuteCeedData(QdrantClient qdrantClient, EmbeddingClient embeddingGenerator, string jsonPath) {
            var collectionName = Movie.CollectionName;
            var collections = qdrantClient.ListCollectionsAsync().Result;
            var collectionExists = collections.Contains(collectionName);
            if (!collectionExists)
            {
                qdrantClient.CreateCollectionAsync(collectionName, new VectorParams { Size = 1536, Distance = Distance.Cosine }).Wait();
                var createdCollection = qdrantClient.GetCollectionInfoAsync(collectionName).Result;
                if (createdCollection != null)
                {
                    // Load movie data JSON                   
                    if (!File.Exists(jsonPath))
                    {
                        Log.Error("Movie data file not found at {Path}", jsonPath);
                        return [];
                    }

                    using var stream = File.OpenRead(jsonPath);
                    using var doc = JsonDocument.ParseAsync(stream).Result;
                    if (!doc.RootElement.TryGetProperty("movies", out var moviesEl) || moviesEl.ValueKind != JsonValueKind.Array)
                    {
                        Log.Error("Movie data JSON does not contain a 'movies' array.");
                        return [];
                    }

                    var restPoints = new List<object>();
                    var points = new List<PointStruct>();
                    long countItem = 0;
                    foreach (var item in moviesEl.EnumerateArray())
                    {
                        try
                        {
                            var id = item.GetProperty("id").GetInt32();
                            var title = item.GetProperty("title").GetString() ?? string.Empty;
                            var year = item.GetProperty("year").GetString() ?? string.Empty;
                            var genres = item.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
                                ? g.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                                : Array.Empty<string>();
                            var director = item.TryGetProperty("director", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                            var actors = item.TryGetProperty("actors", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                            var plot = item.TryGetProperty("plot", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                            var poster = item.TryGetProperty("posterUrl", out var pu) ? pu.GetString() ?? string.Empty : string.Empty;

                            var textEmbedding = string.Concat("Title: ", title,
                                " Year ", year,
                                " Genres ", string.Join(", ", genres),
                                " Director ", director,
                                " Actors ", actors,
                                " Plot ", plot);

                            // Generate embedding
                            Console.Write("Getting the embedding data... ");
                            Console.Write("\n");
                            var movieEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { textEmbedding }).Result;
                            var movieVector = movieEmbedding.Value[0].ToFloats().Span;
                            Console.Write("Embedding data retrieved.");
                            Console.Write("\n");

                            countItem++;
                            var point = new PointStruct
                            {
                                Id = new PointId { Num = (ulong)countItem },
                                Vectors = new Vectors { Vector = new Qdrant.Client.Grpc.Vector { Data = { movieVector.ToArray() } } }
                            };

                            // Add payload entries individually to the read-only MapField
                           var payload = string.Concat("\n Title: ", title,
                            "\n Year: ", year,
                                "\n Genres: ", string.Join(", ", genres),
                                "\n Director: ", director,
                                "\n Actors: ", actors,
                                "\n Plot: ", plot);
                            point.Payload.Add("movie", payload);                           

                            points.Add(point);
                            Console.Write("Waiting to fetch the vector data..... ");
                            Console.Write("\n");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed processing a movie element");
                        }
                    }

                    var upsertResponse = qdrantClient.UpsertAsync(collectionName: collectionName, points: points).Result;
                    if (upsertResponse != null)
                    {
                        Console.Write($"Done processing the ceed data succesfully... {points.Count} points were inserted!!!");
                        Console.Write("\n");
                        Log.Information("Upsert response: {Response}", JsonSerializer.Serialize(upsertResponse));

                        // Verification: do a quick search using the first inserted vector
                        if (points.Count > 0)
                        {
                            var queryVector = points[0].Vectors.Vector.Data.ToArray();
                            var searchResults = qdrantClient.SearchAsync(collectionName: collectionName, vector: queryVector, limit: 5).Result;
                            if (searchResults != null && searchResults.Count > 0)
                            {
                                Log.Information("Verification search found {Count} points. Top Id={Id}", searchResults.Count, searchResults[0].Id);
                                return points;
                            }                           
                        }
                    }

                    Log.Information("Finished upserting points to Qdrant with vector size = 1536");
                }
            }
            return new List<PointStruct>();   
        } 
    

    }
}
