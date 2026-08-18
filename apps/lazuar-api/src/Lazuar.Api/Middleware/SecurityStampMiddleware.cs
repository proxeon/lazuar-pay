using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Modules.One.Application;
using Modules.One.Infrastructure.Services;

namespace Lazuar.Api.Middleware;

public class SecurityStampMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityStampMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOneRepository users)
    {
        if (context.User.Identity?.AuthenticationType == "ApiKey")
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var stampClaim = context.User.FindFirst("security_stamp")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(stampClaim))
        {
            await _next(context);
            return;
        }

        var user = await users.GetUserByIdAsync(userId, context.RequestAborted);
        if (user != null && string.Equals(stampClaim, user.SecurityStamp.ToString(), StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/v1/platform"))
            AuthCookie.DeleteAdmin(context);
        else
            AuthCookie.DeleteMerchant(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}
