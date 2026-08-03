using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public class AnonymizeClientProfileCommandHandler : ICommandHandler<AnonymizeClientProfileCommand>
{
    private readonly CrmDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public AnonymizeClientProfileCommandHandler(
        CrmDbContext dbContext,
        [FromKeyedServices("CrmEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task Handle(AnonymizeClientProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ClientProfiles
            .FirstOrDefaultAsync(p => p.Id == request.ClientProfileId && p.OrganizationId == request.OrganizationId, cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException("Client profile not found.");
        }

        profile.Anonymize();

        // Publish into outbox first so a single SaveChanges persists domain + outbox atomically.
        await _eventBus.PublishAsync(new ClientProfileAnonymizedIntegrationEvent(
            request.OrganizationId,
            request.ClientProfileId));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
