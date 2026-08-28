using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Solana;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.PaymentLinks;

internal static class PaymentLinkEndpoints
{
    public static void MapPaymentLinks(this WebApplication app)
    {
        app.MapPost("/v1/payment-links", Create);
        app.MapGet("/v1/orgs/{orgId}/payment-links", List);
    }

    static async Task<IResult> Create(
        CreatePaymentLinkRequest? body,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        IHostEnvironment env,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var orgId = body?.OrgId?.Trim();
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId ?? "", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var settings = orgId is null ? null : await db.OrgSettings.FindAsync([orgId], cancellationToken);
        if (settings is null && orgId is not null)
        {
            settings = new OrgSettingsRow { OrgId = orgId };
            db.OrgSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(403, "Forbidden", "Org charges are paused");
        }

        if (body?.Amount is null || body.Amount <= 0)
        {
            return PayErrors.Status(400, "Bad Request", "amount must be greater than 0");
        }

        if (!PayProviders.TryNormalize(body.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        if (PayProviders.IsTest(provider))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "test processor is not enabled");
            }
        }
        else
        {
            var cred = await db.GatewayCredentials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == provider, cancellationToken);
            if (cred is null)
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }

        int? maxPayers;
        if (body.Unlimited)
        {
            maxPayers = null;
        }
        else
        {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1)
            {
                return PayErrors.Status(400, "Bad Request", "max_payers must be at least 1");
            }
        }

        var productId = string.IsNullOrWhiteSpace(body.ProductId) ? null : body.ProductId.Trim();
        var mintErr = SolanaMoney.MintError(provider, body.Currency, interval: null, productId);
        if (mintErr is not null)
        {
            return mintErr;
        }

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency.Trim().ToUpperInvariant();
        if (productId is not null)
        {
            var product = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId && p.OrgId == orgId, cancellationToken);
            if (product is null)
            {
                return PayErrors.Status(404, "Not Found", "product not found");
            }

            var price = await db.Prices.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
            if (price is not null
                && (price.Amount != body.Amount.Value
                    || !string.Equals(price.Currency, currency, StringComparison.OrdinalIgnoreCase)))
            {
                return PayErrors.Status(400, "Bad Request", "amount must match the catalog price");
            }
        }

        var row = new PaymentLinkRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId!,
            Provider = provider,
            ProductId = productId,
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = body.Amount.Value,
            Currency = currency,
            MaxPayers = maxPayers,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.PaymentLinks.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Json(Map(row, taken: 0, paid: 0, config: config, env: env), OneClient.Json, statusCode: 201);
    }

    static async Task<IResult> List(
        string orgId,
        int? limit,
        string? after,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        IHostEnvironment env,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var take = PayList.Clamp(limit);
        var q = db.PaymentLinks.AsNoTracking().Where(x => x.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(after))
        {
            var cursor = await db.PaymentLinks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == after, cancellationToken);
            if (cursor is not null)
            {
                q = q.Where(x => x.CreatedAt < cursor.CreatedAt
                    || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var rows = await q
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take + 1)
            .ToListAsync(cancellationToken);
        string? next = null;
        if (rows.Count > take)
        {
            rows = rows.Take(take).ToList();
            next = rows[^1].Id;
        }
        var ids = rows.Select(r => r.Id).ToList();
        var children = ids.Count == 0
            ? []
            : await db.Checkouts.AsNoTracking()
                .Where(c => c.PaymentLinkId != null && ids.Contains(c.PaymentLinkId))
                .Select(c => new { c.PaymentLinkId, c.Status })
                .ToListAsync(cancellationToken);
        var takenBy = children
            .Where(c => PaymentLinkOccupancy.CountsTowardCapacity(c.Status))
            .GroupBy(c => c.PaymentLinkId!)
            .ToDictionary(g => g.Key, g => g.Count());
        var paidBy = children
            .Where(c => c.Status == "paid")
            .GroupBy(c => c.PaymentLinkId!)
            .ToDictionary(g => g.Key, g => g.Count());
        var productIds = rows
            .Select(x => x.ProductId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var names = productIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Products.AsNoTracking()
                .Where(p => p.OrgId == orgId && productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return Results.Json(new
        {
            items = rows.Select(r =>
            {
                var taken = takenBy.GetValueOrDefault(r.Id);
                var paid = paidBy.GetValueOrDefault(r.Id);
                return Map(r, taken, paid, config, env, r.ProductId is not null && names.TryGetValue(r.ProductId, out var name) ? name : null);
            }),
            next_cursor = next
        }, OneClient.Json);
    }

    static PaymentLinkView Map(
        PaymentLinkRow row,
        int taken,
        int paid,
        IConfiguration config,
        IHostEnvironment env,
        string? label = null)
    {
        return new PaymentLinkView
        {
            Id = row.Id,
            OrgId = row.OrgId,
            Provider = row.Provider,
            Amount = row.Amount,
            Currency = row.Currency,
            Status = PaymentLinkOccupancy.MerchantStatus(row.MaxPayers, taken),
            PublicToken = row.PublicToken,
            PayUrl = CheckoutUrls.Pay(row.PublicToken, config, env),
            CreatedAt = row.CreatedAt,
            MaxPayers = row.MaxPayers,
            Unlimited = row.MaxPayers is null,
            PaidCount = paid,
            TakenCount = taken,
            Remaining = PaymentLinkOccupancy.RemainingUnclamped(row.MaxPayers, taken),
            Label = label
        };
    }
}

public sealed class PaymentLinkView
{
    public required string Id { get; init; }
    public required string OrgId { get; init; }
    public required string Provider { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public required string PublicToken { get; init; }
    public string? PayUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public int? MaxPayers { get; init; }
    public required bool Unlimited { get; init; }
    public required int PaidCount { get; init; }
    public required int TakenCount { get; init; }
    public int? Remaining { get; init; }
    public string? Label { get; init; }
}
