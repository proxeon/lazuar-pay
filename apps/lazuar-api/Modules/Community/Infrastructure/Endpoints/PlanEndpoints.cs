// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints/PlanEndpoints.cs
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
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Endpoints;

public static class PlanEndpoints
{
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/plans", async Task<Ok<ICollection<CommunityPlanDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var plans = await queryService.GetAdminPlansAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CommunityPlanDto>)plans.ToList());
        });

        group.MapGet("/plans/{id:guid}", async Task<Results<Ok<CommunityPlanDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var plan = await queryService.GetAdminPlanByIdAsync(ctx.TenantId, id);
            return plan != null ? TypedResults.Ok(plan) : TypedResults.NotFound();
        });

        group.MapPost("/plans", async Task<Ok<IdResponse>> (
            CreatePlanRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new CreatePlanCommand(
                ctx.TenantId, req.Slug, req.Name, req.Audience, req.Short_description,
                req.Long_description, (decimal)req.Price, req.Interval, req.Grace_period_days,
                req.Max_capacity, req.Display_order, req.Features?.ToList() ?? new List<string>(), req.Methodology,
                req.Faq?.Select(f => new FaqItemDto(f.Id, f.Question, f.Answer)).ToList() ?? new List<FaqItemDto>(),
                req.Telegram_invite_link, req.Weekly_meeting_link);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPut("/plans/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdatePlanRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new UpdatePlanCommand(
                ctx.TenantId, id, req.Slug, req.Name, req.Audience, req.Short_description,
                req.Long_description, req.Price.HasValue ? (decimal)req.Price.Value : null, req.Interval, req.Features?.ToList(), req.Methodology,
                req.Faq?.Select(f => new FaqItemDto(f.Id, f.Question, f.Answer)).ToList(),
                req.Is_active, req.Display_order, req.Max_capacity, req.Grace_period_days,
                req.Telegram_invite_link, req.Weekly_meeting_link);
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/plans/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new ArchivePlanCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "archived" });
        });

        return group;
    }
}
