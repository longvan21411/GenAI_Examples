#Architecture

Ollama + SigLIP:	Generate image embeddings. SigLIP is the current state of the art open source model for image similarity.
Qdrant:	Vector database to store and search embeddings.
OllamaSharp:	C# client for Ollama.
Qdrant.Client:	C# client for Qdrant.
SixLabors.ImageSharp:	Load and process local images.

#Prerequisites
1. Install Ollama and set up the SigLIP model.
	sudo apt-get clean
	sudo rm -rf /var/lib/apt/lists/*
	sudo apt-get update
	sudo apt-get install -y python3.10-dev
	sudo apt-get install zstd
	curl -fsSL https://ollama.com/install.sh | sh

	#Create a docker volume to persist the model data
	docker volume create ollama-models

	#Pull the Ollama docker image and run it, mapping the volume and exposing the port
	docker run -d \
	  -p 11434:11434 \
	  -v ollama-models:/root/.ollama \
	  --name ollama \
	  ollama/ollama:latest

	#Pull the model you want to use, for example: pull llama3.2 and mistral 
		ollama pull llama3.2
		ollama pull mistral

	#Pull the SigLIP model for image embeddings
		ollama pull siglip

	#Verify the model is pulled successfully
		ollama ollama list

2. Install Qdrant and create a collection for storing image embeddings.
3. Create a C# project and add the following NuGet packages:
   - OllamaSharp
   - Qdrant.Client
   - SixLabors.ImageSharp
