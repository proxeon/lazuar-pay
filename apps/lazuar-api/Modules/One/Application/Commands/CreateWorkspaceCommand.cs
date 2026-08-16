using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record CreateWorkspaceCommand(
    Guid OwnerUserId,
    string Name,
    string Slug,
    List<string> ProvisionApps) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateWorkspaceCommandHandler : ICommandHandler<CreateWorkspaceCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly IEventBus _eventBus;

    public CreateWorkspaceCommandHandler(
        IOneRepository repository,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(CreateWorkspaceCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByIdAsync(request.OwnerUserId, ct);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        var isSlugUnique = await _repository.IsSlugUniqueAsync(slug, ct);
        if (!isSlugUnique)
        {
            throw new InvalidOperationException("The requested workspace slug is already taken. Please choose another.");
        }

        var organization = new Organization(request.Name, slug);
        _repository.AddOrganization(organization);

        var membership = new TenantMembership(request.OwnerUserId, organization.Id, "ADMIN");
        _repository.AddTenantMembership(membership);

        foreach (var appId in request.ProvisionApps)
        {
            var cleanAppId = appId.Trim().ToUpperInvariant();
            var entitlement = new TenantAppEntitlement(organization.Id, cleanAppId);
            _repository.AddEntitlement(entitlement);

            await _eventBus.PublishAsync(new AppEntitlementGrantedIntegrationEvent(organization.Id, cleanAppId));
        }

        await _repository.SaveChangesAsync(ct);

        return organization.Id;
    }
}
