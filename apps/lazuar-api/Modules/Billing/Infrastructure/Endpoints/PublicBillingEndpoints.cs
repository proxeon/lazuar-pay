using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Application.Queries;
using Modules.Billing.Contracts;
using Modules.One.Contracts;

namespace Modules.Billing.Infrastructure;

public static class PublicBillingEndpoints
{
    public static RouteGroupBuilder MapPublicBillingEndpoints(this RouteGroupBuilder publicGroup)
    {
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

        return publicGroup;
    }
}
