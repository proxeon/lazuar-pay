using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Dapper;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.Payments.Application.Commands;
using Modules.Payments.Application.Queries;

namespace Modules.Payments.Infrastructure;

public static class PlatformEndpoints
{
    private record GlobalUserDto(Guid Id, string Email, string Name, string PasswordHash, Guid SecurityStamp, bool IsSystemAdmin, bool IsEmailVerified, bool IsActive);

    public static RouteGroupBuilder MapPlatformEndpoints(this RouteGroupBuilder group)
    {
        // Platform Login - Bypasses group authorization
        group.MapPost("/auth/login", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> (
            [FromBody] LoginRequest req,
            [FromKeyedServices("OneSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
            IPasswordService passwordService,
            IJwtService jwtService,
            IConfiguration config,
            IWebHostEnvironment env,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password)) return TypedResults.Unauthorized();

            using var conn = sqlFactory.CreateConnection();
            var query = @"SELECT ""Id"", ""Email"", ""Name"", ""PasswordHash"", ""SecurityStamp"", ""IsSystemAdmin"", ""IsEmailVerified"", ""IsActive"" FROM one.""GlobalUsers"" WHERE ""Email"" = @Email LIMIT 1";
            var user = await conn.QuerySingleOrDefaultAsync<GlobalUserDto>(query, new { Email = email });

            // Critical Guard: Must be a System Admin
            if (user == null || !user.IsActive || !user.IsSystemAdmin || !passwordService.Verify(req.Password, user.PasswordHash))
            {
                return TypedResults.Unauthorized();
            }

            IssueAdminCookie(ctx, user, jwtService, config, env);

            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser { Email = user.Email, Name = user.Name, Role = "SUPER_ADMIN", Is_email_verified = user.IsEmailVerified }
            });
        }).AllowAnonymous();

        // Platform Logout - Bypasses group authorization
        group.MapPost("/auth/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete("lazuar_admin_auth", new CookieOptions { Path = "/api/v1/platform" });
            return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
        }).AllowAnonymous();

        // Platform GetMe - Requires SUPER_ADMIN
        group.MapGet("/auth/me", async Task<Results<Ok<AuthUser>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal, 
            [FromKeyedServices("OneSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
            HttpContext ctx) =>
        {
            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId)) return TypedResults.Unauthorized();

            using var conn = sqlFactory.CreateConnection();
            var query = @"SELECT ""Id"", ""Email"", ""Name"", ""SecurityStamp"", ""IsSystemAdmin"", ""IsEmailVerified"", ""IsActive"" FROM one.""GlobalUsers"" WHERE ""Id"" = @Id LIMIT 1";
            var user = await conn.QuerySingleOrDefaultAsync<GlobalUserDto>(query, new { Id = userId });

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

        group.MapGet("/payment-config", async Task<Results<Ok<PaymentConfigDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            // Middleware implicitly sets TenantId to SystemTenantId (0000...001)
            var query = new GetPaymentConfigQuery(ctx.TenantId);
            var config = await mediator.Send(query);
            return config != null ? TypedResults.Ok(config) : TypedResults.NotFound();
        });

        group.MapPut("/payment-config", async Task<Ok<StatusResponse>> (
            SavePaymentConfigRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            // Middleware implicitly sets TenantId to SystemTenantId (0000...001)
            var command = new UpdatePaymentConfigCommand(
                ctx.TenantId, req.Gateway_type, req.Api_key, req.Collection_id, req.Webhook_secret, req.Secret_key, req.Is_active);
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        return group;
    }

    private static void IssueAdminCookie(HttpContext ctx, GlobalUserDto user, IJwtService jwtService, IConfiguration config, IWebHostEnvironment env)
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

        // Scope the cookie strictly to the platform routes to prevent collision with tenant cookies
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
