// apps/lazuar-api/Modules/Community/Application/Commands/RequestRefundCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Process a refund for a specific payment record.", "high", "SUPER_ADMIN", "ADMIN")]
public record RequestRefundCommand(Guid OrganizationId, Guid SubscriptionId, Guid PaymentRecordId, string? Reason) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RequestRefundCommandHandler : ICommandHandler<RequestRefundCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public RequestRefundCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RequestRefundCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        var payment = subscription.PaymentRecords.FirstOrDefault(p => p.Id == request.PaymentRecordId);
        if (payment == null)
            throw new InvalidOperationException("Payment record not found on this subscription.");

        if (payment.Status != "CONFIRMED")
            throw new InvalidOperationException("Only confirmed payments can be refunded.");

        subscription.RequestRefund(payment.Id);

        await _repository.SaveChangesAsync(ct);
    }
}
