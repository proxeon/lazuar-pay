using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
using Modules.Messaging.Contracts;

namespace Modules.Community.Infrastructure.Commands;

public class UpdateMessageTemplateCommandHandler : ICommandHandler<UpdateMessageTemplateCommand>
{
    private readonly CommunityDbContext _context;

    public UpdateMessageTemplateCommandHandler(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        template.UpdateContent(request.Subject, request.Body);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class ResetMessageTemplateCommandHandler : ICommandHandler<ResetMessageTemplateCommand>
{
    private readonly CommunityDbContext _context;

    public ResetMessageTemplateCommandHandler(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ResetMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        var defaultTemplates = new List<Domain.Entities.MessageTemplate>
        {
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Welcome", "ALL", "Welcome to {{plan_name}}! 🎉", "Hi {{customer_name}},\n\nWelcome to {{plan_name}}!\n\nHere is your private group link:\n{{group_link}}\n\nWeekly session link:\n{{meeting_link}}\n\nSee you there! 🙏\n\n— {{business_name}}", true, new[] { "{{group_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Payment Success", "ALL", "Payment Received: {{plan_name}}", "Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\n— {{business_name}}", true, new[] { "{{total_price}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Payment Failed", "ALL", "Payment Failed: {{plan_name}}", "Hi {{customer_name}},\n\nWe were unable to process your renewal payment for {{plan_name}}.\n\nPlease complete your payment to avoid losing access to the community:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal (3 Days)", "ALL", "Your {{plan_name}} subscription renews in 3 days", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is expiring in 3 days. To ensure you don't lose access to the community and weekly sessions, please renew your subscription here:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal Due Today", "ALL", "Action Required: {{plan_name}} renewal due today", "Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal Overdue", "ALL", "Final Notice: {{plan_name}} is overdue", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Subscription Cancelled", "ALL", "Your {{plan_name}} membership has ended", "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}", true, Array.Empty<string>(), new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{current_period_end}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Abandoned Cart (12h)", "WHATSAPP", "Complete your purchase for {{item_name}}", "Hi {{customer_name}},\n\nWe noticed you didn't complete your purchase for {{item_name}}. Did you have trouble with the payment page?\n\nHere is a fresh link to complete your transaction:\n{{checkout_url}}\n\n— {{business_name}}", true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" }),
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Abandoned Cart (24h)", "EMAIL", "Don't miss out on {{item_name}}", "Hi {{customer_name}},\n\nSpots are filling up fast! Grab yours here before it's gone:\n{{checkout_url}}\n\n— {{business_name}}", true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" })
        };

        var defaultTemplate = defaultTemplates.FirstOrDefault(t => t.Name == template.Name);

        if (defaultTemplate != null)
        {
            template.ResetToDefault(defaultTemplate.Subject, defaultTemplate.Body, defaultTemplate.RequiredVariables, defaultTemplate.OptionalVariables);
        }
        else
        {
            template.ResetToDefault("", "", Array.Empty<string>(), Array.Empty<string>());
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class SendTestReminderCommandHandler : ICommandHandler<SendTestReminderCommand>
{
    private readonly IMessageTemplateQueryService _templateService;
    private readonly IEventBus _eventBus;

    public SendTestReminderCommandHandler(
        IMessageTemplateQueryService templateService,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus)
    {
        _templateService = templateService;
        _eventBus = eventBus;
    }

    public async Task Handle(SendTestReminderCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateService.GetTemplateByNameAsync(request.OrganizationId, request.TemplateName);
        if (template == null) throw new InvalidOperationException("Template not found.");

        var body = template.Body
            .Replace("{{customer_name}}", "Test User", StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", "Test Business", StringComparison.OrdinalIgnoreCase)
            .Replace("{{plan_name}}", "Test Plan", StringComparison.OrdinalIgnoreCase)
            .Replace("{{group_link}}", "https://t.me/test", StringComparison.OrdinalIgnoreCase)
            .Replace("{{meeting_link}}", "https://zoom.us/test", StringComparison.OrdinalIgnoreCase)
            .Replace("{{total_price}}", "99.00", StringComparison.OrdinalIgnoreCase)
            .Replace("{{renewal_link}}", "https://example.com/renew", StringComparison.OrdinalIgnoreCase);

        var subject = template.Subject
            .Replace("{{customer_name}}", "Test User", StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", "Test Business", StringComparison.OrdinalIgnoreCase)
            .Replace("{{plan_name}}", "Test Plan", StringComparison.OrdinalIgnoreCase)
            .Replace("{{total_price}}", "99.00", StringComparison.OrdinalIgnoreCase);

        var htmlBody = body.Replace("\\n", "<br>").Replace("\n", "<br>");

        var dispatchEvent = new DispatchMessageIntegrationEvent(
            OrganizationId: request.OrganizationId,
            ToEmail: "admin@lazuars.io",
            ToPhone: "+60123456789",
            Subject: subject,
            HtmlBody: htmlBody,
            Channel: request.Channel ?? template.Channel
        );

        await _eventBus.PublishAsync(dispatchEvent);
    }
}
