using Microsoft.Extensions.DependencyInjection;

namespace Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services)
    {
        return services;
    }
}
