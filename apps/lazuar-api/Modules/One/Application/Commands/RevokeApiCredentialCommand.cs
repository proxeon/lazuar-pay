using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts.Events;

namespace Modules.One.Application.Commands;

public record RevokeApiCredentialCommand(Guid OrganizationId, Guid CredentialId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RevokeApiCredentialCommandHandler : ICommandHandler<RevokeApiCredentialCommand>
{
    private readonly IOneRepository _repository;
    private readonly IEventBus _eventBus;

    public RevokeApiCredentialCommandHandler(
        IOneRepository repository,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(RevokeApiCredentialCommand request, CancellationToken ct)
    {
        var credential = await _repository.GetApiCredentialAsync(request.CredentialId, ct);

        if (credential == null || credential.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("API credential not found or does not belong to this organization.");
        }

        if (!credential.IsActive)
        {
            return; // Already revoked
        }

        credential.Revoke();

        await _eventBus.PublishAsync(new ApiKeyRevokedIntegrationEvent(credential.OrganizationId, credential.KeyHash));
        await _repository.SaveChangesAsync(ct);
    }
}
