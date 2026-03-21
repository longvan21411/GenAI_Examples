- ensure the Ollama model is working fine, you can follow these steps:
download docker image and start it via

#0 Host the Ollama model in a docker container, you can follow these steps:
sudo apt-get clean
sudo rm -rf /var/lib/apt/lists/*
sudo apt-get update
sudo apt-get install -y python3.10-dev
sudo apt-get install zstd
curl -fsSL https://ollama.com/install.sh | sh

#1 Create a docker volume to persist the model data
docker volume create ollama-models

#2 Pull the Ollama docker image and run it, mapping the volume and exposing the port
docker run -d \
  -p 11434:11434 \
  -v ollama-models:/root/.ollama \
  --name ollama \
  ollama/ollama:latest

#3 Pull the model you want to use, for example: pull llama3.2 and mistral 
ollama pull llama3.2
ollama pull mistral

#4 Verify the model is pulled successfully
ollama ollama list

#5 Verify the model is working by running a test inference
APIs are the same:

    http://localhost:11434/api/generate
    http://localhost:11434/api/chat
    http://localhost:11434/api/tags
    http://localhost:11434/v1/chat/completions
#dependency
dotnet add package Microsoft.Extensions.AI.Ollama


#Ollama issues:
Error response from daemon: failed to set up container networking: driver failed programming external connectivity on endpoint ollama 
(252c8f6851ed44fae4ae940ff3f41290e6ec62d0d594fcdf45f979104090c22d): failed to bind host port for 0.0.0.0:11434:172.17.0.2:11434/tcp: address already in use.
-> Sometimes Docker creates a partial container even if the command fails. 
Before trying again, clean up the failed container:
docker rm -f ollama
docker run -d \
  -p 11434:11434 \
  -v ollama-models:/root/.ollama \
  --name ollama \
  ollama/ollama:latest