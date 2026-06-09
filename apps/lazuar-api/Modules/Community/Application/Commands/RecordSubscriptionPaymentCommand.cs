// apps/lazuar-api/Modules/Community/Application/Commands/RecordSubscriptionPaymentCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Manually register an offline or cash payment and force the subscription to active status.", "medium", "SUPER_ADMIN", "ADMIN")]
public record RecordSubscriptionPaymentCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string? ExternalReference,
    string RecordedBy,
    string? ReceiptUrl = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RecordSubscriptionPaymentCommandHandler : ICommandHandler<RecordSubscriptionPaymentCommand>
{
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;

    public RecordSubscriptionPaymentCommandHandler(
        ICommunitySubscriptionRepository subscriptionRepository, 
        ICommunityPlanRepository planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task Handle(RecordSubscriptionPaymentCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        if (plan == null)
            throw new InvalidOperationException("Plan not found.");

        var now = DateTime.UtcNow;
        var intervalDays = plan.Interval == "yr" ? 365 : 30;
        
        var baseDate = (subscription.CurrentPeriodEnd.HasValue && subscription.CurrentPeriodEnd.Value > now) 
            ? subscription.CurrentPeriodEnd.Value 
            : now;
            
        var periodStart = now;
        var periodEnd = baseDate.AddDays(intervalDays);

        subscription.Activate(
            periodStart, 
            periodEnd, 
            request.Amount, 
            request.Currency, 
            request.PaymentMethod, 
            request.ExternalReference, 
            request.RecordedBy, 
            request.ReceiptUrl);

        await _subscriptionRepository.SaveChangesAsync(ct);
    }
}
