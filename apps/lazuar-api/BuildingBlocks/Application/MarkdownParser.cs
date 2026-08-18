using Markdig;

namespace BuildingBlocks.Application;

public static class MarkdownParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }
        
        return Markdown.ToHtml(markdown, Pipeline);
    }

    public static string ToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToPlainText(markdown, Pipeline).Trim();
    }
}
