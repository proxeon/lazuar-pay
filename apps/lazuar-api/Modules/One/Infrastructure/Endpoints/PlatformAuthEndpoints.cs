// apps/lazuar-api/Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure;

/// <summary>
/// Platform super-admin cookie auth under host group <c>/api/v1/platform</c>.
/// Owns <c>lazuar_admin_auth</c>; payment-config routes stay on Payments.
/// </summary>
public static class PlatformAuthEndpoints
{
    public static RouteGroupBuilder MapPlatformAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/login", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> (
            [FromBody] LoginRequest req,
            IPlatformAdminAuthQuery adminAuthQuery,
            IPasswordService passwordService,
            IJwtService jwtService,
            IConfiguration config,
            IWebHostEnvironment env,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
            {
                return TypedResults.Unauthorized();
            }

            var user = await adminAuthQuery.GetSystemAdminByEmailAsync(email);

            if (user == null
                || !user.IsActive
                || !user.IsSystemAdmin
                || !passwordService.Verify(req.Password, user.PasswordHash))
            {
                return TypedResults.Unauthorized();
            }

            IssueAdminCookie(ctx, user, jwtService, config, env);

            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser
                {
                    Email = user.Email,
                    Name = user.Name,
                    Role = "SUPER_ADMIN",
                    Is_email_verified = user.IsEmailVerified
                }
            });
        }).AllowAnonymous();

        group.MapPost("/auth/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete("lazuar_admin_auth", new CookieOptions { Path = "/api/v1/platform" });
            return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
        }).AllowAnonymous();

        group.MapGet("/auth/me", async Task<Results<Ok<AuthUser>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            IPlatformAdminAuthQuery adminAuthQuery,
            HttpContext ctx) =>
        {
            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var user = await adminAuthQuery.GetSystemAdminByIdAsync(userId);

            if (user == null || !user.IsActive || !user.IsSystemAdmin)
            {
                ctx.Response.Cookies.Delete("lazuar_admin_auth", new CookieOptions { Path = "/api/v1/platform" });
                return TypedResults.Unauthorized();
            }

            var stampClaim = principal.FindFirst("security_stamp")?.Value;
            if (stampClaim != user.SecurityStamp.ToString())
            {
                ctx.Response.Cookies.Delete("lazuar_admin_auth", new CookieOptions { Path = "/api/v1/platform" });
                return TypedResults.Unauthorized();
            }

            return TypedResults.Ok(new AuthUser
            {
                Email = user.Email,
                Name = user.Name,
                Role = "SUPER_ADMIN",
                Is_email_verified = user.IsEmailVerified
            });
        });

        return group;
    }

    private static void IssueAdminCookie(
        HttpContext ctx,
        PlatformAdminLoginUserDto user,
        IJwtService jwtService,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
        var issuer = config["Jwt:Issuer"] ?? "lazuar-api";
        var audience = config["Jwt:Audience"] ?? "lazuar-clients";
        var expiryHours = config.GetValue<int>("Jwt:ExpiryHours", 24);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, "SUPER_ADMIN"),
            new Claim("is_system_admin", "true"),
            new Claim("is_email_verified", user.IsEmailVerified ? "true" : "false"),
            new Claim("security_stamp", user.SecurityStamp.ToString())
        };

        var token = jwtService.GenerateToken(claims, secret, issuer, audience, expiryHours);
        var isDev = env.IsDevelopment();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Lax,
            Domain = isDev ? null : ".lazuar.com",
            Path = "/api/v1/platform",
            Expires = DateTime.UtcNow.AddHours(expiryHours)
        };

        ctx.Response.Cookies.Append("lazuar_admin_auth", token, cookieOptions);
    }
}
