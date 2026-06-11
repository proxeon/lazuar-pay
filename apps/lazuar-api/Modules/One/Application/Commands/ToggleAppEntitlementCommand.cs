// apps/lazuar-api/Modules/One/Application/Commands/ToggleAppEntitlementCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

[AgentTool("Turn specific Lazuar modules on or off for the tenant.", "medium", "SUPER_ADMIN", "ADMIN")]
public record ToggleAppEntitlementCommand(Guid OrganizationId, string AppId, bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ToggleAppEntitlementCommandHandler : ICommandHandler<ToggleAppEntitlementCommand>
{
    private readonly IOneRepository _repository;
    private readonly IEventBus _eventBus;

    public ToggleAppEntitlementCommandHandler(
        IOneRepository repository,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(ToggleAppEntitlementCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetEntitlementAsync(request.OrganizationId, request.AppId, ct);
        bool grantedNow = false;

        if (entitlement == null)
        {
            entitlement = new TenantAppEntitlement(request.OrganizationId, request.AppId);
            if (!request.IsActive)
            {
                entitlement.Toggle(false);
            }
            else
            {
                grantedNow = true;
            }

            _repository.AddEntitlement(entitlement);
        }
        else
        {
            var wasActive = entitlement.IsActive;
            entitlement.Toggle(request.IsActive);

            if (!wasActive && request.IsActive)
            {
                grantedNow = true;
            }
        }

        await _repository.SaveChangesAsync(ct);

        if (grantedNow)
        {
            await _eventBus.PublishAsync(new AppEntitlementGrantedIntegrationEvent(request.OrganizationId, request.AppId.ToUpperInvariant()));
        }
    }
}
