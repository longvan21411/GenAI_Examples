using Serilog;
using Qdrant.Client;
using Qdrant.Client.Grpc;

// Simple Qdrant Console app to create collections and upsert vectors.

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/qdrant-console-.log", rollingInterval: Serilog.RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting Qdrant console app");

const string desiredCollection = "demo_01_custom"; 

var client = new QdrantClient("localhost", 6334);

var points = new List<PointStruct>();

var p1 = new PointStruct { Id = 1, Vectors = new[] { 0.9f, 0.1f, 0.1f, 0.2f } };
p1.Payload["city"] = "red";
points.Add(p1);

var p2 = new PointStruct { Id = 2, Vectors = new[] { 0.1f, 0.9f, 0.1f, 0.1f } };
p2.Payload["city"] = "green";
points.Add(p2);

var p3 = new PointStruct { Id = 3, Vectors = new[] { 0.1f, 0.1f, 0.9f, 0.2f } };
p3.Payload["city"] = "blue";
points.Add(p3);

try {
    await client.UpsertAsync( collectionName: desiredCollection,  points: points);
}
catch(Exception ex)
{
    Log.Error(ex.Message, "Error upserting points to Qdrant");
}





