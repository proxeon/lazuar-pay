using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.One.Application.Commands;
using Modules.One.Infrastructure.Configuration;
using Modules.One.Infrastructure.Services;

namespace Modules.One.Infrastructure;

public static class IntegrationProvisionEndpoints
{
    public static RouteGroupBuilder MapIntegrationProvisionEndpoints(this RouteGroupBuilder group)
    {
        // Phase 1 policy probe for IntegrationPaymentsCheckoutsWrite (real M2M checkout routes land in Phase 2).
        // Authenticated API clients with payments.checkouts:write (or human admins) receive 200; others 403.
        // Registered on the /one group (same final path as endpoints.MapGet("/one/integrations/...")).
        group.MapGet("/integrations/payments/checkouts/_scope-probe", () =>
                TypedResults.Ok(new StatusResponse { Status = "payments.checkouts:write" }))
            .RequireAuthorization("IntegrationPaymentsCheckoutsWrite");

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

        return group;
    }

    private static string FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a.Trim();
        }

        return string.IsNullOrWhiteSpace(b) ? string.Empty : b.Trim();
    }
}
