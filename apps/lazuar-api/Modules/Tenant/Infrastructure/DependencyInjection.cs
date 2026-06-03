using Microsoft.Extensions.DependencyInjection;
using Modules.Tenant.Contracts;

namespace Modules.Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantQueryService, TenantQueryService>();
        return services;
    }
}
