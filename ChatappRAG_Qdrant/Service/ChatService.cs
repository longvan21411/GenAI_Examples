using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using System;
using System.ClientModel;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ChatappRAG_Qdrant.Service
{
    public static class ChatService
    {
        public static async Task ChatAsync(
            ApiKeyCredential credential, 
            OpenAIClientOptions openAIOptions,
            QdrantClient qdrantClient,
            EmbeddingClient embeddingGenerator,
            string collectionName
            )
        {
            // create a chat client
            var model = "openai/gpt-5-mini";
            var client = new OpenAIClient(credential, openAIOptions).GetChatClient(model).AsIChatClient();
            var systemMessage = new ChatMessage(ChatRole.System, content: "You are a helpful assistant specialized in moive knowledge");
            var history = new ConversationMemory();
            while (true)
            {
                Console.Write("\nYour question: ");
                var query = Console.ReadLine();

                if (query == null)
                    break;

                if (query.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Exiting the application. Goodbye!");
                    break;
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    Console.WriteLine("Please enter a prompt or type 'exit' to quit.");
                    continue;
                }

                if (query.Trim().Equals("chat_history", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("=================================================== ");
                    Console.Write("\nChat history: ");
                    foreach (var chatHistory in history.GetMessages())
                    {
                        Console.Write("\n");
                        Console.Write(chatHistory);
                    }
                    Console.Write("\n=================================================== ");
                    break;
                }

                var queryEmbedding = embeddingGenerator.GenerateEmbeddingsAsync(new[] { query }).Result;
                var queryVector = queryEmbedding.Value[0].ToFloats().Span;
                var results =  qdrantClient.SearchAsync(collectionName: collectionName,
                    vector: queryVector.ToArray(),
                    limit: 3
                    ).Result;

                var searchResult = new HashSet<string>();
                StringBuilder movieInfo = new StringBuilder();
                foreach (var movie in results)
                {
                    if (movie.Payload != null)
                    {
                        movie.Payload.TryGetValue("movie", out var title);
                        movieInfo.Append("\n");
                        movieInfo.Append("movie infomation: " + title?.StringValue ?? string.Empty);
                        movieInfo.AppendLine("\n==============================================");
                        movieInfo.AppendLine();
                    }
                    //Log.Information("Point Id={Id} Movie information ={info}", movie.Id, movieInfo.ToString());
                }

                var context = string.Join(Environment.NewLine, searchResult);
                var previousMessage = string.Join(Environment.NewLine, history.GetMessages());

                var prompt = $"""
            Context:
            {context}

            Based on the context above, please answer the following question.
            If the context doesn't provide the answer, say you don't know based on the provide information.

            User question: {query}

            Answer: 
            """;

                var userMsg = new ChatMessage(ChatRole.User, prompt);
                history.AddMessage(query.Trim());

                var responseText = new StringBuilder();
                var responses = client.GetStreamingResponseAsync([systemMessage, userMsg], options: null, CancellationToken.None);
                await foreach (var response in responses)
                {
                    Console.Write(response.Text);
                    responseText.Append(response.Text);
                }

                history.AddMessage(responseText.ToString().Trim());
                Console.WriteLine("\n");
            }
        }
    }
}
