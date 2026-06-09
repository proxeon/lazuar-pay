using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Ops.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IApplicationBuilder UseOpsSubscriptions(this IApplicationBuilder app)
    {
        return app;
    }
}
