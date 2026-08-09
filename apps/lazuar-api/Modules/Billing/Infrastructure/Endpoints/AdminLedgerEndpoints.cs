using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;

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

        admin.MapGet("/ledger/{id:guid}/document", Task<Ok<DocumentDownloadUrlDto>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var key = $"vault/{ctx.TenantId}/documents/{id}.pdf";

            var downloadUrl = r2Service.GetPresignedDownloadUrl(bucket, key, 5);
            return Task.FromResult(TypedResults.Ok(new DocumentDownloadUrlDto { Url = downloadUrl }));
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
