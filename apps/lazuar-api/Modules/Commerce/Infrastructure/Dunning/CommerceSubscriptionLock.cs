using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Modules.Commerce.Infrastructure.Dunning;

internal static class CommerceSubscriptionLock
{
    public static async Task AcquireAsync(CommerceDbContext db, Guid subscriptionId, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM commerce."Subscriptions" WHERE "Id" = {subscriptionId} FOR UPDATE""",
            ct);
    }
}
