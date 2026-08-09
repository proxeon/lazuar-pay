using System;
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
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure;

public static class PublicProductEndpoints
{
    public static RouteGroupBuilder MapPublicProductEndpoints(this RouteGroupBuilder group)
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

        return group;
    }
}
