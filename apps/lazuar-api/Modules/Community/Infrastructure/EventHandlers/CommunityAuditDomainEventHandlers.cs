using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Community.Domain.Events;

namespace Modules.Community.Infrastructure.EventHandlers;

public class CommunityAuditDomainEventHandlers :
    INotificationHandler<SubscriptionGracePeriodExtendedDomainEvent>,
    INotificationHandler<SubscriptionRemindersPausedDomainEvent>,
    INotificationHandler<SubscriptionProfileUpdatedDomainEvent>,
    INotificationHandler<PlanUpdatedDomainEvent>,
    INotificationHandler<PlanArchivedDomainEvent>
{
    private readonly ILogger<CommunityAuditDomainEventHandlers> _logger;

    public CommunityAuditDomainEventHandlers(ILogger<CommunityAuditDomainEventHandlers> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionGracePeriodExtendedDomainEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[AUDIT] [COMMUNITY] Subscription {SubscriptionId} (Tenant: {OrgId}, Profile: {ProfileId}) grace period extended by {Days} days. New due date: {NewDate:yyyy-MM-dd HH:mm} UTC.",
            notification.SubscriptionId,
            notification.OrganizationId,
            notification.ClientProfileId,
            notification.ExtendedDays,
            notification.NewRenewalDate);

        return Task.CompletedTask;
    }

    public Task Handle(SubscriptionRemindersPausedDomainEvent notification, CancellationToken ct)
    {
        var status = notification.PauseUntil.HasValue
            ? $"PAUSED until {notification.PauseUntil.Value:yyyy-MM-dd HH:mm} UTC"
            : "RESUMED (Unpaused)";

        _logger.LogInformation(
            "[AUDIT] [COMMUNITY] Subscription {SubscriptionId} (Tenant: {OrgId}, Profile: {ProfileId}) reminders status: {Status}.",
            notification.SubscriptionId,
            notification.OrganizationId,
            notification.ClientProfileId,
            status);

        return Task.CompletedTask;
    }

    public Task Handle(SubscriptionProfileUpdatedDomainEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[AUDIT] [COMMUNITY] Subscription {SubscriptionId} (Tenant: {OrgId}) profile configurations modified. Reminder Only: {IsReminderOnly} | Channel: {Channel} | Renewal Override: {RenewalOverride:yyyy-MM-dd}.",
            notification.SubscriptionId,
            notification.OrganizationId,
            notification.IsReminderOnly,
            notification.PreferredChannel ?? "Auto (Both)",
            notification.NextRenewalDate);

        return Task.CompletedTask;
    }

    public Task Handle(PlanUpdatedDomainEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[AUDIT] [COMMUNITY] Plan {PlanId} (Tenant: {OrgId}, Slug: '{Slug}') updated. Name: '{Name}' | Price: RM {Price:F2}.",
            notification.PlanId,
            notification.OrganizationId,
            notification.Slug,
            notification.Name,
            notification.Price);

        return Task.CompletedTask;
    }

    public Task Handle(PlanArchivedDomainEvent notification, CancellationToken ct)
    {
        _logger.LogWarning(
            "[AUDIT] [COMMUNITY] Plan {PlanId} (Tenant: {OrgId}, Slug: '{Slug}') was permanently ARCHIVED.",
            notification.PlanId,
            notification.OrganizationId,
            notification.Slug);

        return Task.CompletedTask;
    }
}
