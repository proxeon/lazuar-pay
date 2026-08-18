using Microsoft.AspNetCore.Http;
using Modules.One.Contracts;
using System.Security.Claims;
using System.Text.Json;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

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
        // API keys already bind TenantId in ApiKeyAuthenticationMiddleware.
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

        // Public / webhook / auth: still bind ambient tenant when slug/header is present (EF fail-closed),
        // but never require it and never inject membership roles for anonymous storefronts.
        var isExempt = IsTenantExemptPath(context.Request.Path);

        if (!isExempt
            && RequiresTenantContext(context.Request.Path)
            && (!resolvedTenantId.HasValue || resolvedTenantId.Value == Guid.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing Tenant Context",
                Detail = "Missing Tenant Context Header. X-Tenant-Id is required for this route."
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
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

                    var isSystemAdmin = string.Equals(
                        context.User.FindFirst("is_system_admin")?.Value,
                        "true",
                        StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrEmpty(role))
                    {
                        var identity = context.User.Identity as ClaimsIdentity;
                        identity?.AddClaim(new Claim(ClaimTypes.Role, role));
                    }
                    else if (isSystemAdmin)
                    {
                        var identity = context.User.Identity as ClaimsIdentity;
                        identity?.AddClaim(new Claim(ClaimTypes.Role, "ADMIN"));
                    }
                    else if (!isExempt)
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
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Paths that must not require X-Tenant-Id (public storefronts, auth, inbound webhooks).
    /// </summary>
    public static bool IsTenantExemptPath(PathString path)
    {
        if (path.StartsWithSegments("/health"))
        {
            return true;
        }

        if (path.StartsWithSegments("/api/v1/public"))
        {
            return true;
        }

        if (path.StartsWithSegments("/api/v1/webhooks"))
        {
            return true;
        }

        // One: public register + auth + workspace list/create (no ambient tenant yet).
        // Integrator provision creates a tenant — must not require X-Tenant-Id.
        if (path.StartsWithSegments("/api/v1/one/public")
            || path.StartsWithSegments("/api/v1/one/auth")
            || path.StartsWithSegments("/api/v1/one/me")
            || path.StartsWithSegments("/api/v1/one/workspaces")
            || path.StartsWithSegments("/api/v1/one/integrations/workspaces"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Org-scoped modules and One surfaces that require ambient tenant (JWT header/slug).
    /// </summary>
    public static bool RequiresTenantContext(PathString path)
    {
        if (path.StartsWithSegments("/api/v1/admin")
            || path.StartsWithSegments("/api/v1/lhdn")
            || path.StartsWithSegments("/api/v1/ops")
            || path.StartsWithSegments("/api/v1/messaging"))
        {
            return true;
        }

        // One tenant-scoped (storage vault keys, platform API credentials).
        if (path.StartsWithSegments("/api/v1/one/storage")
            || path.StartsWithSegments("/api/v1/one/api-keys"))
        {
            return true;
        }

        return false;
    }
}
