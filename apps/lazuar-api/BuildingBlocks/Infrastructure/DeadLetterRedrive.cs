using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Parks Dead rows back onto the poll (ProcessedAt IS NULL). System-admin only at the host.
/// </summary>
public static class DeadLetterRedrive
{
    public static int Reset(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        var count = 0;
        if (db.Model.FindEntityType(typeof(OutboxMessage)) != null)
        {
            count += ResetSet(db.Set<OutboxMessage>());
        }

        if (db.Model.FindEntityType(typeof(InboxMessage)) != null)
        {
            count += ResetSet(db.Set<InboxMessage>());
        }

        return count;
    }

    private static int ResetSet<T>(DbSet<T> set) where T : class, IMessageProcessingState
    {
        var dead = set.IgnoreQueryFilters().Where(m => m.Status == MessageProcessingStatus.Dead).ToList();
        foreach (var message in dead)
        {
            message.Status = MessageProcessingStatus.Pending;
            message.ProcessedAt = null;
            message.NextAttemptAt = null;
            message.AttemptCount = 0;
            message.Error = null;
        }

        return dead.Count;
    }
}
