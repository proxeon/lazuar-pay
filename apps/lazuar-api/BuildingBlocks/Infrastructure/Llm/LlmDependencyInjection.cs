using BuildingBlocks.Application.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Llm;

public static class LlmDependencyInjection
{
    public static IServiceCollection AddThinLlmFactory(this IServiceCollection services)
    {
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddScoped<ILlmTitleGenerator, LlmTitleGenerator>();
        return services;
    }
}
