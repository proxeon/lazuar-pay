using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Community.Contracts;
using Modules.Community.Domain.Events;

namespace Modules.Community.Application.EventHandlers;

public class DomainEventHandlers : 
    INotificationHandler<SubscriptionActivatedDomainEvent>,
    INotificationHandler<SubscriptionCancelledDomainEvent>,
    INotificationHandler<CheckoutInitiatedDomainEvent>,
    INotificationHandler<MagicLinkRequestedDomainEvent>,
    INotificationHandler<OneOffReminderRequestedDomainEvent>,
    INotificationHandler<SubscriptionRenewalDueDomainEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IConfiguration _configuration;

    public DomainEventHandlers(
        IEventBus eventBus,
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityPlanRepository planRepository,
        IConfiguration configuration)
    {
        _eventBus = eventBus;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _configuration = configuration;
    }

    private string GetCommunityBaseUrl()
    {
        var apiBaseUrl = _configuration["App:ApiBaseUrl"] ?? "";
        return apiBaseUrl.Contains("lazuar.com") 
            ? "https://community.lazuar.com" 
            : "http://localhost:3020";
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken ct)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        var plan = sub != null ? await _planRepository.GetByIdAsync(sub.PlanId, ct) : null;

        if (sub == null || plan == null) return;

        // Grab the most recent payment record amount, fallback to plan price
        var latestPayment = sub.PaymentRecords.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var amountPaid = latestPayment?.Amount ?? plan.Price;

        await _eventBus.PublishAsync(
            new CommunitySubscriptionActivatedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.IsFirstPayment,
                plan.Name,
                plan.TelegramInviteLink ?? "(link coming soon)",
                plan.WeeklyMeetingLink ?? "(link coming soon)",
                amountPaid
            )
        );
    }

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken ct)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        var plan = sub != null ? await _planRepository.GetByIdAsync(sub.PlanId, ct) : null;

        if (sub == null || plan == null) return;

        await _eventBus.PublishAsync(
            new CommunitySubscriptionCancelledIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                plan.Name,
                sub.CurrentPeriodEnd
            )
        );
    }

    public async Task Handle(SubscriptionRenewalDueDomainEvent notification, CancellationToken ct)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        var plan = sub != null ? await _planRepository.GetByIdAsync(sub.PlanId, ct) : null;

        if (sub == null || plan == null) return;

        var baseUrl = GetCommunityBaseUrl();
        var renewalLink = sub.IsReminderOnly 
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl}/{plan.Slug}/checkout";

        await _eventBus.PublishAsync(
            new CommunityRenewalReminderDueIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.TemplateId,
                notification.Channel,
                plan.Name,
                renewalLink
            )
        );
    }

    public async Task Handle(CheckoutInitiatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityCheckoutInitiatedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId
            )
        );
    }

    public async Task Handle(MagicLinkRequestedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityMagicLinkRequestedIntegrationEvent(
                notification.OrganizationId,
                notification.ClientProfileId,
                notification.MagicLinkUrl
            )
        );
    }

    public async Task Handle(OneOffReminderRequestedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityOneOffReminderRequestedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.TemplateId,
                notification.CustomMessage,
                notification.Channel
            )
        );
    }
}
