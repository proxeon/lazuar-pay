// apps/lazuar-api/Modules/Community/Application/Commands/CreateSubscriberCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
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
    string RecordedBy) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateSubscriberCommandHandler : ICommandHandler<CreateSubscriberCommand, Guid>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly IMediator _mediator;

    public CreateSubscriberCommandHandler(
        ICommunityPlanRepository planRepository,
        ICommunitySubscriptionRepository subscriptionRepository,
        IMediator mediator)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _mediator = mediator;
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

        var periodStart = DateTime.UtcNow;
        var periodEnd = periodStart.AddDays(plan.Interval == "yr" ? 365 : 30);

        if (!request.IsReminderOnly && request.AmountPaid.HasValue)
        {
            subscription.Activate(
                periodStart,
                periodEnd,
                request.AmountPaid.Value,
                "MYR",
                request.PaymentMethod ?? "BANK_TRANSFER",
                request.ReferenceNumber,
                request.RecordedBy);
        }
        else
        {
            subscription.Activate(
                periodStart,
                periodEnd,
                0m,
                "MYR",
                "CASH",
                "MANUAL_ACTIVATION",
                request.RecordedBy);
        }

        await _subscriptionRepository.SaveChangesAsync(ct);

        return subscription.Id;
    }
}
