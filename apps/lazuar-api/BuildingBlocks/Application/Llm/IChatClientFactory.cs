using OpenAI.Chat;

namespace BuildingBlocks.Application.Llm;

public interface IChatClientFactory
{
    ChatClient CreateClient(string? providerOverride = null, string? modelOverride = null, bool thinkingEnabled = false, string reasoningEffort = "high");
}
