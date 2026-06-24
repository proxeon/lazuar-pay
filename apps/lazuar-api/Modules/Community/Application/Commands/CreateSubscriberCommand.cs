using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Contracts;
using Modules.Community.Domain.Aggregates;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Commands;

[AgentTool("Manually enroll a customer into a subscription plan.", "COMMUNITY", "medium", "SUPER_ADMIN", "ADMIN")]
public record CreateSubscriberCommand(
    Guid OrganizationId,
    string Name,
    string Email,
    string Phone,
    Guid PlanId,
    string Source,
    bool IsReminderOnly,
    string? PreferredChannel,
    decimal? AmountPaid,
    string? PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    string RecordedBy,
    DateTime? StartDate = null,
    DateTime? NextBillingDate = null,
    bool SendWelcomeEmail = true) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateSubscriberCommandHandler : ICommandHandler<CreateSubscriberCommand, Guid>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    public CreateSubscriberCommandHandler(
        ICommunityPlanRepository planRepository,
        ICommunitySubscriptionRepository subscriptionRepository,
        IMediator mediator,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _mediator = mediator;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(CreateSubscriberCommand request, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId, ct);
        if (plan == null || plan.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("The requested plan is invalid or does not belong to this organization.");
        }

        var profileCommand = new CreateClientProfileCommand(
            request.OrganizationId,
            request.Name,
            request.Email,
            request.Phone,
            null);

        var profileId = await _mediator.Send(profileCommand, ct);

        var subscription = new CommunitySubscription(
            request.OrganizationId,
            profileId,
            plan.Id,
            request.Source,
            request.IsReminderOnly,
            request.PreferredChannel,
            request.Notes);

        _subscriptionRepository.Add(subscription);

        var periodStart = request.StartDate ?? DateTime.UtcNow;
        var periodEnd = request.NextBillingDate ?? periodStart.AddDays(plan.Interval == "yr" ? 365 : 30);
        var isSilent = !request.SendWelcomeEmail;

        var amountPaid = request.AmountPaid ?? 0m;
        var actualPaymentMethod = request.PaymentMethod ?? (amountPaid > 0 ? "BANK_TRANSFER" : "CASH");

        subscription.Activate(
            periodStart,
            periodEnd,
            amountPaid,
            "MYR",
            actualPaymentMethod,
            request.ReferenceNumber,
            request.RecordedBy,
            null,
            isSilent);

        if (amountPaid > 0)
        {
            await _eventBus.PublishAsync(new CommunityManualPaymentRecordedIntegrationEvent(
                request.OrganizationId,
                subscription.Id,
                amountPaid,
                "MYR",
                actualPaymentMethod,
                request.ReferenceNumber
            ));
        }

        await _subscriptionRepository.SaveChangesAsync(ct);

        return subscription.Id;
    }
}
