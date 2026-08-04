using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Commerce.Infrastructure;

public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicCommerceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{tenantSlug}/products/{slug}", async Task<Results<Ok<ProductDto>, NotFound>> (
            string tenantSlug,
            string slug,
            [FromServices] IServiceProvider serviceProvider,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var connectionFactory = serviceProvider.GetRequiredKeyedService<ISqlConnectionFactory>("CommerceSqlConnectionFactory");
            using var connection = connectionFactory.CreateConnection();
            
            var productQuery = "SELECT \"Id\" FROM commerce.\"Products\" WHERE \"OrganizationId\" = @OrgId AND \"Slug\" = @Slug AND \"IsActive\" = true LIMIT 1";
            var productId = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<Guid?>(connection, productQuery, new { OrgId = tenantId.Value, Slug = slug });

            if (!productId.HasValue) return TypedResults.NotFound();

            var product = await queryService.GetProductByIdAsync(tenantId.Value, productId.Value);
            return product != null ? TypedResults.Ok(product) : TypedResults.NotFound();
        });

        group.MapGet("/{tenantSlug}/validate-coupon", async Task<Results<Ok<ValidateCouponResponseDto>, NotFound>> (
            string tenantSlug,
            [FromQuery] string code,
            [FromQuery] string product_slug,
            IOneQueryService oneQueryService,
            IMediator mediator) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            try
            {
                var query = new ValidateCouponQuery(tenantId.Value, product_slug, code);
                var result = await mediator.Send(query);
                return TypedResults.Ok(result);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is BusinessRuleValidationException)
            {
                return TypedResults.Ok(new ValidateCouponResponseDto
                {
                    Is_valid = false,
                    Discount_amount = 0,
                    Final_price = 0,
                    Error_message = ex.Message
                });
            }
        });

        group.MapGet("/{tenantSlug}/portal", async Task<Results<Ok<AggregatedPortalDataResponse>, NotFound, UnauthorizedHttpResult>> (
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

            var portalData = await queryService.GetPortalDataAsync(tenantId.Value, subId.Value);
            if (portalData == null) return TypedResults.NotFound();

            return TypedResults.Ok(portalData);
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
                await mediator.Send(new CancelPortalSubscriptionCommand(tenantSlug, token, subscriptionId));
                return TypedResults.Ok(new StatusResponse { Status = "canceled" });
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

        group.MapPost("/checkout", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>>> (
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
                parsedSessionId
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

        group.MapGet("/{tenantSlug}/custom-checkouts/{sessionId:guid}", async Task<Results<Ok<CustomCheckoutDto>, NotFound>> (
            string tenantSlug,
            Guid sessionId,
            IOneQueryService oneQueryService,
            ICommerceQueryService queryService,
            IConfiguration config,
            HttpContext httpContext) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            httpContext.Items["TenantId"] = tenantId.Value;

            var checkout = await queryService.GetCustomCheckoutBySessionIdAsync(tenantId.Value, sessionId);
            if (checkout == null) return TypedResults.NotFound();

            var secret = DocumentLinkSigner.ResolveSecret(config["Jwt:Secret"]);
            var exp = DocumentLinkSigner.ExpiryUnixSeconds(TimeSpan.FromDays(7));
            var payload = DocumentLinkSigner.DraftDocumentPayload(tenantSlug, sessionId, exp);
            var sig = DocumentLinkSigner.Sign(secret, payload);
            var apiBaseUrl = config["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
            checkout.Draft_pdf_url = $"{apiBaseUrl}/public/billing/{tenantSlug}/documents/draft/{sessionId}?sig={sig}&exp={exp}";

            return TypedResults.Ok(checkout);
        });

        group.MapGet("/checkout/{subId:guid}/arrears", async Task<Results<Ok<ArrearsSummaryDto>, NotFound>> (
            Guid subId,
            [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlFactory) =>
        {
            using var connection = sqlFactory.CreateConnection();
            var query = @"
                SELECT p.""Name"" as ProductName, p.""Price"" as Amount, p.""Currency"", s.""Status""
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
                
            var result = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<ArrearsSummaryDto>(connection, query, new { SubId = subId });
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        group.MapPost("/checkout/{subId:guid}/update-payment", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>>> (
            Guid subId,
            [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
            IMediator mediator,
            IConfiguration config) =>
        {
            using var connection = sqlFactory.CreateConnection();
            var query = @"
                SELECT s.""OrganizationId"", s.""ProductId"", s.""Status"", s.""CurrentDunningCampaignId"",
                       p.""Name"" as ProductName, p.""Price"", p.""Currency"", p.""GatewayName"" as ProductGatewayName,
                       cp.""Email"" as CustomerEmail,
                       org.""Slug"" as TenantSlug
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
                JOIN one.""Organizations"" org ON s.""OrganizationId"" = org.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
                
            var sub = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<dynamic>(connection, query, new { SubId = subId });
            
            if (sub == null) return TypedResults.BadRequest("Subscription not found.");
            if (sub.Status != "PAST_DUE" && sub.Status != "SUSPENDED") return TypedResults.BadRequest("This subscription is currently active and does not require a payment update.");

            var clientUrl = config["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
            var successUrl = $"{clientUrl}/{sub.TenantSlug}/portal"; 
            var cancelUrl = $"{clientUrl}/{sub.TenantSlug}/update-payment/{subId}";

            var metadata = new Dictionary<string, string>
            {
                { "type", "commerce_subscription" },
                { "subscription_id", subId.ToString() },
                { "tenant_id", sub.OrganizationId.ToString() }
            };

            if (sub.CurrentDunningCampaignId != null)
            {
                metadata["dunning_campaign_id"] = sub.CurrentDunningCampaignId.ToString();
            }

            // Use the subscription product's gateway (not default BILLPLZ).
            string? productGateway = sub.ProductGatewayName as string;
            if (string.IsNullOrWhiteSpace(productGateway))
            {
                productGateway = null;
            }

            try
            {
                var checkoutQuery = new GenerateCheckoutSessionQuery(
                    (Guid)sub.OrganizationId,
                    (decimal)sub.Price,
                    (string)sub.Currency,
                    (string)sub.ProductName,
                    (string)sub.CustomerEmail,
                    successUrl,
                    cancelUrl,
                    metadata,
                    true, 
                    1,
                    productGateway
                );

                var checkoutUrl = await mediator.Send(checkoutQuery);
                return TypedResults.Ok(new CheckoutResponse { Url = checkoutUrl, Is_zero_amount_bypass = false });
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return group;
    }
}
