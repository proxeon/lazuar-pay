using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Infrastructure.Services;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure;

public static class PublicPortalEndpoints
{
    public static RouteGroupBuilder MapPublicPortalEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{tenantSlug}/portal", async Task<Results<Ok<AggregatedPortalDataResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService,
            IMagicLinkTokenService tokenService,
            PortalDocumentQueryService portalDocuments) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var portalData = await queryService.GetPortalDataAsync(tenantId.Value, subId.Value);
            if (portalData == null) return TypedResults.NotFound();

            await portalDocuments.ListForBuyerAsync(tenantId.Value, subId.Value, tenantSlug);
            portalDocuments.AttachLatestToSubscriptions(portalData);

            return TypedResults.Ok(portalData);
        });

        group.MapGet("/{tenantSlug}/portal/documents", async Task<Results<Ok<PortalDocumentsResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            PortalDocumentQueryService portalDocuments) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var documents = await portalDocuments.ListForBuyerAsync(tenantId.Value, subId.Value, tenantSlug);
            return TypedResults.Ok(documents);
        });

        group.MapPost("/{tenantSlug}/portal/magic-link", async Task<Ok<StatusResponse>> (
            string tenantSlug,
            [FromBody] RequestPortalMagicLinkRequest? body,
            IMediator mediator) =>
        {
            // Always 200. Existing public-route throttle (if configured) is the only rate limit.
            await mediator.Send(new RequestPortalMagicLinkCommand(tenantSlug, body?.Email ?? ""));
            return TypedResults.Ok(new StatusResponse { Status = "ok" });
        });

        group.MapPost("/{tenantSlug}/portal/cancel", async Task<Results<Ok<StatusResponse>, BadRequest<string>, UnauthorizedHttpResult, NotFound>> (
            string tenantSlug,
            [FromQuery] string token,
            [FromBody] CancelPortalRequest body,
            IMediator mediator) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return TypedResults.Unauthorized();
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Subscription_id) || !Guid.TryParse(body.Subscription_id, out var subscriptionId))
            {
                return TypedResults.BadRequest("subscription_id is required and must be a valid GUID.");
            }

            try
            {
                var status = await mediator.Send(new CancelPortalSubscriptionCommand(
                    tenantSlug,
                    token,
                    subscriptionId,
                    body.At_period_end ?? true));
                return TypedResults.Ok(new StatusResponse { Status = status });
            }
            catch (UnauthorizedAccessException)
            {
                return TypedResults.Unauthorized();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound();
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        });

        group.MapPost("/{tenantSlug}/portal/keep", async Task<Results<Ok<StatusResponse>, BadRequest<string>, UnauthorizedHttpResult, NotFound>> (
            string tenantSlug,
            [FromQuery] string token,
            [FromBody] KeepPortalRequest body,
            IMediator mediator) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return TypedResults.Unauthorized();
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Subscription_id) || !Guid.TryParse(body.Subscription_id, out var subscriptionId))
            {
                return TypedResults.BadRequest("subscription_id is required and must be a valid GUID.");
            }

            try
            {
                await mediator.Send(new KeepPortalSubscriptionCommand(tenantSlug, token, subscriptionId));
                return TypedResults.Ok(new StatusResponse { Status = "kept" });
            }
            catch (UnauthorizedAccessException)
            {
                return TypedResults.Unauthorized();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound();
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        });

        return group;
    }
}
