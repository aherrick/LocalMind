using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace LocalMind.Providers;

// Ollama, llama.cpp, and Foundry Local all expose an OpenAI-compatible endpoint, so the chat
// client is created the same way for each: point the OpenAI client at the local base URL.
internal static class OpenAICompatible
{
    // Local servers need no real API key.
    public static IChatClient CreateChatClient(Uri baseUrl, string modelId)
        => new OpenAIClient(new ApiKeyCredential("not-needed"), new OpenAIClientOptions { Endpoint = baseUrl })
            .GetChatClient(modelId)
            .AsIChatClient();
}
