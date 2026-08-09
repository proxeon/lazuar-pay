using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Caching.Memory;

namespace Lazuar.Api.EventHandlers;

/// <summary>
/// Evicts revoked API keys from the middleware's memory cache instantly to
/// eliminate the 5-minute TTL security exposure window.
/// R05 One-only: handles <see cref="Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent"/> only.
/// Lhdn dual-subscribe removed (legacy dual-read window closed). Table drop is R06 — not this handler.
/// <b>DEPLOY ONLY</b> after env Q8 <c>active_legacy_only = 0</c> (or signed residual quarantine).
/// </summary>
public class ApiKeyRevokedIntegrationEventHandler :
    IIntegrationEventHandler<Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent>
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

    private void Evict(string keyHash)
    {
        _cache.Remove($"ApiKey_{keyHash}");
    }
}
