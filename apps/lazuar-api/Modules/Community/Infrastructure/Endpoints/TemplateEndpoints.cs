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

namespace Modules.Community.Infrastructure;

public static class TemplateEndpoints
{
    public static RouteGroupBuilder MapTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/templates", async Task<Ok<ICollection<MessageTemplateDto>>> (
            IExecutionContextAccessor ctx,
            IMessageTemplateQueryService templateService) =>
        {
            var templates = await templateService.GetAllTemplatesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<MessageTemplateDto>)templates.ToList());
        });

        group.MapPost("/templates", async Task<Ok<IdResponse>> (
            CreateTemplateRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new CreateMessageTemplateCommand(
                ctx.TenantId, 
                req.Name, 
                req.Subject, 
                req.Body, 
                req.Channel,
                req.Required_variables ?? new List<string>(), 
                req.Optional_variables ?? new List<string>());
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapGet("/templates/variables", async Task<Ok<ICollection<TemplateVariableCategoryDto>>> (
            IMediator mediator) =>
        {
            var variables = await mediator.Send(new GetTemplateVariablesQuery());
            return TypedResults.Ok((ICollection<TemplateVariableCategoryDto>)variables.ToList());
        });

        group.MapPost("/templates/preview", async Task<Ok<TemplatePreviewResponseDto>> (
            TemplatePreviewRequestDto req,
            IMediator mediator) =>
        {
            var response = await mediator.Send(new RenderTemplatePreviewQuery(req.Subject, req.Body));
            return TypedResults.Ok(response);
        });

        group.MapPut("/templates/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateTemplateRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new UpdateMessageTemplateCommand(ctx.TenantId, id, req.Subject, req.Body));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/templates/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ResetMessageTemplateCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "reset" });
        });

        group.MapPost("/reminders/test", async Task<Ok<TestReminderResponse>> (
            TestReminderRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new SendTestReminderCommand(ctx.TenantId, req.Template_name, req.Channel));
            return TypedResults.Ok(new TestReminderResponse { Success = true, Sent_to = "admin@lazuars.io" });
        });

        return group;
    }
}
