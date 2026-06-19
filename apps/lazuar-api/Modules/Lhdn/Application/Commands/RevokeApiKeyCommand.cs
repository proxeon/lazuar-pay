using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Lhdn.Application.Commands;

public record RevokeApiKeyCommand(Guid OrganizationId, Guid ApiKeyId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RevokeApiKeyCommandHandler : ICommandHandler<RevokeApiKeyCommand>
{
    private readonly ILhdnRepository _repository;
    private readonly IEventBus _eventBus;

    public RevokeApiKeyCommandHandler(ILhdnRepository repository, [FromKeyedServices("LhdnEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken ct)
    {
        var apiKey = await _repository.GetDeveloperApiKeyAsync(request.ApiKeyId, ct);

        if (apiKey == null || apiKey.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("API Key not found or does not belong to this organization.");
        }

        if (!apiKey.IsActive)
        {
            return; // Already revoked
        }

        apiKey.Revoke();

        // Publish event to trigger immediate cache eviction in the API authentication layer
        await _eventBus.PublishAsync(new ApiKeyRevokedIntegrationEvent(apiKey.OrganizationId, apiKey.KeyHash));
        
        await _repository.SaveChangesAsync(ct);
    }
}
