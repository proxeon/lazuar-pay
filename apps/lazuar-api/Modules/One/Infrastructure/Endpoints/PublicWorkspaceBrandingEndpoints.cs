using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure;

public static class PublicWorkspaceBrandingEndpoints
{
    public static IEndpointRouteBuilder MapPublicWorkspaceBrandingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/public/one").RequireCors();

        group.MapGet("/{tenantSlug}/branding", async Task<Results<Ok<PublicWorkspaceBrandingResponse>, NotFound>> (
            string tenantSlug,
            IOneQueryService queryService) =>
        {
            var branding = await queryService.GetPublicBrandingBySlugAsync(tenantSlug);
            if (branding == null) return TypedResults.NotFound();

            return TypedResults.Ok(new PublicWorkspaceBrandingResponse
            {
                Name = branding.Name,
                Slug = branding.Slug,
                Logo_url = branding.LogoUrl,
                Primary_color = branding.PrimaryColor
            });
        });

        return endpoints;
    }
}

public sealed class PublicWorkspaceBrandingResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("logo_url")]
    public string? Logo_url { get; set; }

    [JsonPropertyName("primary_color")]
    public string? Primary_color { get; set; }
}
