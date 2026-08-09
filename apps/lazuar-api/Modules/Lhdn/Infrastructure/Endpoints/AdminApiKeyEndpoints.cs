using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Lhdn.Domain;
using Modules.One.Contracts;

namespace Modules.Lhdn.Infrastructure;

public static class AdminApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapLhdnAdminApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Dashboard / org-admin surface: no API_CLIENT (key mint).
        var admin = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        // API keys are platform-owned (One). Lhdn routes remain as a product façade.
        admin.MapGet("/api-keys", async Task<Ok<ICollection<ApiKeyDto>>> (
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            var keys = await credentials.ListAsync(ctx.TenantId);
            var dtos = keys.Select(k => new ApiKeyDto
            {
                Id = k.Id.ToString(),
                Name = k.Name,
                Prefix = k.Prefix,
                Hint = k.Hint,
                Is_active = k.IsActive,
                Created_at = new DateTimeOffset(k.CreatedAt, TimeSpan.Zero),
                Scopes = ApiKeyScopes.Split(k.Scopes).ToList()
            }).ToList();
            return TypedResults.Ok((ICollection<ApiKeyDto>)dtos);
        });

        admin.MapPost("/api-keys", async Task<Results<Ok<GenerateApiKeyResponseDto>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] GenerateApiKeyRequestDto req,
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            try
            {
                var createdBy = ctx.UserId == Guid.Empty ? (Guid?)null : ctx.UserId;
                // Null/omitted scopes → LHDN document defaults (product façade compat).
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
                    Scopes = ApiKeyScopes.Split(created.Scopes).ToList()
                });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapDelete("/api-keys/{id:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
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

        return endpoints;
    }
}
