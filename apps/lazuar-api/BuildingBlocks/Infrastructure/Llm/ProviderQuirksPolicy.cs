using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BuildingBlocks.Infrastructure.Llm;

public sealed class ProviderQuirksPolicy : PipelinePolicy
{
    private readonly string _provider;
    private readonly bool _thinkingEnabled;
    private readonly string _reasoningEffort;

    public ProviderQuirksPolicy(string provider, bool thinkingEnabled, string reasoningEffort)
    {
        _provider = provider.ToUpperInvariant();
        _thinkingEnabled = thinkingEnabled;
        _reasoningEffort = reasoningEffort;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        RewriteRequestBody(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        RewriteRequestBody(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void RewriteRequestBody(PipelineMessage message)
    {
        if (message.Request.Content == null) return;

        using var stream = new MemoryStream();
        message.Request.Content.WriteTo(stream, default);
        stream.Position = 0;

        var jsonNode = JsonNode.Parse(stream) as JsonObject;
        if (jsonNode == null) return;

        if (_provider == "OPENROUTER")
        {
            if (_thinkingEnabled)
            {
                jsonNode["include_reasoning"] = true;
                jsonNode["reasoning_effort"] = _reasoningEffort;
            }
        }
        else if (_provider == "DEEPSEEK")
        {
            if (_thinkingEnabled)
            {
                jsonNode["thinking"] = new JsonObject { ["type"] = "enabled" };
                jsonNode["reasoning_effort"] = _reasoningEffort;
            }
        }
        else if (_provider == "MIMO")
        {
            if (_thinkingEnabled)
            {
                jsonNode["thinking"] = new JsonObject { ["type"] = "enabled" };
                jsonNode["temperature"] = 1.0;
            }
            
            if (jsonNode.TryGetPropertyValue("max_tokens", out var maxTokensNode) && maxTokensNode != null)
            {
                jsonNode["max_completion_tokens"] = maxTokensNode.GetValue<int>();
                jsonNode.Remove("max_tokens");
            }
        }

        message.Request.Content = BinaryContent.Create(BinaryData.FromString(jsonNode.ToJsonString()));
    }
}
