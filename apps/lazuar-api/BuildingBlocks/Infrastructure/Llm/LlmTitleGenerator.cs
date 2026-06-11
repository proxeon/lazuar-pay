using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using BuildingBlocks.Application.Llm;

namespace BuildingBlocks.Infrastructure.Llm;

public sealed class LlmTitleGenerator : ILlmTitleGenerator
{
    private readonly IChatClientFactory _clientFactory;
    private readonly ILogger<LlmTitleGenerator> _logger;

    private const string TitlePrompt =
        "Summarize this text in 3-6 words as a short title. " +
        "Return ONLY the title in plain text. " +
        "CRITICAL RULES: Do NOT use any Markdown formatting (no asterisks, no hashes, no underscores). " +
        "Do NOT include mathematical equations or LaTeX. " +
        "Do NOT wrap in quotes. Do NOT put punctuation at the end.";

    public LlmTitleGenerator(IChatClientFactory clientFactory, ILogger<LlmTitleGenerator> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string contentContext)
    {
        var preview = contentContext.Length > 400
            ? (char.IsHighSurrogate(contentContext[399]) ? contentContext[..399] : contentContext[..400])
            : contentContext;

        try
        {
            // Note: Inherits default provider and model from appsettings.json
            var chatClient = _clientFactory.CreateClient(null, null, false);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(TitlePrompt),
                new UserChatMessage(preview)
            };

            // FIX: Removed MaxOutputTokenCount. 
            // Many OpenRouter models reject or return empty when max_completion_tokens is too low.
            var result = await chatClient.CompleteChatAsync(messages);

            if (result.Value.Content == null || result.Value.Content.Count == 0)
            {
                _logger.LogWarning("Title generation returned empty content, using fallback");
                return GenerateFallback(contentContext);
            }

            var text = result.Value.Content[0].Text;

            var cleaned = text
                .Replace("*", "")
                .Replace("_", "")
                .Replace("#", "")
                .Replace("`", "")
                .Replace("$", "")
                .Trim()
                .Trim('"', '\'', '.', ',', '!', '?', '-', ':')
                .Trim();

            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length <= 100)
            {
                return cleaned;
            }

            _logger.LogWarning("Title generation returned invalid result, using fallback");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate title using LLM, using fallback");
        }

        return GenerateFallback(contentContext);
    }

    public string GenerateFallback(string content)
    {
        var t = content.Replace("*", "").Replace("#", "").Replace("`", "").Trim();
        if (t.Length <= 80) return t;

        var cut = char.IsHighSurrogate(t[79]) ? t[..79] : t[..80];
        var sp = cut.LastIndexOf(' ');
        return (sp > 20 ? cut[..sp] : cut) + "…";
    }
}
