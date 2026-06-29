using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;
using Modules.Communications.Contracts.Commands;
using Modules.Communications.Domain.Aggregates;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Application.Commands;

public class CreateMessageTemplateCommandHandler : ICommandHandler<CreateMessageTemplateCommand, Guid>
{
    private readonly ICommunicationsRepository _repository;

    public CreateMessageTemplateCommandHandler(ICommunicationsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        ValidateTemplateVariables(request.Channel, request.Subject, request.EmailBody, request.WhatsAppBody, request.RequiredVariables, request.OptionalVariables);

        var template = new MessageTemplate(
            request.OrganizationId,
            request.Name,
            request.Channel,
            request.Subject,
            request.EmailBody,
            request.WhatsAppBody,
            isDefault: false,
            request.RequiredVariables,
            request.OptionalVariables);

        _repository.AddTemplate(template);
        await _repository.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    private void ValidateTemplateVariables(
        string channel,
        string subject, 
        string emailBody, 
        string whatsappBody,
        IEnumerable<string> requiredVariables, 
        IEnumerable<string> optionalVariables)
    {
        if (channel is "EMAIL" or "ALL")
        {
            CheckVariables($"{subject} {emailBody}", requiredVariables, optionalVariables, "Email");
        }

        if (channel is "WHATSAPP" or "ALL")
        {
            CheckVariables(whatsappBody, requiredVariables, optionalVariables, "WhatsApp");
        }
    }

    private void CheckVariables(string content, IEnumerable<string> requiredVariables, IEnumerable<string> optionalVariables, string contextName)
    {
        var extractedTags = Regex.Matches(content, @"\{\{([a-zA-Z0-9_]+)\}\}")
            .Select(m => m.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedVariables = new HashSet<string>(requiredVariables.Concat(optionalVariables), StringComparer.OrdinalIgnoreCase);

        var unsupportedTags = extractedTags.Except(allowedVariables).ToList();
        if (unsupportedTags.Any())
        {
            var joined = string.Join(", ", unsupportedTags);
            throw new BusinessRuleValidationException(new GenericBusinessRule($"Unsupported variables detected in {contextName}: {joined}. Please use only the allowed tags."));
        }

        var missingTags = requiredVariables.Except(extractedTags, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingTags.Any())
        {
            var joined = string.Join(", ", missingTags);
            throw new BusinessRuleValidationException(new GenericBusinessRule($"Missing required variables in {contextName}: {joined}. You must include these tags to ensure the message functions correctly."));
        }
    }
}

public class UpdateMessageTemplateCommandHandler : ICommandHandler<UpdateMessageTemplateCommand>
{
    private readonly ICommunicationsRepository _repository;

    public UpdateMessageTemplateCommandHandler(ICommunicationsRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetTemplateByIdAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        template.UpdateContent(request.Subject, request.EmailBody, request.WhatsAppBody);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

public class ResetMessageTemplateCommandHandler : ICommandHandler<ResetMessageTemplateCommand>
{
    private readonly ICommunicationsRepository _repository;

    public ResetMessageTemplateCommandHandler(ICommunicationsRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ResetMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetTemplateByIdAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        template.UpdateContent("", "", "");

        await _repository.SaveChangesAsync(cancellationToken);
    }
}

public class SendTestReminderCommandHandler : ICommandHandler<SendTestReminderCommand>
{
    private readonly ICommunicationsRepository _repository;
    private readonly IEventBus _eventBus;

    public SendTestReminderCommandHandler(
        ICommunicationsRepository repository,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(SendTestReminderCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetTemplateByNameAsync(request.OrganizationId, request.TemplateName, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        string PopulateMocks(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("{{customer_name}}", "Test User", StringComparison.OrdinalIgnoreCase)
                .Replace("{{business_name}}", "Test Business", StringComparison.OrdinalIgnoreCase)
                .Replace("{{plan_name}}", "Test Plan", StringComparison.OrdinalIgnoreCase)
                .Replace("{{group_link}}", "https://t.me/test", StringComparison.OrdinalIgnoreCase)
                .Replace("{{meeting_link}}", "https://zoom.us/test", StringComparison.OrdinalIgnoreCase)
                .Replace("{{total_price}}", "99.00", StringComparison.OrdinalIgnoreCase)
                .Replace("{{renewal_link}}", "https://example.com/renew", StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", "https://portal.lazuar.com/workspace/portal?token=test_token", StringComparison.OrdinalIgnoreCase)
                .Replace("{{fulfillment_url}}", "https://cloudflare.r2/download.pdf", StringComparison.OrdinalIgnoreCase)
                .Replace("{{current_period_end}}", "31 Dec 2026", StringComparison.OrdinalIgnoreCase);
        }

        var subject = MarkdownParser.ToPlainText(PopulateMocks(template.Subject));
        var emailBody = MarkdownParser.ToHtml(PopulateMocks(template.EmailBody));
        var whatsappBody = MarkdownParser.ToPlainText(PopulateMocks(template.WhatsAppBody));

        var dispatchEvent = new DispatchMessageIntegrationEvent(
            OrganizationId: request.OrganizationId,
            ToEmail: "admin@lazuars.io",
            ToPhone: "+60123456789",
            Subject: subject,
            HtmlEmailBody: emailBody,
            PlainTextPhoneBody: whatsappBody,
            Channel: request.Channel ?? template.Channel
        );

        await _eventBus.PublishAsync(dispatchEvent);
    }
}
