using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;

namespace ImageSimilaritySearch.Utils
{
    public static class ImageSearchUtils
    {
        public record ImageIndexEntry(string Path, string Description, float[] ImageEmbedding, float[] DescriptionEmbedding);

        // Configuration-backed settings
        private static readonly int ResizeSize;
        private static readonly int Grid;

        private record ImageSearchConfig
        {
            public int ImageResize { get; init; } = 64;
            public int SpatialGrid { get; init; } = 4;
        }

        static ImageSearchUtils()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var cfg = new ImageSearchConfig();

            if (File.Exists(configPath))
            {
                var txt = File.ReadAllText(configPath);
                var parsed = JsonSerializer.Deserialize<ImageSearchConfig>(txt);
                if (parsed != null) cfg = parsed;
            }

            ResizeSize = Math.Max(1, cfg.ImageResize);
            Grid = Math.Max(1, cfg.SpatialGrid);
        }

        public static float[] ComputeImageEmbedding(string path)
        {            
            using var img = Image.Load<Rgba32>(path);
            img.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(ResizeSize, ResizeSize), Mode = ResizeMode.Crop }));
            var emb = new List<float>(Grid * Grid * 4); //Each block contributes 4 floats(R, G, B, A normalized to 0..1 by dividing by 255)
            int blockW = Math.Max(1, img.Width / Grid);
            int blockH = Math.Max(1, img.Height / Grid);
            for (int by = 0; by < Grid; by++)
            {
                for (int bx = 0; bx < Grid; bx++)
                {
                    double r = 0, g = 0, b = 0, a = 0;
                    int count = 0;
                    for (int y = by * blockH; y < Math.Min((by + 1) * blockH, img.Height); y++)
                    {
                        for (int x = bx * blockW; x < Math.Min((bx + 1) * blockW, img.Width); x++)
                        {
                            var p = img[x, y];
                            r += p.R;
                            g += p.G;
                            b += p.B;
                            a += p.A;
                            count++;
                        }
                    }
                    if (count == 0) count = 1;
                    emb.Add((float)(r / count / 255.0));
                    emb.Add((float)(g / count / 255.0));
                    emb.Add((float)(b / count / 255.0));
                    emb.Add((float)(a / count / 255.0));
                }
            }

            var arr = emb.ToArray();
            // Normalize image embedding so cosine similarity becomes a simple dot product at query time
            NormalizeInPlace(arr);
            return arr; // length = grid * grid * 4
        }

        public static float[] ComputeTextEmbeddingApprox(string text)
        {
            var dims = Grid * Grid * 4;//Each block contributes 4 floats (R,G,B,A normalized to 0..1 by dividing by 255)
            var emb = new float[dims];
            if (string.IsNullOrWhiteSpace(text)) return emb;

            var words = System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), "\\p{L}+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value);

            foreach (var w in words)
            {
                var h = HashToInt(w);
                var idx = Math.Abs(h) % dims;
                emb[idx] += 1.0f;
            }
            var norm = Math.Sqrt(emb.Select(x => x * x).Sum());
            if (norm > 0)
            {
                for (int i = 0; i < emb.Length; i++) emb[i] /= (float)norm;
            }
            return emb;
        }

        private static void NormalizeInPlace(float[] v)
        {
            if (v == null) return;
            double sum = 0;
            for (int i = 0; i < v.Length; i++) sum += (double)v[i] * v[i];
            if (sum <= 0) return;
            var norm = Math.Sqrt(sum);
            for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norm);
        }

        public static int HashToInt(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            int val = BitConverter.ToInt32(bytes, 0);
            return val;
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length) throw new ArgumentException("Vectors must have the same length", nameof(b));

            double dot = 0; double na = 0; double nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
                na += (double)a[i] * a[i];
                nb += (double)b[i] * b[i];
            }

            // if either vector is zero-length return 0 (no similarity)
            if (na == 0 || nb == 0) return 0f;

            // if both vectors are already normalized (||v||^2 near 1) then cosine = dot
            const double eps = 1e-3;
            if (Math.Abs(na - 1.0) < eps && Math.Abs(nb - 1.0) < eps)
            {
                return (float)dot;
            }
           var cosSim = (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
            return cosSim;
        }

        public static string[] SplitArgs(string line)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            foreach (var c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) parts.Add(sb.ToString());
            return parts.ToArray();
        }
    }
}
