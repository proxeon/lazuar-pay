using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money.Queries;

internal static class PaymentQueryEndpoints
{
    public static void MapPaymentQueries(this WebApplication app)
    {
        app.MapGet("/v1/orgs/{orgId}/payments", List);
        app.MapGet("/v1/orgs/{orgId}/receipts", ListReceipts);
        app.MapGet("/v1/orgs/{orgId}/receipts/{id}", Receipt);
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
        if (denied is not null) return denied;
        var take = PayList.Clamp(limit);
        var joined =
            from c in db.Charges.AsNoTracking()
            join ch in db.Checkouts.AsNoTracking() on c.CheckoutId equals ch.Id
            where c.OrgId == orgId
            select new { Charge = c, Checkout = ch };
        if (!string.IsNullOrWhiteSpace(after))
        {
            // Issue 015: the cursor row is fetched org-scoped — a foreign org's charge id
            // used to resolve globally, leaking cross-org existence + timestamps through the
            // page boundary an empty/bogus cursor would not produce.
            var cursor = await (
                from c in db.Charges.AsNoTracking()
                join ch in db.Checkouts.AsNoTracking() on c.CheckoutId equals ch.Id
                where c.OrgId == orgId && c.Id == after
                select new { c.Id, ch.CreatedAt }).FirstOrDefaultAsync(ct);
            if (cursor is not null)
            {
                joined = joined.Where(x => x.Checkout.CreatedAt < cursor.CreatedAt
                    || (x.Checkout.CreatedAt == cursor.CreatedAt && x.Charge.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var page = await joined
            .OrderByDescending(x => x.Checkout.CreatedAt)
            .ThenByDescending(x => x.Charge.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        string? next = null;
        if (page.Count > take)
        {
            page = page.Take(take).ToList();
            next = page[^1].Charge.Id;
        }

        var checkoutIds = page.Select(x => x.Charge.CheckoutId).Distinct().ToList();
        var productIds = page
            .Select(x => x.Checkout.ProductId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var names = productIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return Results.Json(new
        {
            items = page.Select(x =>
            {
                var c = x.Charge;
                var ch = x.Checkout;
                return new
                {
                    id = c.Id,
                    org_id = c.OrgId,
                    checkout_id = c.CheckoutId,
                    amount = c.Amount,
                    currency = c.Currency,
                    status = c.Status,
                    provider = c.Provider,
                    payer_name = ch.PayerName,
                    created_at = ch.CreatedAt,
                    label = ch.ProductId is string pid && names.TryGetValue(pid, out var name) ? name : null
                };
            }),
            next_cursor = next
        }, OneClient.Json);
    }

    static async Task<IResult> ListReceipts(
        string orgId,
        int? limit,
        string? after,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var take = PayList.Clamp(limit);
        var q = db.Documents.AsNoTracking().Where(d => d.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(after))
        {
            // Issue 015: org-scope the cursor row (see PaymentLinkEndpoints.List).
            var cursor = await db.Documents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Id == after, ct);
            if (cursor is not null)
            {
                q = q.Where(x => x.CreatedAt < cursor.CreatedAt
                    || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var rows = await q.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        string? next = null;
        if (rows.Count > take)
        {
            rows = rows.Take(take).ToList();
            next = rows[^1].Id;
        }
        var checkoutIds = rows.Select(d => d.CheckoutId).Distinct().ToList();
        var checkouts = checkoutIds.Count == 0
            ? []
            : await db.Checkouts.AsNoTracking().Where(x => checkoutIds.Contains(x.Id)).ToListAsync(ct);
        var byCheckout = checkouts.ToDictionary(x => x.Id);
        var charges = checkoutIds.Count == 0
            ? []
            : await db.Charges.AsNoTracking().Where(c => checkoutIds.Contains(c.CheckoutId)).ToListAsync(ct);
        var byCharge = charges
            .GroupBy(c => c.CheckoutId)
            .ToDictionary(g => g.Key, g => g.First());
        var productIds = checkouts
            .Select(x => x.ProductId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var names = productIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return Results.Json(new
        {
            items = rows.Select(d =>
            {
                var ch = byCheckout.GetValueOrDefault(d.CheckoutId);
                var charge = byCharge.GetValueOrDefault(d.CheckoutId);
                return new
                {
                    id = d.Id,
                    org_id = d.OrgId,
                    number = d.Number ?? "PENDING",
                    title = d.Title,
                    checkout_id = d.CheckoutId,
                    amount = charge?.Amount ?? ch?.Amount,
                    currency = charge?.Currency ?? ch?.Currency,
                    payer_name = ch?.PayerName,
                    created_at = d.CreatedAt,
                    label = ch?.ProductId is string pid && names.TryGetValue(pid, out var name) ? name : null,
                    status = string.IsNullOrWhiteSpace(d.Number) ? "pending" : "issued"
                };
            }),
            next_cursor = next
        }, OneClient.Json);
    }

    static async Task<IResult> Receipt(
        string orgId,
        string id,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.OrgId == orgId, ct);
        if (doc is null)
        {
            return PayErrors.Status(404, "Not Found", "Receipt not found");
        }

        var checkout = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == doc.CheckoutId, ct);
        var charge = await db.Charges.AsNoTracking().FirstOrDefaultAsync(c => c.CheckoutId == doc.CheckoutId, ct);
        string? label = null;
        if (!string.IsNullOrWhiteSpace(checkout?.ProductId))
        {
            label = await db.Products.AsNoTracking()
                .Where(p => p.Id == checkout.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct);
        }

        return Results.Json(new
        {
            id = doc.Id,
            org_id = doc.OrgId,
            number = doc.Number ?? "PENDING",
            title = doc.Title,
            checkout_id = doc.CheckoutId,
            amount = charge?.Amount ?? checkout?.Amount,
            currency = charge?.Currency ?? checkout?.Currency,
            payer_name = checkout?.PayerName,
            created_at = doc.CreatedAt,
            label,
            status = string.IsNullOrWhiteSpace(doc.Number) ? "pending" : "issued"
        }, OneClient.Json);
    }
}
