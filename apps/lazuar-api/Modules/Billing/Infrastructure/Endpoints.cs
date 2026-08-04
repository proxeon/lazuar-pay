using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Application.Queries;
using Modules.Payments.Contracts.Queries;
using Modules.One.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Billing.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/billing").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/billing");

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

        admin.MapGet("/credits", async Task<Ok<CreditBalanceDto>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var balance = await queryService.GetCreditBalanceWithHistoryAsync(ctx.TenantId);
            return TypedResults.Ok(balance);
        });

        admin.MapGet("/credits/packages", (
            ICreditCostService creditCostService) =>
        {
            var packages = creditCostService.GetPackages()
                .Select(p => new CreditPackageDto { Amount_myr = (double)p.AmountMyr, Credits = p.Credits })
                .ToList();
            return TypedResults.Ok(packages);
        });

        admin.MapPost("/credits/top-up", async Task<Results<Ok<TopUpResponseDto>, BadRequest<string>>> (
            CreateTopUpRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (req.Amount_myr < 50) return TypedResults.BadRequest("Minimum top-up amount is RM 50.");

            var metadata = new Dictionary<string, string>
            {
                { "type", "utility_credit_topup" },
                { "tenant_id", ctx.TenantId.ToString() }
            };

            var query = new GenerateSystemCheckoutSessionQuery(
                ctx.TenantId,
                (decimal)req.Amount_myr,
                "MYR",
                "Lazuar Utility Credits",
                "",
                req.Return_url,
                req.Return_url,
                metadata
            );

            try
            {
                var checkoutUrl = await mediator.Send(query);
                return TypedResults.Ok(new TopUpResponseDto { Checkout_url = checkoutUrl });
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        admin.MapGet("/profile", async Task<Results<Ok<TenantBillingProfileDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var profile = await queryService.GetBillingProfileAsync(ctx.TenantId);
            return profile != null ? TypedResults.Ok(profile) : TypedResults.NotFound();
        });

        admin.MapPut("/profile", async Task<Ok<StatusResponse>> (
            UpdateTenantBillingProfileRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new UpdateTenantBillingProfileCommand(
                ctx.TenantId,
                req.Legal_name,
                req.Tin,
                req.Registration_number,
                req.Sst_registration_number,
                req.Logo_url,
                req.Address
            );

            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        publicGroup.MapGet("/{tenantSlug}/profile", async Task<Results<Ok<TenantBillingProfileDto>, NotFound>> (
            string tenantSlug,
            IOneQueryService oneQueryService,
            IBillingQueryService queryService) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var profile = await queryService.GetBillingProfileAsync(tenantId.Value);
            return profile != null ? TypedResults.Ok(profile) : TypedResults.NotFound();
        });

        publicGroup.MapGet("/{tenantSlug}/documents/{ledgerEntryId:guid}", async Task<IResult> (
            string tenantSlug,
            Guid ledgerEntryId,
            [FromQuery] string? sig,
            [FromQuery] long exp,
            IConfiguration config,
            IOneQueryService oneQueryService,
            IR2StorageService r2Service) =>
        {
            var secret = DocumentLinkSigner.ResolveSecret(config["Jwt:Secret"]);
            var payload = DocumentLinkSigner.FinalDocumentPayload(tenantSlug, ledgerEntryId, exp);
            if (!DocumentLinkSigner.TryValidate(secret, payload, sig, exp, out var linkError))
            {
                return linkError is "This secure document link has expired."
                    ? TypedResults.BadRequest(linkError)
                    : TypedResults.Unauthorized();
            }

            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var key = $"vault/{tenantId.Value}/documents/{ledgerEntryId}.pdf";

            var downloadUrl = r2Service.GetPresignedDownloadUrl(bucket, key, 5);

            return TypedResults.Redirect(downloadUrl);
        });

        publicGroup.MapGet("/{tenantSlug}/documents/draft/{sessionId:guid}", async Task<IResult> (
            string tenantSlug,
            Guid sessionId,
            [FromQuery] string? sig,
            [FromQuery] long exp,
            IConfiguration config,
            IOneQueryService oneQueryService,
            IMediator mediator,
            HttpContext httpContext) =>
        {
            var secret = DocumentLinkSigner.ResolveSecret(config["Jwt:Secret"]);
            var payload = DocumentLinkSigner.DraftDocumentPayload(tenantSlug, sessionId, exp);
            if (!DocumentLinkSigner.TryValidate(secret, payload, sig, exp, out var linkError))
            {
                return linkError is "This secure document link has expired."
                    ? TypedResults.BadRequest(linkError)
                    : Results.Json(new { status = 401, title = "Unauthorized", detail = linkError ?? "Invalid document signature." },
                        statusCode: StatusCodes.Status401Unauthorized);
            }

            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            // Ensure ambient tenant for fail-closed EF filters (also set by middleware from route slug).
            httpContext.Items["TenantId"] = tenantId.Value;

            try
            {
                var query = new GenerateDraftDocumentQuery(tenantId.Value, sessionId);
                var pdfBytes = await mediator.Send(query);
                return Results.File(pdfBytes, "application/pdf", $"Proforma_{sessionId.ToString()[..8].ToUpperInvariant()}.pdf");
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return endpoints;
    }
}
