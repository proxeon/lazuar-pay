// apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.IO;
using System.Threading.Tasks;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.One.Application.Commands;
using Modules.One.Application.Queries;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.One.Infrastructure.Configuration;
using Modules.One.Infrastructure.Services;

namespace Modules.One.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/one").RequireCors();

        group.MapPost("/public/register", async Task<Ok<LoginResponse>> (
            [FromBody] PublicRegisterRequestDto req,
            IConfiguration config,
            OneDbContext db,
            IMediator mediator,
            HttpContext ctx) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(req.Password))
                throw new InvalidOperationException("Email and password are required.");

            if (string.IsNullOrEmpty(req.Workspace_name) || string.IsNullOrEmpty(req.Tenant_slug))
                throw new InvalidOperationException("Workspace name and slug are required.");

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

        group.MapPost("/auth/verify-email", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (VerifyEmailRequestDto req, IExecutionContextAccessor ctx, IMediator mediator, OneDbContext db) =>
        {
            var user = await db.GlobalUsers.FindAsync(ctx.UserId);
            if (user == null) return TypedResults.Unauthorized();

            await mediator.Send(new VerifyEmailCommand(user.Email, req.Token));
            return TypedResults.Ok(new StatusResponse { Status = "verified" });
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

        group.MapPut("/me/security/password", async Task<Ok<StatusResponse>> (ChangePasswordRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ChangePasswordCommand(ctx.UserId, req.Current_password, req.New_password));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization();

        group.MapPost("/workspaces", async Task<Results<Ok<IdResponse>, UnauthorizedHttpResult>> (
            CreateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            var id = await mediator.Send(new CreateWorkspaceCommand(ctx.UserId, req.Name, req.Slug, req.Provision_apps?.ToList() ?? new List<string>()));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        }).RequireAuthorization();

        group.MapPut("/workspaces/{id:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
            Guid id, UpdateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            await mediator.Send(new UpdateWorkspaceCommand(id, ctx.UserId, req.Name, req.Slug));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
            Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            await mediator.Send(new ArchiveWorkspaceCommand(id, ctx.UserId));
            return TypedResults.Ok(new StatusResponse { Status = "archived" });
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
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            var members = await queryService.GetWorkspaceMembersAsync(id);
            var dtos = members.Select(m => new WorkspaceMemberDto { Id = m.Id.ToString(), Global_user_id = m.GlobalUserId.ToString(), Name = m.Name, Email = m.Email, Role = m.Role, Joined_at = new DateTimeOffset(m.JoinedAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceMemberDto>)dtos);
        }).RequireAuthorization();

        group.MapPost("/workspaces/{id:guid}/invites", async Task<Ok<IdResponse>> (Guid id, CreateWorkspaceInvitationDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var inviteId = await mediator.Send(new InviteUserToWorkspaceCommand(id, ctx.UserId, req.Email, req.Role));
            return TypedResults.Ok(new IdResponse { Id = inviteId.ToString() });
        }).RequireAuthorization();

        group.MapGet("/workspaces/{id:guid}/invites", async Task<Results<Ok<ICollection<WorkspaceInvitationDto>>, UnauthorizedHttpResult>> (Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            var invites = await queryService.GetWorkspaceInvitationsAsync(id);
            var dtos = invites.Select(i => new WorkspaceInvitationDto { Id = i.Id.ToString(), Email = i.Email, Role = i.Role, Status = i.Status, Expires_at = new DateTimeOffset(i.ExpiresAt) }).ToList();
            return TypedResults.Ok((ICollection<WorkspaceInvitationDto>)dtos);
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}/invites/{inviteId:guid}", async Task<Ok<StatusResponse>> (Guid id, Guid inviteId, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new RevokeWorkspaceInvitationCommand(id, ctx.UserId, inviteId));
            return TypedResults.Ok(new StatusResponse { Status = "revoked" });
        }).RequireAuthorization();

        group.MapDelete("/workspaces/{id:guid}/members/{userId:guid}", async Task<Ok<StatusResponse>> (Guid id, Guid userId, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new RemoveWorkspaceMemberCommand(id, ctx.UserId, userId));
            return TypedResults.Ok(new StatusResponse { Status = "removed" });
        }).RequireAuthorization();

        group.MapPost("/workspaces/invites/accept", async Task<Ok<StatusResponse>> (AcceptWorkspaceInvitationDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new AcceptWorkspaceInvitationCommand(ctx.UserId, req.Token));
            return TypedResults.Ok(new StatusResponse { Status = "accepted" });
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

            // Platform superadmins can operate any active workspace (ops-page requires ≥1 entitlement).
            if (ctx.IsSystemAdmin)
            {
                var allWorkspaces = await db.Organizations.IgnoreQueryFilters()
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .Select(o => new EntitlementDto
                    {
                        Workspace_id = o.Id.ToString(),
                        Workspace_name = o.Name,
                        Workspace_slug = o.Slug,
                        Role = "SUPER_ADMIN"
                    })
                    .ToListAsync();
                return TypedResults.Ok((ICollection<EntitlementDto>)allWorkspaces);
            }

            var entitlements = await db.TenantMemberships.IgnoreQueryFilters().Where(m => m.GlobalUserId == ctx.UserId)
                .Join(db.Organizations.IgnoreQueryFilters(), m => m.OrganizationId, o => o.Id, (m, o) => new EntitlementDto { Workspace_id = o.Id.ToString(), Workspace_name = o.Name, Workspace_slug = o.Slug, Role = m.Role }).ToListAsync();
            return TypedResults.Ok((ICollection<EntitlementDto>)entitlements);
        }).RequireAuthorization();

        // Multi-endpoint workspace webhooks (GET list without full secret; POST create returns secret once; PUT by id).
        // Auth: workspace ADMIN/SUPER_ADMIN membership | system admin | API_CLIENT + webhooks.endpoints:manage
        // IDOR: path workspace id must match membership (human) or API key TenantId (machine).
        group.MapGet("/workspaces/{id:guid}/webhooks", async Task<Results<Ok<ICollection<WebhookEndpointDto>>, UnauthorizedHttpResult>> (
            Guid id,
            HttpContext http,
            IExecutionContextAccessor ctx,
            IOneQueryService queryService) =>
        {
            if (!await CanAccessWorkspaceWebhooksAsync(id, http, ctx, queryService, manageRequired: false))
            {
                return TypedResults.Unauthorized();
            }

            var endpoints = await queryService.GetWorkspaceWebhooksAsync(id);
            var dtos = endpoints.Select(e => new WebhookEndpointDto
            {
                Id = e.Id.ToString(),
                Url = e.Url,
                Is_active = e.IsActive,
                Created_at = new DateTimeOffset(e.CreatedAt, TimeSpan.Zero),
                Enabled_events = e.EnabledEvents.ToList(),
                Has_secret = e.HasSecret,
                Secret_hint = e.SecretHint
            }).ToList();

            return TypedResults.Ok((ICollection<WebhookEndpointDto>)dtos);
        }).RequireAuthorization();

        group.MapPost("/workspaces/{id:guid}/webhooks", async Task<Results<Ok<CreateWebhookEndpointResponseDto>, UnauthorizedHttpResult, BadRequest<string>>> (
            Guid id,
            CreateWebhookEndpointRequestDto req,
            HttpContext http,
            IExecutionContextAccessor ctx,
            IMediator mediator,
            IOneQueryService queryService) =>
        {
            if (!await CanAccessWorkspaceWebhooksAsync(id, http, ctx, queryService, manageRequired: true))
            {
                return TypedResults.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.Url))
            {
                return TypedResults.BadRequest("url is required.");
            }

            try
            {
                var created = await mediator.Send(new CreateWebhookEndpointCommand(
                    id,
                    req.Url.Trim(),
                    req.Is_active ?? true,
                    req.Enabled_events?.ToList()));

                return TypedResults.Ok(new CreateWebhookEndpointResponseDto
                {
                    Id = created.Id.ToString(),
                    Url = created.Url,
                    Secret_key = created.SecretKey,
                    Is_active = created.IsActive,
                    Enabled_events = created.EnabledEvents.ToList(),
                    Created_at = new DateTimeOffset(created.CreatedAt, TimeSpan.Zero)
                });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        }).RequireAuthorization();

        group.MapPut("/workspaces/{id:guid}/webhooks/{endpointId:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult, NotFound, BadRequest<string>>> (
            Guid id,
            Guid endpointId,
            UpdateWebhookEndpointRequestDto req,
            HttpContext http,
            IExecutionContextAccessor ctx,
            IMediator mediator,
            IOneQueryService queryService) =>
        {
            if (!await CanAccessWorkspaceWebhooksAsync(id, http, ctx, queryService, manageRequired: true))
            {
                return TypedResults.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.Url))
            {
                return TypedResults.BadRequest("url is required.");
            }

            try
            {
                await mediator.Send(new UpdateWebhookEndpointCommand(
                    id,
                    endpointId,
                    req.Url.Trim(),
                    req.Is_active,
                    req.Enabled_events?.ToList()));
                return TypedResults.Ok(new StatusResponse { Status = "saved" });
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        }).RequireAuthorization();

        group.MapGet("/workspaces/{id:guid}/webhooks/logs", async Task<Results<Ok<ICollection<WebhookDeliveryLogDto>>, UnauthorizedHttpResult>> (
            Guid id,
            HttpContext http,
            IExecutionContextAccessor ctx,
            IOneQueryService queryService) =>
        {
            if (!await CanAccessWorkspaceWebhooksAsync(id, http, ctx, queryService, manageRequired: false))
            {
                return TypedResults.Unauthorized();
            }

            var logs = await queryService.GetWorkspaceWebhookLogsAsync(id);
            var dtos = logs.Select(l => new WebhookDeliveryLogDto {
                Id = l.Id.ToString(),
                Event_type = l.EventType,
                Status = l.Status,
                Attempt_count = l.AttemptCount,
                Last_error = l.LastError,
                Created_at = new DateTimeOffset(l.CreatedAt)
            }).ToList();

            return TypedResults.Ok((ICollection<WebhookDeliveryLogDto>)dtos);
        }).RequireAuthorization();

        group.MapPost("/storage/presigned-url", Task<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>> (
            [FromBody] GetPresignedUrlRequestDto req,
            IExecutionContextAccessor ctx,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(req.File_name))
            {
                return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(TypedResults.BadRequest("File name is required."));
            }

            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty)
            {
                return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(
                    TypedResults.BadRequest("Tenant context is required to create a presigned storage URL."));
            }

            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var publicUrlBase = config["R2_PUBLIC_DEV_URL"]?.TrimEnd('/');

            var extension = Path.GetExtension(req.File_name);
            var key = $"vault/{tenantId}/{Guid.CreateVersion7()}{extension}";

            var uploadUrl = r2Service.GetPresignedUploadUrl(bucket, key, req.Content_type);
            var finalUrl = $"{publicUrlBase}/{key}";

            return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(TypedResults.Ok(new GetPresignedUrlResponseDto 
            { 
                Upload_url = uploadUrl, 
                Final_url = finalUrl 
            }));
        }).RequireAuthorization();

        // Platform API credentials (OrgAdmin JWT only — never API_CLIENT).
        var orgAdmin = group.MapGroup("").RequireAuthorization("OrgAdmin");

        orgAdmin.MapGet("/api-keys", async Task<Ok<ICollection<ApiKeyDto>>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new ListApiCredentialsQuery(ctx.TenantId));
            return TypedResults.Ok((ICollection<ApiKeyDto>)result.ToList());
        });

        orgAdmin.MapPost("/api-keys", async Task<Results<Ok<GenerateApiKeyResponseDto>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] GenerateApiKeyRequestDto req,
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            try
            {
                var createdBy = ctx.UserId == Guid.Empty ? (Guid?)null : ctx.UserId;
                // Null/omitted scopes → LHDN document defaults; empty/unknown → 400.
                IReadOnlyList<string>? scopes = req.Scopes is null ? null : req.Scopes.ToList();
                var created = await credentials.GenerateAsync(
                    ctx.TenantId,
                    req.Name,
                    req.Is_test_mode,
                    createdBy,
                    scopes);
                return TypedResults.Ok(new GenerateApiKeyResponseDto
                {
                    Id = created.Id.ToString(),
                    Name = created.Name,
                    Prefix = created.Prefix,
                    Hint = created.Hint,
                    Created_at = new DateTimeOffset(created.CreatedAt, TimeSpan.Zero),
                    Plain_key = created.PlainKey,
                    Scopes = PlatformApiScopes.Split(created.Scopes).ToList()
                });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        orgAdmin.MapDelete("/api-keys/{id:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            try
            {
                await credentials.RevokeAsync(ctx.TenantId, id);
                return TypedResults.Ok(new StatusResponse { Status = "revoked" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        // Phase 1 policy probe for IntegrationPaymentsCheckoutsWrite (real M2M checkout routes land in Phase 2).
        // Authenticated API clients with payments.checkouts:write (or human admins) receive 200; others 403.
        endpoints.MapGet("/one/integrations/payments/checkouts/_scope-probe", () =>
                TypedResults.Ok(new StatusResponse { Status = "payments.checkouts:write" }))
            .RequireAuthorization("IntegrationPaymentsCheckoutsWrite")
            .RequireCors();

        // Integrator provision: multi-product workspace + bootstrap key.
        // Auth: X-Lazuar-Provision-Key / Bearer provision secret OR SUPER_ADMIN JWT. Tenant-exempt.
        // Body: external_product (default "aura") + external_org_id OR legacy aura_org_id.
        group.MapPost("/integrations/workspaces/provision", async Task<IResult> (
            [FromBody] ProvisionWorkspaceRequestDto req,
            HttpContext http,
            IMediator mediator,
            IOptions<IntegratorProvisionSettings> provisionOptions,
            IntegratorProvisionRateLimiter rateLimiter,
            ILoggerFactory loggerFactory) =>
        {
            var settings = provisionOptions.Value;
            var auth = IntegratorProvisionAuth.Evaluate(http, settings);
            if (!auth.IsAuthorized)
            {
                return Results.Json(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = auth.StatusCode,
                        Title = auth.StatusCode == 403 ? "Forbidden" : "Unauthorized",
                        Detail = auth.FailureReason
                    },
                    statusCode: auth.StatusCode);
            }

            // external_org_id aliases aura_org_id (backward compatible).
            var externalOrgIdRaw = FirstNonEmpty(req.External_org_id, req.Aura_org_id);
            var externalProductRaw = string.IsNullOrWhiteSpace(req.External_product)
                ? ProvisionAuraWorkspaceCommandHandler.ProductAura
                : req.External_product.Trim();

            if (!await rateLimiter.TryAcquireAsync("secret:global", settings.RateLimitPerMinute, http.RequestAborted))
            {
                http.Response.Headers.RetryAfter = "60";
                return Results.Json(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Provision rate limit exceeded. Retry later."
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (!string.IsNullOrEmpty(externalOrgIdRaw))
            {
                var perOrgKey =
                    $"org:{externalProductRaw.ToLowerInvariant()}:{externalOrgIdRaw.ToLowerInvariant()}";
                if (!await rateLimiter.TryAcquireAsync(
                        perOrgKey,
                        settings.RateLimitPerAuraOrgPerMinute,
                        http.RequestAborted))
                {
                    http.Response.Headers.RetryAfter = "60";
                    return Results.Json(
                        new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too Many Requests",
                            Detail = "Provision rate limit exceeded for this external org. Retry later."
                        },
                        statusCode: StatusCodes.Status429TooManyRequests);
                }
            }

            try
            {
                var result = await mediator.Send(new ProvisionAuraWorkspaceCommand(
                    externalOrgIdRaw,
                    req.Display_name ?? string.Empty,
                    req.Slug,
                    req.Owner_email,
                    req.Owner_role,
                    req.Is_test_mode ?? true,
                    req.Key_name,
                    req.Webhook_url,
                    req.Webhook_enabled_events,
                    auth.ActorUserId,
                    externalProductRaw));

                var log = loggerFactory.CreateLogger("Modules.One.WorkspaceProvision");
                log.LogInformation(
                    "WorkspaceProvisioned workspace_id={WorkspaceId} external_product={Product} external_org_id={ExternalOrgId} created={Created} key_id={KeyId} prefix={Prefix} hint={Hint} webhook_endpoint_id={WebhookId} owner_attached={OwnerAttached} owner_status={OwnerStatus}",
                    result.WorkspaceId,
                    result.ExternalProduct,
                    result.AuraOrgId,
                    result.Created,
                    result.ApiKeyId,
                    result.Prefix,
                    result.Hint,
                    result.WebhookEndpointId,
                    result.OwnerAttached,
                    result.OwnerStatus);
                // Never log result.PlainKey or result.WebhookSecretKey.

                return TypedResults.Ok(new ProvisionWorkspaceResponseDto
                {
                    Workspace_id = result.WorkspaceId.ToString(),
                    Slug = result.Slug,
                    Aura_org_id = result.AuraOrgId,
                    External_org_id = result.ExternalOrgId ?? result.AuraOrgId,
                    External_product = result.ExternalProduct,
                    Created = result.Created,
                    Api_key = new ProvisionWorkspaceApiKeyDto
                    {
                        Id = result.ApiKeyId?.ToString(),
                        Prefix = result.Prefix,
                        Hint = result.Hint,
                        Scopes = result.Scopes.ToList(),
                        Plain_key = result.PlainKey
                    },
                    Webhook = result.WebhookEndpointId is null
                        ? null
                        : new ProvisionWorkspaceWebhookDto
                        {
                            Id = result.WebhookEndpointId?.ToString(),
                            Url = result.WebhookUrl,
                            Is_active = result.WebhookIsActive,
                            Enabled_events = result.WebhookEnabledEvents.ToList(),
                            Secret_key = result.WebhookSecretKey,
                            Has_secret = !string.IsNullOrEmpty(result.WebhookSecretKey)
                                || !string.IsNullOrEmpty(result.WebhookSecretHint),
                            Secret_hint = result.WebhookSecretHint
                        },
                    Owner = new ProvisionWorkspaceOwnerDto
                    {
                        Attached = result.OwnerAttached,
                        Status = result.OwnerStatus,
                        Role = result.OwnerRole,
                        Email = string.IsNullOrWhiteSpace(req.Owner_email)
                            ? null
                            : req.Owner_email.Trim()
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                var status = ex.Message.Contains("already taken", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
                return Results.Json(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = status,
                        Title = status == 409 ? "Conflict" : "Bad Request",
                        Detail = ex.Message
                    },
                    statusCode: status);
            }
        });

        return endpoints;
    }

    /// <summary>
    /// Workspace webhook authZ:
    /// - System admin: allow
    /// - API_CLIENT with webhooks.endpoints:manage: allow only when path id == key TenantId (IDOR fail-closed)
    /// - Human: membership ADMIN/SUPER_ADMIN for path id (manage) or any membership (read)
    /// </summary>
    internal static async Task<bool> CanAccessWorkspaceWebhooksAsync(
        Guid workspaceId,
        HttpContext http,
        IExecutionContextAccessor ctx,
        IOneQueryService queryService,
        bool manageRequired)
    {
        if (ctx.IsSystemAdmin)
        {
            return true;
        }

        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("API_CLIENT"))
        {
            if (ctx.TenantId == Guid.Empty || ctx.TenantId != workspaceId)
            {
                return false;
            }

            // Manage (and list for companion) require the manage scope for machine clients.
            return user.HasClaim("scope", PlatformApiScopes.WebhooksEndpointsManage);
        }

        if (ctx.UserId == Guid.Empty)
        {
            return false;
        }

        if (manageRequired)
        {
            var role = await queryService.GetTenantRoleAsync(ctx.UserId, workspaceId);
            return role is "ADMIN" or "SUPER_ADMIN";
        }

        var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, workspaceId);
        return hasAccess;
    }

    private static string FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a.Trim();
        }

        return string.IsNullOrWhiteSpace(b) ? string.Empty : b.Trim();
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
