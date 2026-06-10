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
            var chatClient = _clientFactory.CreateClient("MIMO", "xiaomi/mimo-v2.5-pro", false);
            
            var messages = new ChatMessage[]
            {
                new SystemChatMessage(TitlePrompt),
                new UserChatMessage(preview)
            };

            var options = new ChatCompletionOptions { MaxOutputTokenCount = 30 };

            var result = await chatClient.CompleteChatAsync(messages, options);
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
