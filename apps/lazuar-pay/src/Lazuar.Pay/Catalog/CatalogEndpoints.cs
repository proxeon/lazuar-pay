using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Money;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Catalog;

internal static class CatalogEndpoints
{
    public static void MapCatalog(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId}/products", Create);
        app.MapGet("/v1/orgs/{orgId}/products", List);
    }

    static async Task<IResult> Create(
        string orgId,
        CreateProductRequest? body,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var name = body?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return PayErrors.Status(400, "Bad Request", "name is required");
        }

        var currency = string.IsNullOrWhiteSpace(body?.Currency) ? "MYR" : body!.Currency!.Trim().ToUpperInvariant();
        if (currency != "MYR")
        {
            return PayErrors.Status(400, "Bad Request", "Bar B currency is MYR");
        }

        var amountErr = MoneyMath.QuotedAmountError(body?.Amount);
        if (amountErr is not null)
        {
            return amountErr;
        }

        var interval = BillingIntervals.OneOff;
        var intervalErr = BillingIntervals.Error(body!.Interval);
        if (intervalErr is not null)
        {
            return intervalErr;
        }

        var product = new ProductRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Name = name,
            Description = body.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var price = new PriceRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ProductId = product.Id,
            Currency = currency,
            Amount = body.Amount!.Value,
            Interval = interval
        };
        db.Products.Add(product);
        db.Prices.Add(price);
        await db.SaveChangesAsync(ct);
        return Results.Json(new { id = product.Id, org_id = orgId, name = product.Name, price_id = price.Id, amount = price.Amount, currency, interval = price.Interval }, OneClient.Json, statusCode: 201);
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
        var q = db.Products.AsNoTracking().Where(p => p.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(after))
        {
            // Issue 015: org-scope the cursor row (see PaymentLinkEndpoints.List).
            var cursor = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Id == after, ct);
            if (cursor is not null)
            {
                q = q.Where(x => x.CreatedAt < cursor.CreatedAt
                    || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var products = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        string? next = null;
        if (products.Count > take)
        {
            products = products.Take(take).ToList();
            next = products[^1].Id;
        }

        var ids = products.Select(p => p.Id).ToList();
        var prices = await db.Prices.AsNoTracking().Where(p => ids.Contains(p.ProductId)).ToListAsync(ct);
        return Results.Json(new
        {
            items = products.Select(p => new
            {
                id = p.Id,
                org_id = p.OrgId,
                name = p.Name,
                prices = prices.Where(x => x.ProductId == p.Id).Select(x => new { x.Id, x.Amount, x.Currency, x.Interval })
            }),
            next_cursor = next
        }, OneClient.Json);
    }
}

public sealed class CreateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Interval { get; set; }
}
