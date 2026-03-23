using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ImageEmbedding_CSharp_OpenSource.Ultils;

var isCLIPModelActive = false;
var imgIndexPath = "imgs\\cat";
var imgIndexStorePath = "image_index.json";
var config = ImageSearchUtils.GetImageSearchConfigInfo();
if(config != null)
{
    bool.TryParse(config.ActiveCLIPModel,out bool isCLIPActive);
    if (isCLIPActive)
    {
        imgIndexPath = "imgs\\cat_CLIP";
        imgIndexStorePath = "image_index_CLIP.json";
        isCLIPModelActive = true;
    }
}

var imageDir = Path.Combine(AppContext.BaseDirectory, imgIndexPath);
Directory.CreateDirectory(imageDir);
var indexFile = Path.Combine(AppContext.BaseDirectory, imgIndexStorePath);

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
        var imgEmbedding = isCLIPModelActive ? ImageSearchUtils.GetCLIPImageEmbedding(dest): ImageSearchUtils.ComputeImageEmbedding(dest);
        var descrEmbedding = isCLIPModelActive ? ImageSearchUtils.GetCLIPTextEmbedding(desc): ImageSearchUtils.ComputeTextEmbeddingApprox(desc);
        var entry = new ImageSearchUtils.ImageIndexEntry(dest, desc, imgEmbedding, descrEmbedding);
        index.Add(entry);
        File.WriteAllText(indexFile, JsonSerializer.Serialize(index));
        Console.WriteLine("Indexed: " + dest);
    }
    else if (parts[0].ToLower() == "search" && parts.Length >= 2)
    {
        var query = string.Join(' ', parts.Skip(1));
        
        if (query.EndsWith(".jpg") || query.EndsWith(".png"))
        {
            //search by image embedding similarity
            var qEmb = isCLIPModelActive ? ImageSearchUtils.GetCLIPImageEmbedding(query): ImageSearchUtils.ComputeImageEmbedding(query);
            var imgResults = index
            .Select(e => new { Entry = e, Score = isCLIPModelActive ? ImageSearchUtils.CLIPCosineSimilarity(e.ImageEmbedding, qEmb) : ImageSearchUtils.CosineSimilarity(e.ImageEmbedding, qEmb) })
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

            Console.WriteLine("Top results:");
            foreach (var r in imgResults)
            {
                Console.WriteLine($"Score: {r.Score:F4} - {r.Entry.Path} - {r.Entry.Description}");
            }
        }
        else
        {
            //search by text embedding similarity
            var qEmb = ImageSearchUtils.GetCLIPTextEmbedding(query);//GetCLIPTextEmbedding(query);//ComputeTextEmbeddingApprox(query);
            var imgResults = index
            .Select(e => new { Entry = e, Score = isCLIPModelActive ? ImageSearchUtils.CLIPCosineSimilarity(e.DescriptionEmbedding, qEmb) : ImageSearchUtils.CosineSimilarity(e.DescriptionEmbedding, qEmb) })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();

            Console.WriteLine("Top results:");
            foreach (var r in imgResults)
            {
                Console.WriteLine($"Score: {r.Score:F4} - {r.Entry.Path} - {r.Entry.Description}");
            }
        }            
    }
    else
    {
        Console.WriteLine("Unknown command");
    }
}
