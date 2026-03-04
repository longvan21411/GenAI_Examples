	Need to add the following packages to your project file (.csproj) to run the sample code:  
	  <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.3.0" />
	  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="6.0.1" />
	  <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="10.0.0" />
	  <PackageReference Include="Microsoft.SemanticKernel.Connectors.InMemory" Version="1.72.0-preview" />
	  <PackageReference Include="OpenAI" Version="2.9.1" />
      <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="6.0.1" />


	  Step 1: Add the above packages to your project file (.csproj).
	  Step 2: Add your OpenAI API key to the user secrets store. You can do this by running the following command in the terminal:
		  dotnet user-secrets set "OpenAI.
      Step 3: Run the sample code to see how to use the vector search functionality with OpenAI embeddings and an in-memory vector store.
	  Step 4: Generate embedding and store in In-memory Vector Store.
	  Step 5: Create a query embedding and perform a vector search to retrieve relevant documents based on cosine similarity.
