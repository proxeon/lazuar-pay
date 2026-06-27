using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Infrastructure;

public record CreateProductRequest(
    string Name, 
    string Slug, 
    decimal Price, 
    string Currency, 
    string Interval, 
    bool Requires_address, 
    bool Requires_tax_id, 
    bool Requires_phone, 
    System.Collections.Generic.List<string> Fulfillment_targets);

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/products", async Task<Ok<IdResponse>> (
            CreateProductRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new CreateProductCommand(
                ctx.TenantId,
                req.Name,
                req.Slug,
                req.Price,
                req.Currency,
                req.Interval,
                req.Requires_address,
                req.Requires_tax_id,
                req.Requires_phone,
                req.Fulfillment_targets
            );

            var productId = await mediator.Send(command);

            return TypedResults.Ok(new IdResponse { Id = productId.ToString() });
        });

        return group;
    }
}
