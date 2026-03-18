using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatappRAG_Qdrant.Model
{
    public class HistoryMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string SessionId { get; set; } // Required for grouping
        public MessageRole Role { get; set; }      
        public string Content { get; set; }   
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional: Store the metadata about what documents were used
        public string? SourceMetadata { get; set; }
    }

    public enum MessageRole
    {
        User,
        Assistant,
        System
    }
}
