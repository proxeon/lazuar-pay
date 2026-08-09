using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Modules.Ops.Infrastructure.Llm;

public sealed class OpenRouterHeaderPolicy : PipelinePolicy
{
    private readonly string _siteUrl;
    private readonly string _siteName;

    public OpenRouterHeaderPolicy(string siteUrl, string siteName)
    {
        _siteUrl = siteUrl;
        _siteName = siteName;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        if (!string.IsNullOrEmpty(_siteUrl))
            message.Request.Headers.Set("HTTP-Referer", _siteUrl);

        if (!string.IsNullOrEmpty(_siteName))
            message.Request.Headers.Set("X-OpenRouter-Title", _siteName);

        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        if (!string.IsNullOrEmpty(_siteUrl))
            message.Request.Headers.Set("HTTP-Referer", _siteUrl);

        if (!string.IsNullOrEmpty(_siteName))
            message.Request.Headers.Set("X-OpenRouter-Title", _siteName);

        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}
