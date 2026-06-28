using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;

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

        group.MapPost("/checkout", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>>> (
            [FromBody] PublicCheckoutRequestDto req,
            IMediator mediator) =>
        {
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
                req.Coupon_code
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

        group.MapGet("/checkout/{subId}/status", async Task<Results<Ok<CheckoutStatusResponse>, NotFound>> (
            string subId,
            ICommerceQueryService queryService) =>
        {
            if (!Guid.TryParse(subId, out var parsedSessionId))
            {
                return TypedResults.NotFound();
            }

            var result = await queryService.GetCheckoutStatusAsync(parsedSessionId);
            if (result == null)
            {
                return TypedResults.NotFound();
            }

            var response = new CheckoutStatusResponse
            {
                Status = result.Status,
                Token = result.Token
            };

            return TypedResults.Ok(response);
        });

        return group;
    }
}
