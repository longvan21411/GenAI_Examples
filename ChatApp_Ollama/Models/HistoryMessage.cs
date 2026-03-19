using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp_Ollama.Models
{
    public enum MessageRole
    {
        user,
        assistant,
        system
    }
    public class HistoryMessage
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = "";
        public MessageRole Role { get; set; } = MessageRole.system;   
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
