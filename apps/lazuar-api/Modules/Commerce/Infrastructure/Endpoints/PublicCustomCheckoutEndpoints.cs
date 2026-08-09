using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Application.Queries;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure;

public static class PublicCustomCheckoutEndpoints
{
    public static RouteGroupBuilder MapPublicCustomCheckoutEndpoints(this RouteGroupBuilder group)
    {
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

        return group;
    }
}
