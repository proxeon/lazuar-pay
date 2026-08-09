using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.One.Application.Queries;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.One.Infrastructure.Services;

namespace Modules.One.Infrastructure;

public static class ApiCredentialEndpoints
{
    public static RouteGroupBuilder MapApiCredentialEndpoints(this RouteGroupBuilder group)
    {
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

        return group;
    }
}
