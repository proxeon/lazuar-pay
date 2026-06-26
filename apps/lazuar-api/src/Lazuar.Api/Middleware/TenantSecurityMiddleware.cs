using Microsoft.AspNetCore.Http;
using Modules.One.Contracts;
using System.Security.Claims;
using System.Text.Json;
using System;
using System.Threading.Tasks;

namespace Lazuar.Api.Middleware;

public class TenantSecurityMiddleware
{
    private readonly RequestDelegate _next;

    public TenantSecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOneQueryService oneQueryService)
    {
        if (context.User.Identity?.AuthenticationType == "ApiKey")
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/v1/platform"))
        {
            context.Items["TenantId"] = Guid.Parse("00000000-0000-0000-0000-000000000001");
            await _next(context);
            return;
        }

        Guid? resolvedTenantId = null;

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader) && Guid.TryParse(tenantIdHeader, out var parsedId))
        {
            resolvedTenantId = parsedId;
        }
        else if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var tenantSlugHeader))
        {
            resolvedTenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlugHeader.ToString());
        }
        else if (context.Request.RouteValues.TryGetValue("tenantSlug", out var routeSlug))
        {
            resolvedTenantId = await oneQueryService.GetTenantIdBySlugAsync(routeSlug!.ToString()!);
        }

        if (resolvedTenantId.HasValue)
        {
            context.Items["TenantId"] = resolvedTenantId.Value;

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    var role = await oneQueryService.GetTenantRoleAsync(userId, resolvedTenantId.Value);

                    if (string.IsNullOrEmpty(role))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var error = JsonSerializer.Serialize(new
                        {
                            status = 403,
                            title = "Forbidden",
                            detail = "You do not have access to this workspace. Please ensure your Lazuar One identity is authorized for this tenant."
                        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                        await context.Response.WriteAsync(error);
                        return;
                    }
                    else
                    {
                        var identity = context.User.Identity as ClaimsIdentity;
                        identity?.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                }
            }
        }

        await _next(context);
    }
}
