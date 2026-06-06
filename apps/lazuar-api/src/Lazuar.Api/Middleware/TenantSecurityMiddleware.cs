using Microsoft.AspNetCore.Http;
using Modules.One.Contracts;
using System.Security.Claims;
using System.Text.Json;

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
        Guid? resolvedTenantId = null;

        // 1. Resolve Tenant Context from Request Header (X-Tenant-Id)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader) && Guid.TryParse(tenantIdHeader, out var parsedId))
        {
            resolvedTenantId = parsedId;
        }
        // 2. Resolve Tenant Context from Request Header (X-Tenant-Slug)
        else if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var tenantSlugHeader))
        {
            resolvedTenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlugHeader.ToString());
        }
        // 3. Resolve Tenant Context from Route values (e.g. /public/community/{tenantSlug}/plans)
        else if (context.Request.RouteValues.TryGetValue("tenantSlug", out var routeSlug))
        {
            resolvedTenantId = await oneQueryService.GetTenantIdBySlugAsync(routeSlug!.ToString()!);
        }

        // 4. Validate Authorization
        if (resolvedTenantId.HasValue)
        {
            // Store the resolved ID so the rest of the application (DB Contexts) knows which tenant we are querying
            context.Items["TenantId"] = resolvedTenantId.Value;

            // If the user is authenticated, we MUST verify they belong to this Tenant
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isSystemAdmin = context.User.FindFirst("is_system_admin")?.Value == "true";

                // System Admins bypass the local membership check (God-Mode)
                if (Guid.TryParse(userIdClaim, out var userId) && !isSystemAdmin)
                {
                    var hasAccess = await oneQueryService.HasTenantAccessAsync(userId, resolvedTenantId.Value);
                    
                    if (!hasAccess)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        
                        var error = JsonSerializer.Serialize(new { 
                            status = 403, 
                            title = "Forbidden", 
                            detail = "You do not have access to this workspace. Please ensure your Lazuar One identity is authorized for this tenant." 
                        });
                        
                        await context.Response.WriteAsync(error);
                        return; // Halt request pipeline
                    }
                }
            }
        }

        await _next(context);
    }
}
