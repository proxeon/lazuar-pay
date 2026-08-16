using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Modules.One.Application.Commands;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/workspaces", async Task<Results<Ok<IdResponse>, UnauthorizedHttpResult>> (
            CreateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            var id = await mediator.Send(new CreateWorkspaceCommand(ctx.UserId, req.Name, req.Slug, req.Provision_apps?.ToList() ?? new List<string>()));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        }).RequireAuthorization();

        group.MapGet("/workspaces/{id:guid}", async Task<Results<Ok<WorkspaceDto>, UnauthorizedHttpResult, NotFound>> (
            Guid id, IExecutionContextAccessor ctx, IOneQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            var hasAccess = await queryService.HasTenantAccessAsync(ctx.UserId, id);
            if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            var workspace = await queryService.GetWorkspaceByIdAsync(id);
            if (workspace == null) return TypedResults.NotFound();

            return TypedResults.Ok(new WorkspaceDto
            {
                Id = workspace.Id.ToString(),
                Name = workspace.Name,
                Slug = workspace.Slug,
                Is_active = workspace.IsActive,
                Created_at = new DateTimeOffset(workspace.CreatedAt),
                Logo_url = workspace.LogoUrl,
                Primary_color = workspace.PrimaryColor
            });
        }).RequireAuthorization();

        group.MapPut("/workspaces/{id:guid}", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
            Guid id, UpdateWorkspaceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            await mediator.Send(new UpdateWorkspaceCommand(
                id,
                ctx.UserId,
                req.Name,
                req.Slug,
                req.Logo_url,
                req.Primary_color,
                UpdateBranding: true));
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

            // Platform superadmins can operate any active workspace (lazuar-ops requires ≥1 entitlement).
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

        return group;
    }
}
