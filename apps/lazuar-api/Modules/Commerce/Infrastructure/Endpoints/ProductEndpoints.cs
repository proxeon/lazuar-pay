using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;
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
    List<string> Fulfillment_targets);

public record UpdateProductRequest(
    string Name, 
    string Slug, 
    decimal Price, 
    string Currency, 
    string Interval, 
    bool Is_active,
    bool Requires_address, 
    bool Requires_tax_id, 
    bool Requires_phone, 
    List<string> Fulfillment_targets);

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/products", async Task<Ok<ICollection<ProductDto>>> (
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var products = await queryService.GetProductsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<ProductDto>)products.ToList());
        });

        group.MapGet("/products/{id:guid}", async Task<Results<Ok<ProductDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var product = await queryService.GetProductByIdAsync(ctx.TenantId, id);
            return product != null ? TypedResults.Ok(product) : TypedResults.NotFound();
        });

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
                req.Fulfillment_targets ?? new List<string>()
            );

            var productId = await mediator.Send(command);

            return TypedResults.Ok(new IdResponse { Id = productId.ToString() });
        });

        group.MapPut("/products/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateProductRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new UpdateProductCommand(
                ctx.TenantId,
                id,
                req.Name,
                req.Slug,
                req.Price,
                req.Currency,
                req.Interval,
                req.Is_active,
                req.Requires_address,
                req.Requires_tax_id,
                req.Requires_phone,
                req.Fulfillment_targets ?? new List<string>()
            );

            await mediator.Send(command);

            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/products/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ArchiveProductCommand(ctx.TenantId, id));

            return TypedResults.Ok(new StatusResponse { Status = "archived" });
        });

        return group;
    }
}
