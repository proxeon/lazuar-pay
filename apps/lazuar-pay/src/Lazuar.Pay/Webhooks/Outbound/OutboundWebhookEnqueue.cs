using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Webhooks.Outbound;

internal static class OutboundWebhookEnqueue
{
    public static async Task TryAddAsync(
        PayDbContext db,
        string orgId,
        string eventId,
        string eventType,
        object data,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orgId) || string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        if (await db.OrgWebhookEndpoints.FindAsync([orgId], ct) is null)
        {
            return;
        }

        if (await db.OrgWebhookDeliveries.AnyAsync(x => x.OrgId == orgId && x.EventId == eventId, ct))
        {
            return;
        }

        db.OrgWebhookDeliveries.Add(new OrgWebhookDeliveryRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            EventId = eventId,
            EventType = eventType,
            PayloadJson = PayWebhookEnvelope.Serialize(eventType, eventId, orgId, data),
            Status = "pending",
            NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
