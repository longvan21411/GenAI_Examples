using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

namespace ChatappRAG_Qdrant.Model
{
    public class Movie
    {
        public const int VectorDemension=1536;
        public const string CollectionName = "RAG_Movie";
        public const string VectorDistance = DistanceFunction.CosineDistance;       

        [VectorStoreKey(StorageName = "id")]
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [VectorStoreData(StorageName = "title")]
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "year")]
        [JsonPropertyName("year")]
        public string Year { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "genres")]
        [JsonPropertyName("genres")]
        public string[] Genres { get; set; } = Array.Empty<string>();

        [VectorStoreData(StorageName = "director")]
        [JsonPropertyName("director")]
        public string Director { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "actors")]
        [JsonPropertyName("actors")]
        public string Actors { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "plot")]
        [JsonPropertyName("plot")]
        public string Plot { get; set; } = string.Empty;

        [VectorStoreData(StorageName = "posterUrl")]
        [JsonPropertyName("posterUrl")]
        public string PosterUrl { get; set; } = string.Empty;

        [VectorStoreVector(VectorDemension, DistanceFunction = VectorDistance, StorageName = "embedding")]
        [JsonPropertyName("embedding")]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
