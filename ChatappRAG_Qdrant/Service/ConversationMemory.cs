using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatappRAG_Qdrant.Service
{
    public class ConversationMemory
    {
        private readonly List<string> _messages = new List<string>();
        private readonly int _maxMessages;

        public ConversationMemory(int maxMessages = 10)
        {
            _maxMessages = maxMessages;
        }

        public void AddMessage(string message)
        {
            _messages.Add(message);
            if (_messages.Count > _maxMessages * 2)
            {
                _messages.RemoveAt(0); 
            }
        }

        public IReadOnlyCollection<string> GetMessages()
        {
            return _messages.AsReadOnly();
        }
    }
}
