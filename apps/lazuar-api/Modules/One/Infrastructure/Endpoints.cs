using System.Security.Claims;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
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
using Modules.One.Domain;

namespace Modules.One.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/one").RequireCors();

        group.MapPost("/public/register", async Task<Results<Ok<LoginResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] PublicRegisterRequestDto req,
            IConfiguration config,
            OneDbContext db,
            IMediator mediator,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Email and password are required." });

            if (string.IsNullOrEmpty(req.Workspace_name) || string.IsNullOrEmpty(req.Tenant_slug))
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Workspace name and slug are required." });

            try
            {
                var userId = await mediator.Send(new RegisterPublicUserCommand(email, req.Password, req.Name, req.Workspace_name, req.Tenant_slug));
                var user = await db.GlobalUsers.FindAsync(userId);

                IssueCookie(ctx, user!, config);

                return TypedResults.Ok(new LoginResponse
                {
                    User = new AuthUser { Email = user!.Email, Name = user.Name, Role = "ADMIN", Is_email_verified = user.IsEmailVerified }
                });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
            catch (BusinessRuleValidationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
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

        group.MapPost("/auth/reset-password", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (ResetPasswordRequestDto req, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ResetPasswordCommand(req.Email, req.Token, req.New_password));
                return TypedResults.Ok(new StatusResponse { Status = "reset" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        group.MapPost("/auth/verify-email", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (VerifyEmailRequestDto req, IExecutionContextAccessor ctx, IMediator mediator, OneDbContext db) =>
        {
            try
            {
                var user = await db.GlobalUsers.FindAsync(ctx.UserId);
                if (user == null) return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400 });

                await mediator.Send(new VerifyEmailCommand(user.Email, req.Token));
                return TypedResults.Ok(new StatusResponse { Status = "verified" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

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

        group.MapPut("/me/profile", async Task<Ok<StatusResponse>> (UpdateProfileRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new UpdateProfileCommand(ctx.UserId, req.Name));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization();

        group.MapPut("/me/security/password", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (ChangePasswordRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ChangePasswordCommand(ctx.UserId, req.Current_password, req.New_password));
                return TypedResults.Ok(new StatusResponse { Status = "updated" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/workspaces", async Task<Results<Ok<IdResponse>, UnauthorizedHttpResult, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            CreateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            try
            {
                var id = await mediator.Send(new CreateWorkspaceCommand(ctx.UserId, req.Name, req.Slug, req.Provision_apps?.ToList() ?? new List<string>()));
                return TypedResults.Ok(new IdResponse { Id = id.ToString() });
            }
            catch (BusinessRuleValidationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPut("/workspaces/{id:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            Guid id, UpdateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            try
            {
                await mediator.Send(new UpdateWorkspaceCommand(id, ctx.UserId, req.Name, req.Slug));
                return TypedResults.Ok(new StatusResponse { Status = "updated" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
            catch (BusinessRuleValidationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            try
            {
                await mediator.Send(new ArchiveWorkspaceCommand(id, ctx.UserId));
                return TypedResults.Ok(new StatusResponse { Status = "archived" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/workspaces", async Task<Results<Ok<ICollection<WorkspaceDto>>, UnauthorizedHttpResult>> (IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty || !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            var workspaces = await queryService.GetWorkspacesAsync();
            var dtos = workspaces.Select(w => new WorkspaceDto { Id = w.Id.ToString(), Name = w.Name, Slug = w.Slug, Is_active = w.IsActive, Created_at = new DateTimeOffset(w.CreatedAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceDto>)dtos);
        }).RequireAuthorization("OrgAdmin");

        group.MapGet("/workspaces/{id:guid}/members", async Task<Results<Ok<ICollection<WorkspaceMemberDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var members = await queryService.GetWorkspaceMembersAsync(id);
            var dtos = members.Select(m => new WorkspaceMemberDto { Id = m.Id.ToString(), Global_user_id = m.GlobalUserId.ToString(), Name = m.Name, Email = m.Email, Role = m.Role, Joined_at = new DateTimeOffset(m.JoinedAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceMemberDto>)dtos);
        }).RequireAuthorization();

        group.MapPost("/workspaces/{id:guid}/invites", async Task<Results<Ok<IdResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, CreateWorkspaceInvitationDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                var inviteId = await mediator.Send(new InviteUserToWorkspaceCommand(id, ctx.UserId, req.Email, req.Role));
                return TypedResults.Ok(new IdResponse { Id = inviteId.ToString() });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/workspaces/{id:guid}/invites", async Task<Results<Ok<ICollection<WorkspaceInvitationDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var invites = await queryService.GetWorkspaceInvitationsAsync(id);
            var dtos = invites.Select(i => new WorkspaceInvitationDto { Id = i.Id.ToString(), Email = i.Email, Role = i.Role, Status = i.Status, Expires_at = new DateTimeOffset(i.ExpiresAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceInvitationDto>)dtos);
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}/invites/{inviteId:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, Guid inviteId, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RevokeWorkspaceInvitationCommand(id, ctx.UserId, inviteId));
                return TypedResults.Ok(new StatusResponse { Status = "revoked" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}/members/{userId:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, Guid userId, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RemoveWorkspaceMemberCommand(id, ctx.UserId, userId));
                return TypedResults.Ok(new StatusResponse { Status = "removed" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/workspaces/invites/accept", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (AcceptWorkspaceInvitationDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new AcceptWorkspaceInvitationCommand(ctx.UserId, req.Token));
                return TypedResults.Ok(new StatusResponse { Status = "accepted" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        }).RequireAuthorization();

        group.MapGet("/workspaces/{id:guid}/apps", async Task<Results<Ok<ICollection<WorkspaceAppDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty || !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            var apps = await queryService.GetWorkspaceAppsAsync(id);
            return TypedResults.Ok((ICollection<WorkspaceAppDto>)apps.Select(a => new WorkspaceAppDto { App_id = a }).ToList());
        }).RequireAuthorization("OrgAdmin");

        group.MapPost("/workspaces/{id:guid}/apps/{appId}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (Guid id, string appId, ToggleAppEntitlementRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty || !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            await mediator.Send(new ToggleAppEntitlementCommand(id, appId, req.Is_active));
            return TypedResults.Ok(new StatusResponse { Status = req.Is_active ? "enabled" : "disabled" });
        }).RequireAuthorization("OrgAdmin");

        group.MapGet("/me/entitlements", async Task<Results<Ok<ICollection<EntitlementDto>>, UnauthorizedHttpResult>> (IExecutionContextAccessor ctx, OneDbContext db) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var entitlements = await db.TenantMemberships.IgnoreQueryFilters().Where(m => m.GlobalUserId == ctx.UserId)
                .Join(db.Organizations.IgnoreQueryFilters(), m => m.OrganizationId, o => o.Id, (m, o) => new EntitlementDto { Workspace_id = o.Id.ToString(), Workspace_name = o.Name, Workspace_slug = o.Slug, Role = m.Role }).ToListAsync();
            return TypedResults.Ok((ICollection<EntitlementDto>)entitlements);
        }).RequireAuthorization();

        return endpoints;
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
