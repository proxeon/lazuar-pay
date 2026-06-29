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

public static class DunningCampaignEndpoints
{
    public static RouteGroupBuilder MapDunningCampaignEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dunning-campaigns", async Task<Ok<ICollection<DunningCampaignDto>>> (
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var campaigns = await queryService.GetDunningCampaignsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<DunningCampaignDto>)campaigns.ToList());
        });

        group.MapPost("/dunning-campaigns", async Task<Ok<IdResponse>> (
            CreateDunningCampaignRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var targetProductIds = req.Target_product_ids?.Select(Guid.Parse).ToList();
            var steps = req.Steps.Select(s => new DunningStepData(s.Day_offset, Guid.Parse(s.Template_id), s.Channel)).ToList();

            var command = new CreateDunningCampaignCommand(
                ctx.TenantId, req.Name, req.Final_action, req.Grace_period_days,
                targetProductIds, req.Target_payment_methods, steps);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPost("/dunning-campaigns/defaults", async Task<Ok<StatusResponse>> (
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new GenerateDefaultDunningCampaignsCommand(ctx.TenantId));
            return TypedResults.Ok(new StatusResponse { Status = "generated" });
        });

        group.MapPut("/dunning-campaigns/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdateDunningCampaignRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var targetProductIds = req.Target_product_ids?.Select(Guid.Parse).ToList();
            var steps = req.Steps.Select(s => new DunningStepData(s.Day_offset, Guid.Parse(s.Template_id), s.Channel)).ToList();

            await mediator.Send(new UpdateDunningCampaignCommand(
                ctx.TenantId, id, req.Name, req.Final_action, req.Grace_period_days,
                targetProductIds, req.Target_payment_methods, steps, req.Is_active));
            
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/dunning-campaigns/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteDunningCampaignCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        return group;
    }
}
