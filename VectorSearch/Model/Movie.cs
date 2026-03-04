using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;
namespace VectorSearch.Model
{
    //Vector data entity class for movie, which will be stored in vector database
    public class Movie
    {
        [VectorStoreKey]
        public int Key { get; set; }
        [VectorStoreData]
        public string Title { get; set; } = string.Empty;
        [VectorStoreData]
        public string Description { get; set; }=string.Empty;

        [VectorStoreVector(Dimensions: 384, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    public static class MovieData
    {
        //populate some movie data for testing
        public static List<Movie> GetMovies()
        {
            return new List<Movie>
            {
                new Movie { Key = 0, Title = "Lion King", Description = "An animated movie which describle about the journey of kid lion to become a king"},
                new Movie { Key = 1, Title = "The Matrix", Description = "A computer hacker learns about the true nature of his reality and his role in the war against its controllers." },
                new Movie { Key = 2, Title = "Inception", Description = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a CEO." },
                new Movie { Key = 3, Title = "Interstellar", Description = "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival." }
            };
        }
    }
}
