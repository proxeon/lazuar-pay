using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Subscriptions;

internal static class SubscriptionEndpoints
{
    public static void MapSubscriptions(this WebApplication app)
    {
        app.MapGet("/v1/orgs/{orgId}/subscriptions", List);
    }

    static async Task<IResult> List(
        string orgId,
        int? limit,
        string? after,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var take = PayList.Clamp(limit);
        var q = db.Subscriptions.AsNoTracking().Where(x => x.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(after))
        {
            var cursor = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == after, ct);
            if (cursor is not null)
            {
                q = q.Where(x => x.CreatedAt < cursor.CreatedAt
                    || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        string? next = null;
        if (rows.Count > take)
        {
            rows = rows.Take(take).ToList();
            next = rows[^1].Id;
        }

        return Results.Json(new
        {
            items = rows.Select(r => new
            {
                id = r.Id,
                org_id = r.OrgId,
                checkout_id = r.CheckoutId,
                interval = r.Interval,
                status = r.Status,
                dunning_status = r.Status,
                attempt_count = r.AttemptCount,
                past_due_at = r.PastDueAt,
                created_at = r.CreatedAt
            }),
            next_cursor = next
        }, OneClient.Json);
    }
}
