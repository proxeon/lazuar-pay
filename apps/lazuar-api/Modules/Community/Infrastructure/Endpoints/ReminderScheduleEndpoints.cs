// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints/ReminderScheduleEndpoints.cs
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

public static class ReminderScheduleEndpoints
{
    public static RouteGroupBuilder MapReminderScheduleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reminder-schedules", async Task<Ok<ICollection<CommunityReminderScheduleDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var schedules = await queryService.GetReminderSchedulesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CommunityReminderScheduleDto>)schedules.ToList());
        });

        group.MapPost("/reminder-schedules", async Task<Ok<IdResponse>> (
            CreateReminderScheduleRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? planId = !string.IsNullOrEmpty(req.Plan_id) ? Guid.Parse(req.Plan_id) : null;
            var command = new CreateReminderScheduleCommand(
                ctx.TenantId, planId, Guid.Parse(req.Template_id), req.Channel,
                req.Days_relative_to_due, req.Time_of_day, req.Is_enabled);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPut("/reminder-schedules/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdateReminderScheduleRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? planId = !string.IsNullOrEmpty(req.Plan_id) ? Guid.Parse(req.Plan_id) : null;
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            
            await mediator.Send(new UpdateReminderScheduleCommand(
                ctx.TenantId, id, planId, templateId, req.Channel,
                req.Days_relative_to_due, req.Time_of_day, req.Is_enabled));
            
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/reminder-schedules/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteReminderScheduleCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        group.MapPost("/reminders/schedule-one-off", async Task<Ok<StatusResponse>> (
            ScheduleOneOffRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            
            await mediator.Send(new ScheduleOneOffReminderCommand(
                ctx.TenantId, Guid.Parse(req.Subscriber_id), templateId, req.Custom_message, req.Channel ?? "DEFAULT", req.Scheduled_at.UtcDateTime));
            
            return TypedResults.Ok(new StatusResponse { Status = "scheduled" });
        });

        return group;
    }
}
