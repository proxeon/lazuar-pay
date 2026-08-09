using OpenAI.Chat;

namespace Modules.Ops.Application.Llm;

public interface IChatClientFactory
{
    ChatClient CreateClient(string? providerOverride = null, string? modelOverride = null, bool thinkingEnabled = false, string reasoningEffort = "high");
}
