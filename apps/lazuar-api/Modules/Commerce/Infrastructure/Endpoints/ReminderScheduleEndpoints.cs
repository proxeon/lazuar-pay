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

public static class ReminderScheduleEndpoints
{
    public static RouteGroupBuilder MapReminderScheduleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reminder-schedules", async Task<Ok<ICollection<ReminderScheduleDto>>> (
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var schedules = await queryService.GetReminderSchedulesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<ReminderScheduleDto>)schedules.ToList());
        });

        group.MapPost("/reminder-schedules", async Task<Ok<IdResponse>> (
            CreateReminderScheduleRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? productId = !string.IsNullOrEmpty(req.Product_id) ? Guid.Parse(req.Product_id) : null;
            var command = new CreateReminderScheduleCommand(
                ctx.TenantId, productId, Guid.Parse(req.Template_id), req.Channel,
                req.Days_relative_to_due, req.Time_of_day, req.Is_enabled);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPost("/reminder-schedules/defaults", async Task<Ok<StatusResponse>> (
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new GenerateDefaultSchedulesCommand(ctx.TenantId));
            return TypedResults.Ok(new StatusResponse { Status = "generated" });
        });

        group.MapPut("/reminder-schedules/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdateReminderScheduleRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? productId = !string.IsNullOrEmpty(req.Product_id) ? Guid.Parse(req.Product_id) : null;
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            
            await mediator.Send(new UpdateReminderScheduleCommand(
                ctx.TenantId, id, productId, templateId, req.Channel,
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

        return group;
    }
}
