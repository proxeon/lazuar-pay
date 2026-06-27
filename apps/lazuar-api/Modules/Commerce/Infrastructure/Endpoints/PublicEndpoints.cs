using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;
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

        return group;
    }
}
