using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Caching.Memory;
using Modules.One.Contracts;

namespace Lazuar.Api.EventHandlers;

public class WorkspaceUpdatedIntegrationEventHandler : IIntegrationEventHandler<WorkspaceUpdatedIntegrationEvent>
{
    private readonly IMemoryCache _cache;

    public WorkspaceUpdatedIntegrationEventHandler(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task HandleAsync(WorkspaceUpdatedIntegrationEvent @event)
    {
        var tenantKeysKey = $"TenantKeys_{@event.OrganizationId}";
        if (_cache.TryGetValue(tenantKeysKey, out List<string>? keyHashes) && keyHashes != null)
        {
            lock (keyHashes)
            {
                foreach (var keyHash in keyHashes)
                {
                    _cache.Remove($"ApiKey_{keyHash}");
                }
            }
            _cache.Remove(tenantKeysKey);
        }
        return Task.CompletedTask;
    }
}
