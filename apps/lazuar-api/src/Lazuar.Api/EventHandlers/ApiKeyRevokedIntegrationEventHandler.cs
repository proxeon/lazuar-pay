using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Caching.Memory;
using Modules.Lhdn.Contracts.Events;

namespace Lazuar.Api.EventHandlers;

/// <summary>
/// Evicts revoked API keys from the middleware's memory cache instantly to 
/// eliminate the 5-minute TTL security exposure window.
/// </summary>
public class ApiKeyRevokedIntegrationEventHandler : IIntegrationEventHandler<ApiKeyRevokedIntegrationEvent>
{
    private readonly IMemoryCache _cache;

    public ApiKeyRevokedIntegrationEventHandler(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task HandleAsync(ApiKeyRevokedIntegrationEvent @event)
    {
        _cache.Remove($"ApiKey_{@event.KeyHash}");
        return Task.CompletedTask;
    }
}
