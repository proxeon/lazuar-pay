using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application;

internal static class SubscriptionCancelApplier
{
    public static async Task<string> ApplyAndPersistAsync(
        ICommerceRepository repository,
        IEventBus eventBus,
        Domain.Aggregates.Subscription subscription,
        bool atPeriodEnd,
        string canceledStatus,
        CancellationToken ct)
    {
        var outcome = SubscriptionCancelDecision.Apply(subscription, atPeriodEnd);
        if (outcome == SubscriptionCancelDecision.Outcome.AlreadyCanceled)
        {
            return canceledStatus;
        }

        if (outcome == SubscriptionCancelDecision.Outcome.Scheduled)
        {
            await repository.SaveChangesAsync(ct);
            return "scheduled";
        }

        var product = await repository.GetProductByIdAsync(subscription.OrganizationId, subscription.ProductId, ct);
        var fulfillmentTargets = product?.FulfillmentTargets.ToList() ?? [];

        await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
            subscription.OrganizationId,
            subscription.Id,
            subscription.ClientProfileId,
            subscription.ProductId,
            fulfillmentTargets));

        await repository.SaveChangesAsync(ct);
        return canceledStatus;
    }
}
