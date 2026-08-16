using System;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Lhdn.Application.Commands;
using Modules.One.Contracts;

namespace Modules.Lhdn.Infrastructure;

public static class PublicTinValidationEndpoints
{
    public static IEndpointRouteBuilder MapPublicTinValidationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/public/commerce/{tenantSlug}/validate-tin", async Task<Results<Ok<ValidateTinResponseDto>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            string tenantSlug,
            [FromBody] ValidateTinRequestDto req,
            IOneQueryService oneQueryService,
            IMediator mediator,
            HttpContext httpContext) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Workspace not found." });
            }

            httpContext.Items["TenantId"] = tenantId.Value;

            try
            {
                var result = await mediator.Send(new ValidateTaxpayerTinCommand(
                    tenantId.Value,
                    req.Tin,
                    req.Id_type.ToString(),
                    req.Id_value));
                return TypedResults.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                var detail = ex.Message.Contains("configuration", StringComparison.OrdinalIgnoreCase)
                    ? "Merchant has not connected MyInvois."
                    : "TIN could not be validated.";
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = detail });
            }
        });

        return endpoints;
    }
}
