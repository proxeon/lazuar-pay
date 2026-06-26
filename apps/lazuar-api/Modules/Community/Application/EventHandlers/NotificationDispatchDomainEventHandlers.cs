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

    private string RenderEmailTemplate(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? "", StringComparison.OrdinalIgnoreCase);
        }
        return MarkdownParser.ToHtml(result);
    }

    private string RenderWhatsAppTemplate(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? "", StringComparison.OrdinalIgnoreCase);
        }
        return MarkdownParser.ToPlainText(result);
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

        string templateName;
        if (plan.ProductType == "VAULT") 
        {
            templateName = "Digital Product Delivery";
        } 
        else if (plan.ProductType == "EVENT") 
        {
            templateName = "Event Ticket Confirmation";
        } 
        else 
        {
            templateName = notification.IsFirstPayment ? "Community Welcome" : "Community Payment Success";
        }

        var template = await _templateService.GetTemplateByNameAsync(notification.OrganizationId, templateName);

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var magicToken = _tokenService.GenerateToken(sub.Id);
        
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.Full_name,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan.Name,
            ["group_link"] = plan.TelegramInviteLink ?? "",
            ["meeting_link"] = plan.WeeklyMeetingLink ?? "",
            ["total_price"] = (plan.Price * sub.Quantity).ToString("F2"),
            ["portal_magic_link"] = portalMagicLink,
            ["fulfillment_url"] = plan.FulfillmentFileUrl ?? ""
        };

        var subject = template != null ? RenderWhatsAppTemplate(template.Subject, variables) : "Purchase Complete";
        
        string fallbackEmail = "";
        string fallbackWhatsapp = "";

        if (plan.ProductType == "VAULT") 
        {
            fallbackEmail = $"Hi {profile.Full_name},\n\nThank you for your purchase of {plan.Name}. You can download your file here:\n[Download Now]({plan.FulfillmentFileUrl})\n\nYou can also access this anytime via your portal:\n[Dashboard]({portalMagicLink})";
            fallbackWhatsapp = $"Hi {profile.Full_name}, your purchase of {plan.Name} is confirmed! Download your file here: {plan.FulfillmentFileUrl} or access your portal: {portalMagicLink}";
        } 
        else if (plan.ProductType == "EVENT") 
        {
            fallbackEmail = $"Hi {profile.Full_name},\n\nYour ticket for {plan.Name} is confirmed. Save this link to join the event:\n[Join Event]({plan.WeeklyMeetingLink})\n\nManage your ticket via your portal:\n[Dashboard]({portalMagicLink})";
            fallbackWhatsapp = $"Hi {profile.Full_name}, your ticket for {plan.Name} is confirmed! Event link: {plan.WeeklyMeetingLink} Portal: {portalMagicLink}";
        } 
        else 
        {
            fallbackEmail = $"Hi {profile.Full_name},\n\nYour subscription to {plan.Name} is now active. Access your portal here:\n[Dashboard]({portalMagicLink})";
            fallbackWhatsapp = $"Hi {profile.Full_name}, your subscription to {plan.Name} is now active. Access your portal here: {portalMagicLink}";
        }

        var emailBody = template != null ? RenderEmailTemplate(template.Email_body, variables) : MarkdownParser.ToHtml(fallbackEmail);
        var whatsappBody = template != null ? RenderWhatsAppTemplate(template.Whatsapp_body, variables) : MarkdownParser.ToPlainText(fallbackWhatsapp);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, emailBody, whatsappBody, template?.Channel ?? "EMAIL"));
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

        var subject = template != null ? RenderWhatsAppTemplate(template.Subject, variables) : "Subscription Cancelled";
        var fallbackContent = $"Hi {profile.Full_name},\n\nYour subscription has been cancelled.";

        var emailBody = template != null ? RenderEmailTemplate(template.Email_body, variables) : MarkdownParser.ToHtml(fallbackContent);
        var whatsappBody = template != null ? RenderWhatsAppTemplate(template.Whatsapp_body, variables) : MarkdownParser.ToPlainText(fallbackContent);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, emailBody, whatsappBody, template?.Channel ?? "EMAIL"));
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
        
        var renewalLink = sub.IsReminderOnly
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/{plan.Slug}/checkout";

        var magicToken = _tokenService.GenerateToken(sub.Id);
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.Full_name,
            ["business_name"] = "Our Community",
            ["plan_name"] = plan.Name,
            ["renewal_link"] = renewalLink,
            ["portal_magic_link"] = portalMagicLink
        };

        var subject = template != null ? RenderWhatsAppTemplate(template.Subject, variables) : "Renewal Reminder";
        var fallbackEmail = $"Renew here:\n[Renew Subscription]({renewalLink})";
        var fallbackWhatsapp = $"Renew here: {renewalLink}";

        var emailBody = template != null ? RenderEmailTemplate(template.Email_body, variables) : MarkdownParser.ToHtml(fallbackEmail);
        var whatsappBody = template != null ? RenderWhatsAppTemplate(template.Whatsapp_body, variables) : MarkdownParser.ToPlainText(fallbackWhatsapp);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, emailBody, whatsappBody, notification.Channel));
    }

    public async Task Handle(MagicLinkRequestedDomainEvent notification, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var rawEmail = $"Hi {profile.Full_name},\n\nClick the link below to access your subscriber portal to manage or cancel your subscription. This link expires in 24 hours.\n\n[Access Portal]({notification.MagicLinkUrl})\n\n— Lazuar Support";
        var emailBody = MarkdownParser.ToHtml(rawEmail);
        
        var whatsappBody = $"Hi {profile.Full_name} 🔐 Here is your secure dashboard link (expires in 24h): {notification.MagicLinkUrl}";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, "Your Subscriber Portal Access", emailBody, whatsappBody, "EMAIL"));
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
        
        var renewalLink = sub.IsReminderOnly
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/{plan.Slug}/checkout";

        var magicToken = _tokenService.GenerateToken(sub.Id);
        var portalMagicLink = $"{baseUrl.TrimEnd('/')}/{tenantSlug}/community/portal?token={Uri.EscapeDataString(magicToken)}";

        string subject = "Important Update Regarding Your Subscription";
        string emailBody = "";
        string whatsappBody = "";

        if (!string.IsNullOrWhiteSpace(notification.CustomMessage))
        {
            var rawEmail = $"Hi {profile.Full_name},\n\n{notification.CustomMessage}";
            emailBody = MarkdownParser.ToHtml(rawEmail);
            whatsappBody = MarkdownParser.ToPlainText(rawEmail);
        }
        else if (notification.TemplateId.HasValue)
        {
            var template = (await _templateService.GetTemplatesAsync(new[] { notification.TemplateId.Value })).FirstOrDefault();
            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.Full_name,
                ["business_name"] = "Our Community",
                ["plan_name"] = plan.Name,
                ["total_price"] = (plan.Price * sub.Quantity).ToString("F2"),
                ["renewal_link"] = renewalLink,
                ["portal_magic_link"] = portalMagicLink,
                ["fulfillment_url"] = plan.FulfillmentFileUrl ?? ""
            };
            
            subject = template != null ? RenderWhatsAppTemplate(template.Subject, variables) : subject;
            var fallbackContent = $"Hi {profile.Full_name},\n\nNotification regarding your subscription.";
            
            emailBody = template != null ? RenderEmailTemplate(template.Email_body, variables) : MarkdownParser.ToHtml(fallbackContent);
            whatsappBody = template != null ? RenderWhatsAppTemplate(template.Whatsapp_body, variables) : MarkdownParser.ToPlainText(fallbackContent);
        }
        else return;

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(notification.OrganizationId, profile.Email, profile.Phone, subject, emailBody, whatsappBody, notification.Channel));
    }
}
