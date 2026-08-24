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
        return Results.Json(rows.Select(c => new
        {
            id = c.Id,
            org_id = c.OrgId,
            checkout_id = c.CheckoutId,
            amount = c.Amount,
            currency = c.Currency,
            status = c.Status
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
        var rows = await db.Documents.AsNoTracking().Where(d => d.OrgId == orgId).ToListAsync(ct);
        return Results.Json(rows.Select(d => new
        {
            id = d.Id,
            org_id = d.OrgId,
            number = d.Number ?? "PENDING",
            title = d.Title,
            checkout_id = d.CheckoutId
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
