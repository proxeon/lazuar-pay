using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Contracts.Events;

namespace Modules.One.Application.Commands;

public record ArchiveWorkspaceCommand(Guid OrganizationId, Guid RequesterUserId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ArchiveWorkspaceCommandHandler : ICommandHandler<ArchiveWorkspaceCommand>
{
    private readonly IOneRepository _repository;
    private readonly IEventBus _eventBus;

    public ArchiveWorkspaceCommandHandler(
        IOneRepository repository,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(ArchiveWorkspaceCommand request, CancellationToken ct)
    {
        var membership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        if (membership == null || membership.Role != "ADMIN")
        {
            throw new InvalidOperationException("Unauthorized to archive workspace.");
        }

        var organization = await _repository.GetOrganizationByIdAsync(request.OrganizationId, ct);
        if (organization == null || !organization.IsActive)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        organization.Archive();

        var revokedHashes = new System.Collections.Generic.List<string>();
        foreach (var key in await _repository.ListApiCredentialsAsync(request.OrganizationId, ct))
        {
            if (!key.IsActive) continue;
            key.Revoke();
            revokedHashes.Add(key.KeyHash);
        }

        foreach (var invite in await _repository.ListPendingInvitationsAsync(request.OrganizationId, ct))
            invite.Revoke();

        foreach (var member in await _repository.ListMembershipsAsync(request.OrganizationId, ct))
            _repository.RemoveTenantMembership(member);

        await _repository.SaveChangesAsync(ct);

        foreach (var hash in revokedHashes)
            await _eventBus.PublishAsync(new ApiKeyRevokedIntegrationEvent(request.OrganizationId, hash));

        await _eventBus.PublishAsync(new TenantUpdatedIntegrationEvent(
            organization.Id, organization.Name, organization.Slug, organization.IsActive));
    }
}
