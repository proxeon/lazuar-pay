using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
using Modules.Messaging.Contracts;

namespace Modules.Community.Infrastructure.Commands;

public class CreateMessageTemplateCommandHandler : ICommandHandler<CreateMessageTemplateCommand, Guid>
{
    private readonly CommunityDbContext _context;

    public CreateMessageTemplateCommandHandler(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        ValidateTemplateVariables(request.Channel, request.Subject, request.EmailBody, request.WhatsAppBody, request.RequiredVariables, request.OptionalVariables);

        var template = new Domain.Entities.MessageTemplate(
            request.OrganizationId,
            request.Name,
            request.Channel,
            request.Subject,
            request.EmailBody,
            request.WhatsAppBody,
            isDefault: false,
            request.RequiredVariables,
            request.OptionalVariables);

        _context.MessageTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

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

        ValidateTemplateVariables(template.Channel, request.Subject, request.EmailBody, request.WhatsAppBody, template.RequiredVariables, template.OptionalVariables);

        template.UpdateContent(request.Subject, request.EmailBody, request.WhatsAppBody);

        await _context.SaveChangesAsync(cancellationToken);
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
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Welcome", "ALL", 
                "You're in! Welcome to {{plan_name}} 🎉", 
                "Hi {{customer_name}},\n\nYour payment of RM {{total_price}} was successful, and your access is officially active. We are thrilled to have you here.\n\nHere is everything you need to get started:\n\n1. **Join the Community:** Meet everyone and say hi!\n<a href=\"{{group_link}}\">Join the Telegram Group</a>\n\n2. **Weekly Sessions:** Bookmark our live room.\n<a href=\"{{meeting_link}}\">Save the Zoom Link</a>\n\nYou can access your resources anytime via your private dashboard:\n<a href=\"{{portal_magic_link}}\">Go to my Dashboard</a>\n\nSee you inside,\n— {{business_name}}", 
                "Hey {{customer_name}}! 🎉 Welcome to {{plan_name}}! Your payment is confirmed. Click here to join the private group right now: {{group_link}}. See you inside! 🚀", 
                true, new[] { "{{group_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}", "{{portal_magic_link}}", "{{total_price}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Payment Success", "ALL", 
                "Payment Received: {{plan_name}}", 
                "Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\nYou can manage your subscription at any time via your portal:\n<a href=\"{{portal_magic_link}}\">Access Portal</a>\n\n— {{business_name}}", 
                "Hi {{customer_name}}, your payment of RM {{total_price}} for {{plan_name}} is confirmed! ✅ Manage your access here: {{portal_magic_link}}", 
                true, new[] { "{{total_price}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{portal_magic_link}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Payment Failed", "ALL", 
                "Action Needed: Payment issue for {{plan_name}}", 
                "Hi {{customer_name}},\n\nWe tried to process your renewal for {{plan_name}}, but the payment didn't go through. This usually just means your bank blocked the transaction or the card expired.\n\nTo ensure you don't lose access to the community and upcoming sessions, please update your payment details here:\n\n<a href=\"{{renewal_link}}\">Securely Update Payment</a>\n\nIf you need any help, just reply to this email.\n\n— {{business_name}}", 
                "Hi {{customer_name}} 👋 Quick heads up: your recent card payment for {{plan_name}} was declined by the bank. To keep your access active, you can quickly update your details here: {{renewal_link}}. Let us know if you need help!", 
                true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal (3 Days)", "ALL", 
                "Upcoming renewal for {{plan_name}}", 
                "Hi {{customer_name}},\n\nWe hope you're getting great value out of the community! This is just a quick reminder that your {{plan_name}} subscription will automatically renew in a few days.\n\nIf you need to update your card, download invoices, or manage your account, you can access your dashboard below:\n\n<a href=\"{{renewal_link}}\">Manage Account</a>\n\n— {{business_name}}", 
                "Hey {{customer_name}}, hope you're doing great! 🌟 Just a quick reminder that your {{plan_name}} cycle renews in 3 days. No action needed if you're staying with us, but you can manage your account anytime here: {{renewal_link}}", 
                true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal Due Today", "ALL", 
                "Action Required: {{plan_name}} renewal due today", 
                "Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n\n<a href=\"{{renewal_link}}\">Renew Subscription</a>\n\n— {{business_name}}", 
                "Hi {{customer_name}}! ⏳ Your {{plan_name}} membership is due for renewal today. Secure your access here: {{renewal_link}}", 
                true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Renewal Overdue", "ALL", 
                "Final Notice: {{plan_name}} is overdue", 
                "Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n\n<a href=\"{{renewal_link}}\">Renew Now</a>\n\n— {{business_name}}", 
                "Hey {{customer_name}}, your {{plan_name}} membership is past due and access will be suspended soon. ⚠️ You can resolve this quickly here: {{renewal_link}}", 
                true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Community Subscription Cancelled", "ALL", 
                "Your {{plan_name}} membership has ended", 
                "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}", 
                "Hi {{customer_name}}, your {{plan_name}} membership has been cancelled. You have access until {{current_period_end}}. We hope to see you back soon! 🙏", 
                true, Array.Empty<string>(), new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{current_period_end}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Abandoned Cart (12h)", "WHATSAPP", 
                "Complete your purchase for {{item_name}}", 
                "", 
                "Hey {{customer_name}}! We noticed you left {{item_name}} in your cart. Did you have any trouble with the payment page? You can finish your checkout securely here: {{checkout_url}} ⚡️", 
                true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" }),
                
            new Domain.Entities.MessageTemplate(request.OrganizationId, "Abandoned Cart (24h)", "EMAIL", 
                "Did you run into an issue?", 
                "Hi {{customer_name}},\n\nWe noticed you started checking out for {{item_name}} but didn't finish.\n\nIf you had any technical issues, just reply to this email and we'll help you out. Otherwise, your spot is still reserved! You can complete your registration right here:\n\n<a href=\"{{checkout_url}}\">Complete my registration</a>\n\nHope to see you inside.\n\n— {{business_name}}", 
                "", 
                true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" })
        };

        var defaultTemplate = defaultTemplates.FirstOrDefault(t => t.Name == template.Name);

        if (defaultTemplate != null)
        {
            template.ResetToDefault(defaultTemplate.Subject, defaultTemplate.EmailBody, defaultTemplate.WhatsAppBody, defaultTemplate.RequiredVariables, defaultTemplate.OptionalVariables);
        }
        else
        {
            template.ResetToDefault("", "", "", Array.Empty<string>(), Array.Empty<string>());
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
                .Replace("{{item_name}}", "Digital Course Bundle", StringComparison.OrdinalIgnoreCase)
                .Replace("{{checkout_url}}", "https://portal.lazuar.com/checkout", StringComparison.OrdinalIgnoreCase)
                .Replace("{{current_period_end}}", "31 Dec 2026", StringComparison.OrdinalIgnoreCase);
        }

        var subject = PopulateMocks(template.Subject);
        var emailBody = PopulateMocks(template.Email_body).Replace("\\n", "<br>").Replace("\n", "<br>");
        var whatsappBody = PopulateMocks(template.Whatsapp_body).Replace("\\n", "\n");

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
