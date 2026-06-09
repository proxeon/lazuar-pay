using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Ops.Application.Services;

namespace Modules.Ops.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Registered as Singleton so reflection scanning and schema generation only happens once on startup
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        return services;
    }

    public static IApplicationBuilder UseOpsSubscriptions(this IApplicationBuilder app)
    {
        return app;
    }
}
