using System;
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
using Modules.Community.Application;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
using Modules.One.Contracts;

namespace Modules.Community.Infrastructure;

public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{tenantSlug}/plans", async Task<Results<Ok<ICollection<CommunityPlanDto>>, NotFound>> (
            string tenantSlug,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            return TypedResults.Ok((ICollection<CommunityPlanDto>)plans.ToList());
        });

        group.MapGet("/{tenantSlug}/plans/{slug}", async Task<Results<Ok<CommunityPlanDto>, NotFound>> (
            string tenantSlug,
            string slug,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            var plan = plans.FirstOrDefault(p => p.Slug == slug);
            return plan != null ? TypedResults.Ok(plan) : TypedResults.NotFound();
        });

        group.MapGet("/{tenantSlug}/validate-coupon", async Task<Results<Ok<ValidateCouponResponseDto>, NotFound>> (
            string tenantSlug,
            [FromQuery] string code,
            [FromQuery] string plan_slug,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService,
            IMediator mediator) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive)
                return TypedResults.NotFound();

            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            var plan = plans.FirstOrDefault(p => p.Slug == plan_slug);
            if (plan == null)
                return TypedResults.NotFound();

            var query = new ValidatePublicCouponQuery(tenant.Id, Guid.Parse(plan.Id), code);
            var result = await mediator.Send(query);

            return TypedResults.Ok(new ValidateCouponResponseDto
            {
                Is_valid = result.IsValid,
                Discount_amount = (double)result.DiscountAmount,
                Final_price = (double)result.FinalPrice,
                Error_message = result.ErrorMessage
            });
        });

        group.MapPost("/checkout", async Task<Results<Ok<CheckoutResponse>, NotFound>> (
            PublicCheckoutRequestDto req,
            IOneQueryService oneQueryService,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(req.Tenant_slug);
            if (tenant == null || !tenant.IsActive)
                return TypedResults.NotFound();

            var globalUserId = (ctx.UserId != Guid.Empty && req.Is_guest_checkout != true)
                ? ctx.UserId
                : (Guid?)null;

            var command = new RegisterPublicSubscriberCommand(
                tenant.Id,
                req.Tenant_slug,
                req.Plan_slug,
                req.Name,
                req.Email,
                req.Phone,
                globalUserId,
                req.Coupon_code);

            var checkoutUrl = await mediator.Send(command);

            var isBypass = checkoutUrl.EndsWith("/success");

            return TypedResults.Ok(new CheckoutResponse 
            { 
                Url = checkoutUrl,
                Is_zero_amount_bypass = isBypass
            });
        });

        group.MapPost("/{tenantSlug}/portal/magic-link", async Task<Results<Ok<StatusResponse>, NotFound>> (
            string tenantSlug,
            [FromBody] MagicLinkRequestDto req,
            HttpRequest httpReq,
            IOneQueryService oneQueryService,
            IMediator mediator) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var baseUrl = $"{httpReq.Scheme}://{httpReq.Host}";
            var command = new RequestMagicLinkCommand(tenant.Id, tenantSlug, req.Email, baseUrl);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "sent" });
        });

        group.MapGet("/{tenantSlug}/portal", async Task<Results<Ok<PortalDataResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var query = new GetPortalSubscriptionQuery(tenant.Id, subId.Value);
            var sub = await mediator.Send(query);
            if (sub == null) return TypedResults.Unauthorized();
            return TypedResults.Ok(new PortalDataResponse { Subscription = sub });
        });

        group.MapPost("/{tenantSlug}/portal/cancel", async Task<Results<Ok<StatusResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            [FromBody] CancelPortalRequest req,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            if (Guid.Parse(req.Subscription_id) != subId.Value) return TypedResults.Unauthorized();
            var command = new CancelSubscriptionCommand(tenant.Id, subId.Value);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "cancelled" });
        });

        group.MapGet("/{tenantSlug}/portal/billing-link", async Task<Results<Ok<BillingLinkResponseDto>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            HttpRequest httpReq,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            
            var baseUrl = $"{httpReq.Scheme}://{httpReq.Host}/{tenantSlug}/portal?token={token}";
            var query = new GetPortalBillingLinkQuery(tenant.Id, subId.Value, baseUrl);
            var url = await mediator.Send(query);
            return TypedResults.Ok(new BillingLinkResponseDto { Url = url });
        });

        return group;
    }
}
