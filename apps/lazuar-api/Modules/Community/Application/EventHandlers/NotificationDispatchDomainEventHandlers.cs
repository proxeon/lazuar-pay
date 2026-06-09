using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Domain.Events;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

// Alias to prevent ambiguity with the old deleted interface
using ILocalMessageTemplateQueryService = Modules.Community.Application.Queries.IMessageTemplateQueryService;

namespace Modules.Community.Application.EventHandlers;

public class NotificationDispatchDomainEventHandlers : 
    INotificationHandler<SubscriptionActivatedDomainEvent>,
    INotificationHandler<SubscriptionCancelledDomainEvent>,
    INotificationHandler<SubscriptionRenewalDueDomainEvent>,
    INotificationHandler<MagicLinkRequestedDomainEvent>,
    INotificationHandler<OneOffReminderRequestedDomainEvent>
{
    private readonly ICrmQueryService _crmQueryService;
    private readonly ILocalMessageTemplateQueryService _templateService;
    private readonly ICommunityQueryService _communityQueryService;
    private readonly IEventBus _eventBus;
    private readonly ICommunityLinkService _linkService;

    public NotificationDispatchDomainEventHandlers(
        ICrmQueryService crmQueryService, 
        ILocalMessageTemplateQueryService templateService,
        ICommunityQueryService communityQueryService,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus,
        ICommunityLinkService linkService)
    {
        _crmQueryService = crmQueryService;
        _templateService = templateService;
        _communityQueryService = communityQueryService;
        _eventBus = eventBus;
        _linkService = linkService;
    }

    private string RenderTemplate(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? "", StringComparison.OrdinalIgnoreCase);
        }
        return result.Replace("\\n", "<br>").Replace("\n", "<br>");
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _communityQueryService.GetPortalSubscriptionAsync(notification.OrganizationId, notification.SubscriptionId);
        var plan = await _communityQueryService.GetAdminPlanByIdAsync(notification.OrganizationId, Guid.Parse(sub!.Plan_id));

        var templateName = notification.IsFirstPayment ? "Community Welcome" : "Community Payment Success";
        var template = await _templateService.GetTemplateByNameAsync(notification.OrganizationId, templateName);

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.FullName,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan!.Name,
            ["group_link"] = plan.Telegram_invite_link ?? "",
            ["meeting_link"] = plan.Weekly_meeting_link ?? "",
            ["total_price"] = plan.Price.ToString("F2")
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Active";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, your subscription to {plan.Name} is now active.";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, template?.Channel ?? "EMAIL"));
    }

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _communityQueryService.GetPortalSubscriptionAsync(notification.OrganizationId, notification.SubscriptionId);
        var template = await _templateService.GetTemplateByNameAsync(notification.OrganizationId, "Community Subscription Cancelled");

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.FullName,
            ["business_name"] = "Our Community",
            ["plan_name"] = sub!.Plan_name,
            ["current_period_end"] = sub.Current_period_end?.ToString("dd MMM yyyy") ?? "the end of your billing cycle"
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Cancelled";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, your subscription has been cancelled.";
        
        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, template?.Channel ?? "EMAIL"));
    }

    public async Task Handle(SubscriptionRenewalDueDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _communityQueryService.GetPortalSubscriptionAsync(notification.OrganizationId, notification.SubscriptionId);
        var plan = await _communityQueryService.GetAdminPlanByIdAsync(notification.OrganizationId, Guid.Parse(sub!.Plan_id));
        var template = (await _templateService.GetTemplatesAsync(new[] { notification.TemplateId })).FirstOrDefault();

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var renewalLink = sub.Is_reminder_only 
            ? $"Please remit payment directly. Notes: {sub.Admin_notes ?? "Contact us for payment details"}"
            : $"{baseUrl}/{plan!.Slug}/checkout";

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.FullName,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan!.Name,
            ["renewal_link"] = renewalLink
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Renewal Reminder";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Renew here: {renewalLink}";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, notification.Channel));
    }

    public async Task Handle(MagicLinkRequestedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},<br><br>Click the link below to access your subscriber portal to manage or cancel your subscription. This link expires in 24 hours.<br><br><a href=\"{notification.MagicLinkUrl}\">Access Portal</a><br><br>— Lazuar Support";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, "Your Subscriber Portal Access", body, "EMAIL"));
    }

    public async Task Handle(OneOffReminderRequestedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _communityQueryService.GetPortalSubscriptionAsync(notification.OrganizationId, notification.SubscriptionId);
        var plan = await _communityQueryService.GetAdminPlanByIdAsync(notification.OrganizationId, Guid.Parse(sub!.Plan_id));

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var renewalLink = sub.Is_reminder_only 
            ? $"Please remit payment directly. Notes: {sub.Admin_notes ?? "Contact us for payment details"}"
            : $"{baseUrl}/{plan!.Slug}/checkout";

        string subject = "Important Update Regarding Your Subscription";
        string body = "";

        if (!string.IsNullOrWhiteSpace(notification.CustomMessage))
        {
            body = $"Hi {profile.FullName},<br><br>{notification.CustomMessage.Replace("\n", "<br>")}";
        }
        else if (notification.TemplateId.HasValue)
        {
            var template = (await _templateService.GetTemplatesAsync(new[] { notification.TemplateId.Value })).FirstOrDefault();
            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.FullName,
                ["business_name"] = "Our Community",
                ["plan_name"] = plan!.Name,
                ["total_price"] = plan.Price.ToString("F2"),
                ["renewal_link"] = renewalLink
            };
            subject = template != null ? RenderTemplate(template.Subject, variables) : subject;
            body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, notification regarding your subscription.";
        }
        else return;

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, notification.Channel));
    }
}
