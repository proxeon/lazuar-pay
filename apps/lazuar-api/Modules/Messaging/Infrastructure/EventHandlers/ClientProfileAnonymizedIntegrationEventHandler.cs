using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

/// <summary>
/// Scrubs delivery-log inboxes after PDPA wipe. Status / provider id stay for support.
/// </summary>
public class ClientProfileAnonymizedIntegrationEventHandler : IIntegrationEventHandler<ClientProfileAnonymizedIntegrationEvent>
{
    private readonly MessagingDbContext _dbContext;

    public ClientProfileAnonymizedIntegrationEventHandler(MessagingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(ClientProfileAnonymizedIntegrationEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            return;
        }

        var email = @event.Email.Trim();
        if (email.StartsWith("deleted_", StringComparison.OrdinalIgnoreCase)
            && email.EndsWith("@localhost", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rows = await _dbContext.MessageDeliveryLogs
            .IgnoreQueryFilters()
            .Where(l => l.OrganizationId == @event.OrganizationId && l.Recipient == email)
            .ToListAsync();

        if (rows.Count == 0)
        {
            var lowered = email.ToLowerInvariant();
            rows = await _dbContext.MessageDeliveryLogs
                .IgnoreQueryFilters()
                .Where(l => l.OrganizationId == @event.OrganizationId && l.Recipient.ToLower() == lowered)
                .ToListAsync();
        }

        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            row.Anonymize(@event.ClientProfileId);
        }

        await _dbContext.SaveChangesAsync();
    }
}
