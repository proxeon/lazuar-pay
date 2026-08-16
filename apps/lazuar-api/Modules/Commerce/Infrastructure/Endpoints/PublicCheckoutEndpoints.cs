using System;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure;

public static class PublicCheckoutEndpoints
{
    public static RouteGroupBuilder MapPublicCheckoutEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/checkout", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>, Conflict<string>>> (
            [FromBody] PublicCheckoutRequestDto req,
            IOneQueryService oneQueryService,
            IMediator mediator,
            HttpContext httpContext) =>
        {
            Guid? parsedSessionId = null;
            if (!string.IsNullOrWhiteSpace(req.Session_id) && Guid.TryParse(req.Session_id, out var sid))
            {
                parsedSessionId = sid;
            }

            // Bind ambient tenant before EF (fail-closed) — no tenantSlug route value on this path.
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(req.Tenant_slug);
            if (tenantId.HasValue)
            {
                httpContext.Items["TenantId"] = tenantId.Value;
            }

            var idempotencyKey = httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var header)
                ? header.ToString()
                : null;

            var command = new InitiateCheckoutCommand(
                req.Tenant_slug,
                req.Product_slug,
                req.Name,
                req.Email,
                req.Phone,
                req.Tax_id,
                req.Company_name,
                req.Address_line1,
                req.City,
                req.Postal_code,
                req.State_code,
                req.Country_code,
                req.Quantity ?? 1,
                req.Is_guest_checkout ?? false,
                req.Coupon_code,
                parsedSessionId,
                req.Metadata,
                idempotencyKey,
                req.Id_type,
                req.Id_value
            );

            try
            {
                var result = await mediator.Send(command);

                var response = new CheckoutResponse
                {
                    Url = result.Url,
                    Is_zero_amount_bypass = result.IsZeroAmountBypass
                };

                return TypedResults.Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("IDEMPOTENCY_CONFLICT", StringComparison.Ordinal))
            {
                return TypedResults.Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        });

        // Preferred: tenant-bound checkout status (no magic token mint).
        group.MapGet("/{tenantSlug}/checkout/{sessionId}/status", async Task<Results<Ok<CheckoutStatusResponse>, NotFound>> (
            string tenantSlug,
            string sessionId,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService,
            HttpContext httpContext) =>
        {
            if (!Guid.TryParse(sessionId, out var parsedSessionId))
            {
                return TypedResults.NotFound();
            }

            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            httpContext.Items["TenantId"] = tenantId.Value;

            var result = await queryService.GetCheckoutStatusAsync(tenantId.Value, parsedSessionId);
            if (result == null)
            {
                return TypedResults.NotFound();
            }

            var response = new CheckoutStatusResponse
            {
                Status = result.Status,
                Token = null
            };

            return TypedResults.Ok(response);
        });

        // Legacy path: still requires tenant_slug query; never mints portal tokens.
        group.MapGet("/checkout/{subId}/status", async Task<Results<Ok<CheckoutStatusResponse>, NotFound, BadRequest<string>>> (
            string subId,
            [FromQuery] string? tenant_slug,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService,
            HttpContext httpContext) =>
        {
            if (!Guid.TryParse(subId, out var parsedSessionId))
            {
                return TypedResults.NotFound();
            }

            if (string.IsNullOrWhiteSpace(tenant_slug))
            {
                return TypedResults.BadRequest("tenant_slug query parameter is required.");
            }

            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenant_slug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            httpContext.Items["TenantId"] = tenantId.Value;

            var result = await queryService.GetCheckoutStatusAsync(tenantId.Value, parsedSessionId);
            if (result == null)
            {
                return TypedResults.NotFound();
            }

            var response = new CheckoutStatusResponse
            {
                Status = result.Status,
                Token = null
            };

            return TypedResults.Ok(response);
        });

        return group;
    }
}
