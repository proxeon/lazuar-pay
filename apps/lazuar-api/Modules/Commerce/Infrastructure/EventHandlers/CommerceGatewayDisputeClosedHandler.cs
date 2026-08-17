using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class CommerceGatewayDisputeClosedHandler : IIntegrationEventHandler<GatewayDisputeClosedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly ILogger<CommerceGatewayDisputeClosedHandler> _logger;

    public CommerceGatewayDisputeClosedHandler(
        CommerceDbContext dbContext,
        ILogger<CommerceGatewayDisputeClosedHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayDisputeClosedIntegrationEvent @event)
    {
        var dispute = await _dbContext.Disputes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d =>
                d.OrganizationId == @event.OrganizationId
                && d.GatewayTransactionId == @event.GatewayTransactionId);

        if (dispute == null)
        {
            return;
        }

        dispute.Resolve(@event.Outcome);

        if (dispute.SubscriptionId is Guid subscriptionId)
        {
            var stillOpen = await _dbContext.Disputes
                .IgnoreQueryFilters()
                .AnyAsync(d =>
                    d.SubscriptionId == subscriptionId
                    && d.OrganizationId == @event.OrganizationId
                    && d.Id != dispute.Id
                    && d.Status == Domain.Entities.CommerceDispute.StatusOpen);

            if (!stillOpen)
            {
                var sub = await _dbContext.Subscriptions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == @event.OrganizationId);
                sub?.ClearHasOpenDispute();
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation(
            "Resolved commerce dispute {DisputeId} as {Status} for gateway tx {GatewayTxId}.",
            dispute.Id, dispute.Status, @event.GatewayTransactionId);
    }
}
