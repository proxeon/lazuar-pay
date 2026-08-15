using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Infrastructure.Dunning;

namespace Modules.Commerce.Infrastructure.Workers;

public partial class DunningEngineJob
{
    internal static int ResolveTerminalDayOffset(int gracePeriodDays, IEnumerable<int> dayOffsets) =>
        PastDueDunningProcessor.ResolveTerminalDayOffset(gracePeriodDays, dayOffsets);

    private async Task ProcessPastDueSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        List<DunningCampaign> campaigns,
        Subscription sub,
        bool whatsAppEnabled,
        CancellationToken ct)
    {
        var processor = new PastDueDunningProcessor(_logger);
        await processor.ProcessAsync(db, eventBus, sub, campaigns, whatsAppEnabled, ct);
    }
}
