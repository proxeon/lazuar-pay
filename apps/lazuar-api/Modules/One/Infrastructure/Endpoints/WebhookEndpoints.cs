using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Infrastructure;

public static class WebhookEndpoints
{
    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
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

        return group;
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
}
