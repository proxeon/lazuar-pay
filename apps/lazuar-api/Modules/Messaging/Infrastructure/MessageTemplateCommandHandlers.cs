using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure;

public class UpdateMessageTemplateCommandHandler : IRequestHandler<UpdateMessageTemplateCommand>
{
    private readonly MessagingDbContext _context;

    public UpdateMessageTemplateCommandHandler(MessagingDbContext context) => _context = context;

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

public class ResetMessageTemplateCommandHandler : IRequestHandler<ResetMessageTemplateCommand>
{
    private readonly MessagingDbContext _context;

    public ResetMessageTemplateCommandHandler(MessagingDbContext context) => _context = context;

    public async Task Handle(ResetMessageTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId, cancellationToken);

        if (template == null) throw new InvalidOperationException("Template not found.");

        var defaultTemplates = AutomationRuleSeeder.GetDefaultTemplates(request.OrganizationId);
        var defaultTemplate = defaultTemplates.FirstOrDefault(t => t.Name == template.Name);

        if (defaultTemplate != null)
            template.ResetToDefault(defaultTemplate.Subject, defaultTemplate.Body);
        else
            template.ResetToDefault("", "");

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class SendTestReminderCommandHandler : IRequestHandler<SendTestReminderCommand>
{
    private readonly IEmailService _emailService;
    private readonly IMessageTemplateQueryService _templateService;

    public SendTestReminderCommandHandler(IEmailService emailService, IMessageTemplateQueryService templateService)
    {
        _emailService = emailService;
        _templateService = templateService;
    }

    public async Task Handle(SendTestReminderCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateService.GetTemplateByNameAsync(request.OrganizationId, request.TemplateName);
        if (template == null) throw new InvalidOperationException("Template not found.");

        // Render with dummy data for testing
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

        // Send to hardcoded admin email for testing
        await _emailService.SendEmailAsync("admin@lazuars.io", subject, body);
    }
}
