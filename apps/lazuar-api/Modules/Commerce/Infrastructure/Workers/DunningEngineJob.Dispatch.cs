using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Contracts;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Infrastructure.Dunning;

namespace Modules.Commerce.Infrastructure.Workers;

public partial class DunningEngineJob
{
    private static string? ResolveEffectiveCommunicationAction(Domain.Entities.DunningStep step, bool whatsAppEnabled) =>
        DunningStepDispatcher.ResolveEffectiveCommunicationAction(step, whatsAppEnabled);

    private static Task DispatchCommunicationStepAsync(
        CommerceDbContext db,
        Subscription sub,
        Domain.Entities.DunningStep step,
        int daysOverdue,
        string effectiveActionType,
        IEventBus eventBus,
        CancellationToken ct,
        IBillingQueryService? billing = null) =>
        DunningStepDispatcher.DispatchCommunicationStepAsync(
            db, sub, step, daysOverdue, effectiveActionType, eventBus, ct, billing);
}
