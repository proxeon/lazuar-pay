using System.Security.Claims;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; 
using Modules.One.Application.Commands;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/one").RequireCors();

        group.MapPost("/auth/login", async Task<Results<Ok<LoginResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] LoginRequest req,
            IConfiguration config,
            IJwtService jwtService,
            IPasswordService passwordService,
            OneDbContext db,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Email and password are required." });

            // Hardcoded developer backdoor for rapid testing
            if ((email == "admin@lazuars.io" || email == "sysadmin@lazuars.io") && req.Password == "Password123!")
            {
                IssueCookie(ctx, "018f3a3f-3610-73bf-baef-c07a3c3df9ee", email, true, config);
                return TypedResults.Ok(new LoginResponse { User = new AuthUser { Email = email, Name = "Administrator", Role = "SUPER_ADMIN", Is_system_admin = true } });
            }

            var user = await db.GlobalUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.IsActive || !passwordService.Verify(req.Password, user.PasswordHash))
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 401, Detail = "Invalid email or password." });

            IssueCookie(ctx, user.Id.ToString(), user.Email, user.IsSystemAdmin, config);

            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser { Email = user.Email, Name = "User", Role = user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT", Is_system_admin = user.IsSystemAdmin }
            });
        });

        group.MapPost("/auth/logout", (HttpContext ctx) => 
        {
            ctx.Response.Cookies.Delete("lazuar_auth");
            return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
        });

        group.MapGet("/auth/me", Results<Ok<AuthUser>, UnauthorizedHttpResult> (ClaimsPrincipal principal) =>
        {
            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            var isSystemAdmin = principal.FindFirst("is_system_admin")?.Value == "true";
            if (string.IsNullOrEmpty(email)) return TypedResults.Unauthorized();

            return TypedResults.Ok(new AuthUser { Email = email, Name = "User", Role = isSystemAdmin ? "SUPER_ADMIN" : "CLIENT", Is_system_admin = isSystemAdmin });
        }).RequireAuthorization();

        group.MapGet("/workspaces", async Task<Results<Ok<ICollection<WorkspaceDto>>, UnauthorizedHttpResult>> (
            IExecutionContextAccessor ctx,
            IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty || !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            
            var workspaces = await queryService.GetWorkspacesAsync();
            var dtos = workspaces.Select(w => new WorkspaceDto 
            { 
                Id = w.Id.ToString(), 
                Name = w.Name, 
                Slug = w.Slug, 
                Is_active = w.IsActive, 
                Created_at = new DateTimeOffset(w.CreatedAt) 
            }).ToList();
            
            return TypedResults.Ok((ICollection<WorkspaceDto>)dtos);
        }).RequireAuthorization();

        group.MapPost("/workspaces", async Task<Results<Ok<IdResponse>, UnauthorizedHttpResult, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] CreateWorkspaceRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty || !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            
            var id = await mediator.Send(new CreateWorkspaceCommand(ctx.UserId, req.Name, req.Slug));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        }).RequireAuthorization();

        group.MapGet("/me/entitlements", async Task<Results<Ok<ICollection<EntitlementDto>>, UnauthorizedHttpResult>> (
            IExecutionContextAccessor ctx,
            OneDbContext db) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            var entitlements = await db.TenantMemberships
                .IgnoreQueryFilters()
                .Where(m => m.GlobalUserId == ctx.UserId)
                .Join(db.Organizations.IgnoreQueryFilters(), 
                      m => m.OrganizationId, 
                      o => o.Id, 
                      (m, o) => new EntitlementDto 
                      { 
                          Workspace_id = o.Id.ToString(), 
                          Workspace_name = o.Name, 
                          Workspace_slug = o.Slug, 
                          Role = m.Role 
                      })
                .ToListAsync();

            return TypedResults.Ok((ICollection<EntitlementDto>)entitlements);
        }).RequireAuthorization();

        return endpoints;
    }

    private static void IssueCookie(HttpContext ctx, string userId, string email, bool isSystemAdmin, IConfiguration config)
    {
        var jwtService = ctx.RequestServices.GetRequiredService<IJwtService>();
        var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
        var issuer = config["Jwt:Issuer"] ?? "lazuar-api";
        var audience = config["Jwt:Audience"] ?? "lazuar-clients";
        var expiryHours = config.GetValue<int>("Jwt:ExpiryHours", 24);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, isSystemAdmin ? "SUPER_ADMIN" : "CLIENT"),
            new Claim("is_system_admin", isSystemAdmin ? "true" : "false")
        };

        var token = jwtService.GenerateToken(claims, secret, issuer, audience, expiryHours);
        
        var isDev = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Lax,
            Domain = isDev ? null : ".lazuar.com",
            Expires = DateTime.UtcNow.AddHours(expiryHours)
        };
        
        ctx.Response.Cookies.Append("lazuar_auth", token, cookieOptions);
    }
}
