using System;
using System.Collections.Generic;
using System.Linq;
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
using Modules.Commerce.Infrastructure.Security;
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

        group.MapPost("/{tenantSlug}/portal/magic-link", async Task<IResult> (
            string tenantSlug,
            [FromBody] RequestPortalMagicLinkRequest? body,
            IMediator mediator,
            HttpContext http,
            PortalMagicLinkRateLimiter rateLimiter) =>
        {
            var key = PortalMagicLinkRateLimiter.ClientKey(http, body?.Email);
            if (!await rateLimiter.TryAcquireAsync(key, http.RequestAborted))
            {
                http.Response.Headers.RetryAfter = "600";
                return Results.Json(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many magic-link requests. Retry later."
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            // Always 200 when under budget — do not reveal whether the email exists.
            await mediator.Send(new RequestPortalMagicLinkCommand(tenantSlug, body?.Email ?? ""));
            return TypedResults.Ok(new StatusResponse { Status = "ok" });
        });

        group.MapGet("/{tenantSlug}/portal/plans", async Task<Results<Ok<ICollection<PortalPlanDto>>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService,
            IMagicLinkTokenService tokenService) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var plans = await queryService.GetPortalPlansAsync(tenantId.Value, subId.Value);
            return TypedResults.Ok((ICollection<PortalPlanDto>)plans.ToList());
        });

        group.MapPost("/{tenantSlug}/portal/change-plan", async Task<Results<Ok<PlanChangePreviewDto>, BadRequest<string>, UnauthorizedHttpResult, NotFound>> (
            string tenantSlug,
            [FromQuery] string token,
            [FromBody] PortalChangePlanRequest body,
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

            Guid? productId = null;
            if (!string.IsNullOrWhiteSpace(body.Product_id))
            {
                if (!Guid.TryParse(body.Product_id, out var parsed))
                {
                    return TypedResults.BadRequest("product_id must be a valid GUID.");
                }

                productId = parsed;
            }

            try
            {
                var preview = await mediator.Send(new ChangePortalPlanCommand(
                    tenantSlug,
                    token,
                    subscriptionId,
                    productId,
                    body.Prorate,
                    body.Apply));
                return TypedResults.Ok(SubscriberEndpoints.MapPreview(preview));
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
