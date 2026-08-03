using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Caching.Memory;

namespace Lazuar.Api.EventHandlers;

/// <summary>
/// Evicts revoked API keys from the middleware's memory cache instantly to
/// eliminate the 5-minute TTL security exposure window.
/// Handles both One (platform) and legacy Lhdn revoke events.
/// </summary>
public class ApiKeyRevokedIntegrationEventHandler :
    IIntegrationEventHandler<Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent>,
    IIntegrationEventHandler<Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent>
{
    private readonly IMemoryCache _cache;

    public ApiKeyRevokedIntegrationEventHandler(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task HandleAsync(Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent @event)
    {
        Evict(@event.KeyHash);
        return Task.CompletedTask;
    }

    public Task HandleAsync(Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent @event)
    {
        Evict(@event.KeyHash);
        return Task.CompletedTask;
    }

    private void Evict(string keyHash)
    {
        _cache.Remove($"ApiKey_{keyHash}");
    }
}
