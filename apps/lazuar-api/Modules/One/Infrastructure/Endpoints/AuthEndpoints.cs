using System.Security.Claims;
using BuildingBlocks.Application;
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
using Modules.One.Application.Queries;
using Modules.One.Domain;
using Modules.One.Infrastructure.Services;

namespace Modules.One.Infrastructure;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/public/pricing", async Task<Ok<PublicPricingDto>> (IMediator mediator) =>
        {
            var pricing = await mediator.Send(new GetPublicPricingQuery());
            return TypedResults.Ok(pricing);
        });

        group.MapPost("/public/register", async Task<IResult> (
            [FromBody] PublicRegisterRequestDto req,
            IConfiguration config,
            OneDbContext db,
            IMediator mediator,
            HttpContext ctx,
            PublicRegisterRateLimiter rateLimiter) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
                throw new InvalidOperationException("Email and password are required.");

            if (string.IsNullOrEmpty(req.Workspace_name) || string.IsNullOrEmpty(req.Tenant_slug))
                throw new InvalidOperationException("Workspace name and slug are required.");

            if (req.Accepted_terms != true)
                throw new InvalidOperationException("You must accept the Terms of Service and Privacy Policy.");

            var clientKey = ResolveRegisterClientKey(ctx, email);
            if (!await rateLimiter.TryAcquireAsync(clientKey, ctx.RequestAborted))
            {
                ctx.Response.Headers.RetryAfter = "600";
                return Results.Json(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many signup attempts. Retry later."
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var userId = await mediator.Send(new RegisterPublicUserCommand(email, req.Password, req.Name, req.Workspace_name, req.Tenant_slug));
            var user = await db.GlobalUsers.FindAsync(userId);

            IssueCookie(ctx, user!, config);

            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser { Email = user!.Email, Name = user.Name, Role = "ADMIN", Is_email_verified = user.IsEmailVerified }
            });
        });

        group.MapPost("/auth/login", async Task<Results<Ok<LoginResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] LoginRequest req,
            IConfiguration config,
            IPasswordService passwordService,
            OneDbContext db,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Email and password are required." });

            var user = await db.GlobalUsers.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !user.IsActive || !passwordService.Verify(req.Password, user.PasswordHash))
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 401, Detail = "Invalid email or password." });
            }

            var role = user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT";

            IssueCookie(ctx, user, config);

            return TypedResults.Ok(new LoginResponse
            {
                User = new AuthUser { Email = user.Email, Name = user.Name, Role = role, Is_email_verified = user.IsEmailVerified }
            });
        });

        group.MapPost("/auth/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete("lazuar_auth");
            return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
        });

        group.MapPost("/auth/forgot-password", async Task<Ok<StatusResponse>> (ForgotPasswordRequestDto req, IMediator mediator) =>
        {
            await mediator.Send(new ForgotPasswordCommand(req.Email));
            return TypedResults.Ok(new StatusResponse { Status = "requested" });
        });

        group.MapPost("/auth/reset-password", async Task<Ok<StatusResponse>> (ResetPasswordRequestDto req, IMediator mediator) =>
        {
            await mediator.Send(new ResetPasswordCommand(req.Email, req.Token, req.New_password));
            return TypedResults.Ok(new StatusResponse { Status = "reset" });
        });

        group.MapPost("/auth/verify-email", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            VerifyEmailRequestDto req,
            string? email,
            IExecutionContextAccessor ctx,
            IMediator mediator,
            OneDbContext db) =>
        {
            var targetEmail = email;
            if (string.IsNullOrWhiteSpace(targetEmail) && ctx.UserId != Guid.Empty)
            {
                var user = await db.GlobalUsers.FindAsync(ctx.UserId);
                targetEmail = user?.Email;
            }

            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 400,
                    Detail = "Email is required to verify."
                });
            }

            try
            {
                await mediator.Send(new VerifyEmailCommand(targetEmail, req.Token));
                return TypedResults.Ok(new StatusResponse { Status = "verified" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 400,
                    Detail = ex.Message
                });
            }
        });

        group.MapPost("/auth/resend-verification", async Task<Ok<StatusResponse>> (ResendVerificationRequestDto req, IMediator mediator) =>
        {
            await mediator.Send(new ResendVerificationEmailCommand(req.Email));
            return TypedResults.Ok(new StatusResponse { Status = "requested" });
        });

        group.MapGet("/auth/me", async Task<Results<Ok<AuthUser>, UnauthorizedHttpResult>> (ClaimsPrincipal principal, OneDbContext db, HttpContext ctx) =>
        {
            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId)) return TypedResults.Unauthorized();

            var user = await db.GlobalUsers.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                ctx.Response.Cookies.Delete("lazuar_auth");
                return TypedResults.Unauthorized();
            }

            var stampClaim = principal.FindFirst("security_stamp")?.Value;
            if (stampClaim != user.SecurityStamp.ToString())
            {
                ctx.Response.Cookies.Delete("lazuar_auth");
                return TypedResults.Unauthorized();
            }

            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? (user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT");

            return TypedResults.Ok(new AuthUser
            {
                Email = user.Email,
                Name = user.Name,
                Role = role,
                Is_email_verified = user.IsEmailVerified
            });
        }).RequireAuthorization();

        return group;
    }

    internal static string ResolveRegisterClientKey(HttpContext ctx, string email)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
            {
                ip = first;
            }
        }

        ip ??= "unknown";
        return $"email:{email}|ip:{ip}";
    }

    private static void IssueCookie(HttpContext ctx, GlobalUser user, IConfiguration config)
    {
        var jwtService = ctx.RequestServices.GetRequiredService<IJwtService>();
        var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
        var issuer = config["Jwt:Issuer"] ?? "lazuar-api";
        var audience = config["Jwt:Audience"] ?? "lazuar-clients";
        var expiryHours = config.GetValue<int>("Jwt:ExpiryHours", 24);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT"),
            new Claim("is_system_admin", user.IsSystemAdmin ? "true" : "false"),
            new Claim("is_email_verified", user.IsEmailVerified ? "true" : "false"),
            new Claim("security_stamp", user.SecurityStamp.ToString())
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
