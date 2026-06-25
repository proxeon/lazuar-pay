// apps/lazuar-api/Modules/Community/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs
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
using Modules.One.Contracts;

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
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IEventBus _eventBus;
    private readonly ICommunityLinkService _linkService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly IOneQueryService _oneQueryService;

    public NotificationDispatchDomainEventHandlers(
        ICrmQueryService crmQueryService,
        ILocalMessageTemplateQueryService templateService,
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityPlanRepository planRepository,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus,
        ICommunityLinkService linkService,
        IMagicLinkTokenService tokenService,
        IOneQueryService oneQueryService)
    {
        _crmQueryService = crmQueryService;
        _templateService = templateService;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _eventBus = eventBus;
        _linkService = linkService;
        _tokenService = tokenService;
        _oneQueryService = oneQueryService;
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
        if (notification.IsSilent) return;

        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        if (sub == null) return;

        var plan = await _planRepository.GetByIdAsync(sub.PlanId, ct);
        if (plan == null) return;

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(notification.OrganizationId);
        var tenantSlug = workspace?.Slug ?? "workspace";

        var templateName = notification.IsFirstPayment ? "Community Welcome" : "Community Payment Success";
        var template = await _templateService.GetTemplateByNameAsync(notification.OrganizationId, templateName);

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var magicToken = _tokenService.GenerateToken(sub.Id);
        
        // Aligned to module-specific portal route
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.Full_name,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan.Name,
            ["group_link"] = plan.TelegramInviteLink ?? "",
            ["meeting_link"] = plan.WeeklyMeetingLink ?? "",
            ["total_price"] = plan.Price.ToString("F2"),
            ["portal_magic_link"] = portalMagicLink
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Active";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.Full_name}, your subscription to {plan.Name} is now active. Access your portal here: {portalMagicLink}";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, template?.Channel ?? "EMAIL"));
    }

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        if (sub == null) return;

        var plan = await _planRepository.GetByIdAsync(sub.PlanId, ct);
        if (plan == null) return;

        var template = await _templateService.GetTemplateByNameAsync(notification.OrganizationId, "Community Subscription Cancelled");

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.Full_name,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan.Name,
            ["current_period_end"] = sub.CurrentPeriodEnd?.ToString("dd MMM yyyy") ?? "the end of your billing cycle"
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Cancelled";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.Full_name}, your subscription has been cancelled.";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, template?.Channel ?? "EMAIL"));
    }

    public async Task Handle(SubscriptionRenewalDueDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        if (sub == null) return;

        var plan = await _planRepository.GetByIdAsync(sub.PlanId, ct);
        if (plan == null) return;

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(notification.OrganizationId);
        var tenantSlug = workspace?.Slug ?? "workspace";

        var template = (await _templateService.GetTemplatesAsync(new[] { notification.TemplateId })).FirstOrDefault();

        var baseUrl = _linkService.GetCommunityBaseUrl();
        
        // Aligned to module-specific checkout route
        var renewalLink = sub.IsReminderOnly
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/{plan.Slug}/checkout";

        var magicToken = _tokenService.GenerateToken(sub.Id);
        
        // Aligned to module-specific portal route
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.Full_name,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan.Name,
            ["renewal_link"] = renewalLink,
            ["portal_magic_link"] = portalMagicLink
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Renewal Reminder";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Renew here: {renewalLink}";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, notification.Channel));
    }

    public async Task Handle(MagicLinkRequestedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.Full_name},<br><br>Click the link below to access your subscriber portal to manage or cancel your subscription. This link expires in 24 hours.<br><br><a href=\"{notification.MagicLinkUrl}\">Access Portal</a><br><br>— Lazuar Support";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, "Your Subscriber Portal Access", body, "EMAIL"));
    }

    public async Task Handle(OneOffReminderRequestedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        if (sub == null) return;

        var plan = await _planRepository.GetByIdAsync(sub.PlanId, ct);
        if (plan == null) return;

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(notification.OrganizationId);
        var tenantSlug = workspace?.Slug ?? "workspace";

        var baseUrl = _linkService.GetCommunityBaseUrl();
        
        // Aligned to module-specific checkout route
        var renewalLink = sub.IsReminderOnly
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/{plan.Slug}/checkout";

        var magicToken = _tokenService.GenerateToken(sub.Id);
        
        // Aligned to module-specific portal route
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        string subject = "Important Update Regarding Your Subscription";
        string body = "";

        if (!string.IsNullOrWhiteSpace(notification.CustomMessage))
        {
            body = $"Hi {profile.Full_name},<br><br>{notification.CustomMessage.Replace("\n", "<br>")}";
        }
        else if (notification.TemplateId.HasValue)
        {
            var template = (await _templateService.GetTemplatesAsync(new[] { notification.TemplateId.Value })).FirstOrDefault();
            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.Full_name,
                ["business_name"] = "Our Community",
                ["plan_name"] = plan.Name,
                ["total_price"] = plan.Price.ToString("F2"),
                ["renewal_link"] = renewalLink,
                ["portal_magic_link"] = portalMagicLink
            };
            subject = template != null ? RenderTemplate(template.Subject, variables) : subject;
            body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.Full_name}, notification regarding your subscription.";
        }
        else return;

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, body, notification.Channel));
    }
}
