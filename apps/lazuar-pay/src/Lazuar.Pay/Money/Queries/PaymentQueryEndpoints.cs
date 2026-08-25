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
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var rows = await db.Charges.AsNoTracking().Where(c => c.OrgId == orgId).ToListAsync(ct);
        var checkoutIds = rows.Select(c => c.CheckoutId).Distinct().ToList();
        var checkouts = checkoutIds.Count == 0
            ? []
            : await db.Checkouts.AsNoTracking().Where(x => checkoutIds.Contains(x.Id)).ToListAsync(ct);
        var byCheckout = checkouts.ToDictionary(x => x.Id);
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

        return Results.Json(rows
            .OrderByDescending(c => byCheckout.TryGetValue(c.CheckoutId, out var ch) ? ch.CreatedAt : DateTimeOffset.MinValue)
            .Select(c =>
            {
                var ch = byCheckout.GetValueOrDefault(c.CheckoutId);
                return new
                {
                    id = c.Id,
                    org_id = c.OrgId,
                    checkout_id = c.CheckoutId,
                    amount = c.Amount,
                    currency = c.Currency,
                    status = c.Status,
                    provider = c.Provider,
                    payer_name = ch?.PayerName,
                    created_at = ch?.CreatedAt,
                    label = ch?.ProductId is string pid && names.TryGetValue(pid, out var name) ? name : null
                };
            }), OneClient.Json);
    }

    static async Task<IResult> ListReceipts(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.OrgId == orgId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
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

        return Results.Json(rows.Select(d =>
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
        }), OneClient.Json);
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

        return Results.Json(new
        {
            id = doc.Id,
            org_id = doc.OrgId,
            number = doc.Number ?? "PENDING",
            title = doc.Title,
            checkout_id = doc.CheckoutId
        }, OneClient.Json);
    }
}
