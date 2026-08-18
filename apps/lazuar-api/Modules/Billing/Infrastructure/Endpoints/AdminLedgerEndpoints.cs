using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;

namespace Modules.Billing.Infrastructure;

public static class AdminLedgerEndpoints
{
    public static RouteGroupBuilder MapAdminLedgerEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/ledger", async Task<Ok<PaginatedResponse<LedgerEntryDto>>> (
            [FromQuery] int? page,
            [FromQuery] int? limit,
            [FromQuery] string? search,
            [FromQuery] string? type_filter,
            [FromQuery] DateTime? from_date,
            [FromQuery] DateTime? to_date,
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var p = page ?? 1;
            var l = limit ?? 50;
            var response = await queryService.GetLedgerEntriesAsync(ctx.TenantId, p, l, search, type_filter, from_date, to_date);
            return TypedResults.Ok(response);
        });

        admin.MapGet("/ledger/{id:guid}/document", async Task<Results<Ok<DocumentDownloadUrlDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            BillingDbContext db,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            var exists = await db.LedgerEntries.AsNoTracking()
                .AnyAsync(e => e.Id == id && e.OrganizationId == ctx.TenantId);
            if (!exists)
            {
                return TypedResults.NotFound();
            }

            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var key = $"vault/{ctx.TenantId}/documents/{id}.pdf";

            var downloadUrl = r2Service.GetPresignedDownloadUrl(bucket, key, 5);
            return TypedResults.Ok(new DocumentDownloadUrlDto { Url = downloadUrl });
        });

        admin.MapPost("/ledger/{id:guid}/collect-buyer-tin", async Task<Results<Ok<StatusResponse>, NotFound, BadRequest<string>>> (
            Guid id,
            CollectBuyerTinRequest? body,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (body == null
                || string.IsNullOrWhiteSpace(body.Tin)
                || string.IsNullOrWhiteSpace(body.Id_type)
                || string.IsNullOrWhiteSpace(body.Id_value)
                || string.IsNullOrWhiteSpace(body.Company_name)
                || string.IsNullOrWhiteSpace(body.Email))
            {
                return TypedResults.BadRequest("tin, id_type, id_value, company_name, and email are required.");
            }

            try
            {
                await mediator.Send(new CollectBuyerTinForLargeB2cCommand(
                    ctx.TenantId,
                    id,
                    body.Tin,
                    body.Id_type,
                    body.Id_value,
                    body.Company_name,
                    body.Full_name ?? body.Company_name,
                    body.Email));
                return TypedResults.Ok(new StatusResponse { Status = "submitted" });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        admin.MapGet("/summary", async Task<Ok<FinancialSummaryDto>> (
            [FromQuery] DateTime? from_date,
            [FromQuery] DateTime? to_date,
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var summary = await queryService.GetFinancialSummaryAsync(ctx.TenantId, from_date, to_date);
            return TypedResults.Ok(summary);
        });

        admin.MapGet("/net-profit", async Task<Ok<IReadOnlyList<NetProfitDto>>> (
            [FromQuery] string? period,
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var rows = await queryService.GetNetProfitAsync(ctx.TenantId, period ?? "monthly");
            return TypedResults.Ok(rows);
        });

        return admin;
    }
}

public sealed class CollectBuyerTinRequest
{
    public string? Tin { get; set; }
    public string? Id_type { get; set; }
    public string? Id_value { get; set; }
    public string? Company_name { get; set; }
    public string? Full_name { get; set; }
    public string? Email { get; set; }
}
