using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Contracts;
using Modules.Community.Domain.Events;
using Modules.CRM.Contracts; // <-- ADDED

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
    private readonly ICommunityLinkService _linkService;
    private readonly ICrmQueryService _crmQueryService;

    public DomainEventHandlers(
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus,
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityPlanRepository planRepository,
        ICommunityLinkService linkService,
        ICrmQueryService crmQueryService)
    {
        _eventBus = eventBus;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _linkService = linkService;
        _crmQueryService = crmQueryService;
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken ct)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        var plan = sub != null ? await _planRepository.GetByIdAsync(sub.PlanId, ct) : null;
        var profile = await _crmQueryService.GetClientProfileAsync(notification.ClientProfileId);

        if (sub == null || plan == null) return;

        var latestPayment = sub.PaymentRecords.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var amountPaid = latestPayment?.Amount ?? plan.Price;

        await _eventBus.PublishAsync(
            new CommunitySubscriptionActivatedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                profile?.GlobalUserId, // <-- ADDED: Pass the GlobalUserId
                notification.IsFirstPayment,
                plan.Name,
                plan.TelegramInviteLink ?? "(link coming soon)",
                plan.WeeklyMeetingLink ?? "(link coming soon)",
                amountPaid
            )
        );
    }

    // ... rest of the handlers remain exactly the same ...
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

        var baseUrl = _linkService.GetCommunityBaseUrl();
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
        var sub = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        var plan = sub != null ? await _planRepository.GetByIdAsync(sub.PlanId, ct) : null;

        if (sub == null || plan == null) return;

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var renewalLink = sub.IsReminderOnly
            ? $"Please remit payment directly. Notes: {sub.AdminNotes ?? "Contact us for payment details"}"
            : $"{baseUrl}/{plan.Slug}/checkout";

        await _eventBus.PublishAsync(
            new CommunityOneOffReminderRequestedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.TemplateId,
                notification.CustomMessage,
                notification.Channel,
                notification.ScheduledAt,
                plan.Name,
                plan.Price,
                renewalLink
            )
        );
    }
}
