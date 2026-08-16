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
            CreateProductRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new CreateProductCommand(
                ctx.TenantId,
                req.Name,
                req.Slug,
                (decimal)req.Price,
                req.Pricing_model,
                (decimal)req.Minimum_price,
                req.Currency,
                req.Interval,
                req.Gateway_name,
                req.Requires_address,
                req.Requires_tax_id,
                req.Requires_phone,
                req.Fulfillment_targets ?? new List<string>(),
                req.Sst_tax_type,
                (decimal)(req.Sst_rate_percent ?? 0),
                req.Trial_days ?? 0,
                req.Yearly_price.HasValue ? (decimal)req.Yearly_price.Value : null
            );

            var productId = await mediator.Send(command);

            return TypedResults.Ok(new IdResponse { Id = productId.ToString() });
        }).RequireAuthorization("OrgMember");

        group.MapPut("/products/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateProductRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new UpdateProductCommand(
                ctx.TenantId,
                id,
                req.Name,
                req.Slug,
                (decimal)req.Price,
                req.Pricing_model,
                (decimal)req.Minimum_price,
                req.Currency,
                req.Interval,
                req.Is_active,
                req.Gateway_name,
                req.Requires_address,
                req.Requires_tax_id,
                req.Requires_phone,
                req.Fulfillment_targets ?? new List<string>(),
                req.Sst_tax_type,
                (decimal)(req.Sst_rate_percent ?? 0),
                req.Trial_days,
                req.Yearly_price.HasValue ? (decimal)req.Yearly_price.Value : null
            );

            await mediator.Send(command);

            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization("OrgMember");

        group.MapDelete("/products/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ArchiveProductCommand(ctx.TenantId, id));

            return TypedResults.Ok(new StatusResponse { Status = "archived" });
        }).RequireAuthorization("OrgMember");

        group.MapPost("/products/{id:guid}/restore", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new RestoreProductCommand(ctx.TenantId, id));

            return TypedResults.Ok(new StatusResponse { Status = "restored" });
        }).RequireAuthorization("OrgMember");

        return group;
    }
}
