# ChatApp_RAG Instructions

## 1. Current features and functions

1. `Program.cs`
   - Loads user secrets and configuration.
   - Configures Serilog logging.
   - Creates the GitHub Models/OpenAI chat client and embedding client.
   - Registers the vector store, `DataIngestor`, `SemanticSearch`, and Blazor services.
   - Chooses between local SQLite and Qdrant-related settings from configuration.

2. `Services/EmbeddingService.cs`
   - Generates embeddings from text input.
   - Handles multiple SDK response shapes with reflection and JSON fallback.
   - Useful as the text-to-vector adapter for future search features.

3. `Services/SemanticSearch.cs`
   - Ensures documents are loaded before search.
   - Searches the vector collection for relevant chunks.
   - Checks whether a question has relevant documents before enabling RAG behavior.

4. `Services/Ingestion/DataIngestor.cs`
   - Runs the document ingestion pipeline.
   - Uses a semantic similarity chunker with embeddings.
   - Writes chunks into the configured vector store.

5. `Services/Ingestion/DocumentReader.cs`
   - Chooses the proper reader by file type.
   - Supports markdown and PDF input.
   - Normalizes document identifiers for ingestion.

6. `Services/Ingestion/PdfPigReader.cs`
   - Reads PDF pages and converts them into ingestion sections.
   - Extracts text blocks from PDF content.

7. `Services/IngestedChunk.cs`
   - Defines the vector record schema.
   - Stores document id, text, context, and embedding vector mapping.
   - Sets the collection name, distance function, and vector size.

8. `Components/Pages/Chat/Chat.razor`
   - Main chat page.
   - Maintains conversation state and streaming assistant responses.
   - Uses tools for loading documents, searching documents, and a sample temperature function.
   - Decides whether to use retrieval-augmented generation based on document relevance.

9. `Components/Pages/Chat/ChatInput.razor`
   - Handles user message input.
   - Supports image selection in the UI.
   - Manages waiting state while the assistant is responding.

10. `Components/Pages/Chat/ChatMessageList.razor`
    - Renders the full message list.
    - Shows the in-progress assistant message.
    - Imports JavaScript to support auto-scrolling.

11. `Components/Pages/Chat/ChatMessageItem.razor`
    - Renders user and assistant messages.
    - Parses and displays citations.
    - Shows tool activity for document loading and search.

12. `Components/Pages/Chat/ChatSuggestions.razor`
    - Generates follow-up suggestions from the conversation.
    - Limits the prompt context to recent messages.

13. `Components/Pages/Chat/ChatCitation.razor`
    - Builds document viewer links for markdown and PDF citations.
    - Displays quoted evidence from retrieved content.

14. `Components/Layout/MainLayout.razor`
    - Global app shell.
    - Provides header, theme toggle, and navigation buttons.

15. `Components/Pages/ImageUpload.razor`
    - Lets the user pick images and preview them.
    - Captures metadata such as category and description.
   - Calls the image Qdrant service to persist uploads and vector data.

16. `Services/QdrantImageService.cs`
   - Separates image upload and Qdrant persistence logic from the UI.
   - Saves files into a specific `wwwroot/uploads/images` folder by default.
   - Creates a Qdrant collection with named vectors for image embedding and description embedding.
   - Upserts each image as a single point with metadata payload and two vectors.

## 2. Recommended approach for future feature implementation

1. Keep feature logic separated by concern.
   - Put UI behavior in `.razor` components.
   - Put data access and vector logic in `Services`.
   - Put ingestion-specific logic under `Services/Ingestion`.

2. Reuse the existing ingestion pattern.
   - Add a reader for new content types.
   - Convert the source into ingestion sections or chunks.
   - Write the chunks into the same vector store pipeline.

3. Keep vector schema explicit.
   - Add a new record type if a feature needs its own collection.
   - Use a clear collection name and vector size.
   - Store metadata needed for retrieval and display.

4. Update the chat tool flow carefully.
   - Add new tool methods in `Chat.razor` only when they are truly part of the assistant workflow.
   - Keep tool descriptions short and accurate.
   - Preserve the current `LoadDocuments` and `Search` behavior.

5. Validate configuration first.
   - Keep endpoint, token, model name, and vector store settings in `appsettings.json` or user secrets.
   - Confirm the active vector database before adding new storage code.

6. Verify the feature before extending it.
   - Upload one small `.jpg` or `.png` image from the image page.
   - Confirm the file is saved under `wwwroot/uploads/images`.
   - Confirm the Qdrant point is created in `data-chatapp_rag-images`.
   - Confirm the payload contains filename, stored path, description, category, and created time.

## 3. Guide to add new images and store them in Qdrant

1. Decide what should be stored for each image.
   - Recommended metadata: `Id`, `FileName`, `ContentType`, `Description`, `Category`, `CreatedAt`, `Embedding`, and optionally `ImageUrl` or `Base64`.

2. Create an image record model.
   - Add a new class similar to `IngestedChunk`.
   - Use `[VectorStoreKey]` for the key.
   - Use `[VectorStoreData]` for metadata fields.
   - Use `[VectorStoreVector]` for the image embedding field.

3. Generate image embeddings.
   - Use a vision-capable embedding model or an image embedding pipeline.
   - Convert the image to the format required by the embedding provider.
   - Keep the embedding size aligned with the Qdrant collection configuration.

4. Store the image record in Qdrant.
   - Create or reuse a Qdrant-backed vector store collection.
   - Upsert the image record together with its metadata and vector.
   - Use a dedicated collection name for images, such as `chatapp-rag-images`.

5. Connect the upload UI to storage.
   - Extend `ImageUpload.razor` to call an upload service after validation.
   - Save the original file only if needed.
   - Persist metadata plus embedding in Qdrant.

6. Add search or retrieval later if needed.
   - Search by text metadata, category, or similarity.
   - Return the closest image matches from Qdrant.
   - Show results in the UI with thumbnail, filename, and description.

7. Keep document and image pipelines separate.
   - Do not mix text chunks and image vectors in the same collection unless the schema is intentionally shared.
   - Prefer one collection for text chunks and one collection for images.

## 4. Notes for this project

1. The chat page already contains retrieval logic, streaming response handling, and citation rendering.
2. The image upload page already contains upload validation and metadata capture, so it is the best place to extend image ingestion.
3. The Qdrant configuration is already present in `appsettings.json`, so future image storage can follow the same pattern as text retrieval.
4. For a production image feature, add a dedicated service layer instead of putting Qdrant write logic directly in the Razor component.
