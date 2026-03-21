using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using ImageSimilaritySearch.Utils;

// Helper container for types and functions

public class Program
{
    public static void Main(string[] args)
    {
        var imageDir = Path.Combine(AppContext.BaseDirectory, "imgs/cat");
        Directory.CreateDirectory(imageDir);
        var indexFile = Path.Combine(AppContext.BaseDirectory, "image_index.json");

        var index = File.Exists(indexFile)
            ? JsonSerializer.Deserialize<List<ImageSearchUtils.ImageIndexEntry>>(File.ReadAllText(indexFile)) ?? new()
            : new List<ImageSearchUtils.ImageIndexEntry>();

        Console.WriteLine("Image Similarity Search (text -> image). Commands: index <path> <description>, search <query>, exit");

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Trim().ToLower() == "exit") break;

            var parts = ImageSearchUtils.SplitArgs(line);
            if (parts.Length == 0) continue;
                        
            if (parts[0].ToLower() == "index" && parts.Length >= 3)
            {
                var path = parts[1];
                var desc = string.Join(' ', parts.Skip(2));
                if (!File.Exists(path))
                {
                    Console.WriteLine("File not found: " + path);
                    continue;
                }

                var dest = Path.Combine(imageDir, Path.GetFileName(path));
                File.Copy(path, dest, true);
                var imgEmbedding = ImageSearchUtils.ComputeImageEmbedding(dest);
                var descrEmbedding = ImageSearchUtils.ComputeTextEmbeddingApprox(desc);
                var entry = new ImageSearchUtils.ImageIndexEntry(dest, desc, imgEmbedding, descrEmbedding);
                index.Add(entry);
                File.WriteAllText(indexFile, JsonSerializer.Serialize(index));
                Console.WriteLine("Indexed: " + dest);
            }
            else if (parts[0].ToLower() == "search" && parts.Length >= 2)
            {
                var query = string.Join(' ', parts.Skip(1));
                var qEmb = ImageSearchUtils.ComputeTextEmbeddingApprox(query);
                var results = index
                    .Select(e => new { Entry = e, Score = ImageSearchUtils.CosineSimilarity(e.DescriptionEmbedding, qEmb) })
                    .OrderByDescending(x => x.Score)
                    .Take(5)
                    .ToList();

                Console.WriteLine("Top results:");
                foreach (var r in results)
                {
                    Console.WriteLine($"Score: {r.Score:F4} - {r.Entry.Path} - {r.Entry.Description}");
                }
            }
            else
            {
                Console.WriteLine("Unknown command");
            }
        }
    }
}
