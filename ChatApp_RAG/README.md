# Prequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later
- Install template: `dotnet new install Microsoft.Extensions.AI.Templates`
- Create new project with AI template in Visual Studio 2022

# AI Chat with Custom Data

This project is an AI chat application that demonstrates how to chat with custom data using an AI language model. 
Please note that this template is currently in an early preview stage. 
If you have feedback, please take a [brief survey](https://aka.ms/dotnet-chat-templatePreview2-survey).

>[!NOTE]
> Before running this project you need to configure the API keys or endpoints for the providers you have chosen. See below for details specific to your choices.

# Configure the AI Model Provider
To use models hosted by GitHub Models, you will need to create a GitHub personal access token with `models:read` permissions, but no other scopes or permissions. 
See [Prototyping with AI models](https://docs.github.com/github-models/prototyping-with-ai-models) and [Managing your personal access tokens]
(https://docs.github.com/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) in the GitHub Docs for more information.

Configure your token for this project using .NET User Secrets:

1. In Visual Studio, right-click on your project in the Solution Explorer and select "Manage User Secrets".
2. This opens a `secrets.json` file where you can store your API keys without them being tracked in source control. Add the following key and value:

   ```json
   {
     "GitHubModels:Token": "YOUR-TOKEN"
   }
   ```

Learn more about [prototyping with AI models using GitHub Models](https://docs.github.com/github-models/prototyping-with-ai-models).

# How it works
-------------------------------------------------------------
|                                                           |
|[Documents] → [Chunking] → [Embeddings via GitHub Models]  |
|         ↓                           ↓                     |
|     [Vector Store] ←——————— [Similarity Search]           |
|         ↓                                                 |
|[Context + User Query] → [GitHub Model LLM] → Final        |
|                                                           |
-------------------------------------------------------------

# Blazor pages structure
BlazorStructureDemo/
├── Program.cs              # App bootstrap (services, host)
├── wwwroot/                # Static files (CSS/JS/images)
│   ├── css/app.css         # Global styles
│   ├── index.html          # WASM entry (for client-side)
├── App.razor               # Root component (router outlet)
├── Components/Pages/       # Your pages (.razor)
│   ├── Counter.razor
│   ├── Weather.razor
├── Components/Layouts/     # Layouts (shared UI shells)
│   └── MainLayout.razor    # Nav bar + content
├── Components/Pages/_Host.cshtml  # SSR host (server prerender)
├── wwwroot/_content/       # NuGet assets (e.g., Bootstrap)
└── Properties/             # launchSettings.json (ports/profiles)

# Key Concepts Map

------------------------------------------------------------
|User Request (URL)                                         | 
|    ↓                                                      |
|Router (App.razor) → Finds Component (.razor)              |
|    ↓                                                      |
|Render Mode:                                               |
|  - Static: HTML only (SEO/fast).                          |
|  - Server: Razor → SignalR → DOM diff.                    |
|  - WASM: Razor → C# DLL → Browser JS interop.             |
|    ↓                                                      |
|Lifecycle: OnInitialized → OnParametersSet → OnAfterRender.|
|    ↓                                                      |
|Data/Events: @bind, @onclick → StateHasChanged().          |
|    ↓                                                      |
|Layout → Output HTML/JS.                                   |
-------------------------------------------------------------


#test