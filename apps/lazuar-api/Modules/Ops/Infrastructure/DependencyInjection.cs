using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Ops.Application.Services;
using Modules.Ops.Infrastructure.Services;

namespace Modules.Ops.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddScoped<ILlmOrchestratorService, LlmOrchestratorService>();

        return services;
    }

    public static IApplicationBuilder UseOpsSubscriptions(this IApplicationBuilder app)
    {
        return app;
    }
}
