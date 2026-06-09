using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using BuildingBlocks.Application.Llm;

namespace BuildingBlocks.Infrastructure.Llm;

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly IConfiguration _config;

    public ChatClientFactory(IConfiguration config)
    {
        _config = config;
    }

    public ChatClient CreateClient(string? providerOverride = null, string? modelOverride = null, bool thinkingEnabled = false, string reasoningEffort = "high")
    {
        var provider = (providerOverride ?? _config["Ai:Provider"] ?? "OPENAI").ToUpperInvariant();
        var model = modelOverride ?? _config["Ai:Model"] ?? "gpt-4o";
        var apiKey = _config[$"Ai:ProviderKeys:{provider}"] ?? _config["Ai:ApiKey"] ?? throw new InvalidOperationException($"Missing API Key for LLM Provider: {provider}");

        return provider switch
        {
            "OPENROUTER" => CreateOpenRouterClient(apiKey, model, thinkingEnabled, reasoningEffort),
            "DEEPSEEK" => CreateDeepSeekClient(apiKey, model, thinkingEnabled, reasoningEffort),
            "MIMO" => CreateMiMoClient(apiKey, model, thinkingEnabled, reasoningEffort),
            _ => new ChatClient(model, apiKey)
        };
    }

    private ChatClient CreateOpenRouterClient(string apiKey, string model, bool thinkingEnabled, string reasoningEffort)
    {
        var siteUrl = _config["OpenRouter:SiteUrl"] ?? "";
        var siteName = _config["OpenRouter:SiteName"] ?? "";

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1")
        };
        options.AddPolicy(new OpenRouterHeaderPolicy(siteUrl, siteName), PipelinePosition.PerCall);
        options.AddPolicy(new ProviderQuirksPolicy("OPENROUTER", thinkingEnabled, reasoningEffort), PipelinePosition.BeforeTransport);

        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model);
    }

    private ChatClient CreateDeepSeekClient(string apiKey, string model, bool thinkingEnabled, string reasoningEffort)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.deepseek.com")
        };
        options.AddPolicy(new ProviderQuirksPolicy("DEEPSEEK", thinkingEnabled, reasoningEffort), PipelinePosition.BeforeTransport);

        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model);
    }

    private ChatClient CreateMiMoClient(string apiKey, string model, bool thinkingEnabled, string reasoningEffort)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.xiaomimimo.com/v1")
        };
        options.AddPolicy(new ProviderQuirksPolicy("MIMO", thinkingEnabled, reasoningEffort), PipelinePosition.BeforeTransport);

        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model);
    }
}
