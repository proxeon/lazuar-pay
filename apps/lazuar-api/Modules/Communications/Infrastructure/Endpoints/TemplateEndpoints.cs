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
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Application;
using Modules.Communications.Contracts;
using Modules.Communications.Contracts.Commands;
using Modules.Communications.Domain;

namespace Modules.Communications.Infrastructure;

public static class TemplateEndpoints
{
    public static RouteGroupBuilder MapTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/templates", async Task<Ok<ICollection<MessageTemplateDto>>> (
            IExecutionContextAccessor ctx,
            ICommunicationsQueryService templateService) =>
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
                req.Email_body,
                req.Whatsapp_body,
                req.Channel,
                req.Required_variables ?? new List<string>(), 
                req.Optional_variables ?? new List<string>());
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapGet("/templates/variables", async Task<Ok<ICollection<TemplateVariableCategoryDto>>> (
            ICommunicationsQueryService templateService) =>
        {
            var variables = await templateService.GetTemplateVariablesAsync();
            return TypedResults.Ok((ICollection<TemplateVariableCategoryDto>)variables.ToList());
        });

        group.MapPost("/templates/preview", async Task<Ok<TemplatePreviewResponseDto>> (
            TemplatePreviewRequestDto req) =>
        {
            var subjectContent = MarkdownParser.ToPlainText(MessageTemplateHydrator.PopulatePreview(req.Subject));
            var htmlEmailContent = MarkdownParser.ToHtml(MessageTemplateHydrator.PopulatePreview(req.Email_body));
            var textWhatsappContent = MarkdownParser.ToPlainText(MessageTemplateHydrator.PopulatePreview(req.Whatsapp_body));

            var response = new TemplatePreviewResponseDto
            {
                Html_email_preview = htmlEmailContent,
                Text_whatsapp_preview = textWhatsappContent,
                Subject_content = subjectContent
            };

            return TypedResults.Ok(response);
        });

        group.MapPut("/templates/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateTemplateRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new UpdateMessageTemplateCommand(ctx.TenantId, id, req.Subject, req.Email_body, req.Whatsapp_body));
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

        // Utility: remove orphan/abandoned templates only. Never deletes lifecycle catalog templates.
        group.MapDelete("/templates/legacy-cleanup", async Task<Ok<StatusResponse>> (
            IExecutionContextAccessor ctx,
            CommunicationsDbContext dbContext) =>
        {
            var orphanNames = DefaultMessageTemplates.OrphanNames;

            var templatesToDelete = await dbContext.MessageTemplates
                .Where(t => t.OrganizationId == ctx.TenantId && orphanNames.Contains(t.Name))
                .ToListAsync();

            if (templatesToDelete.Any())
            {
                dbContext.MessageTemplates.RemoveRange(templatesToDelete);
                await dbContext.SaveChangesAsync();
            }

            return TypedResults.Ok(new StatusResponse { Status = "cleaned" });
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
