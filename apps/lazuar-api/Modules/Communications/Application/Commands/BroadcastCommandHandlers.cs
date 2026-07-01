using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using MediatR;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Communications.Application;
using Modules.Communications.Contracts.Commands;
using Modules.Communications.Domain.Aggregates;
using Modules.Commerce.Contracts;

namespace Modules.Communications.Application.Commands;

public class SendBroadcastCommandHandler : ICommandHandler<SendBroadcastCommand, Guid>
{
    private readonly ICommunicationsRepository _repository;
    private readonly ISubscriberQueryService _subscriberQueryService;
    private readonly ICreditCostService _creditCostService;
    private readonly IMediator _mediator;

    public SendBroadcastCommandHandler(
        ICommunicationsRepository repository,
        ISubscriberQueryService subscriberQueryService,
        ICreditCostService creditCostService,
        IMediator mediator)
    {
        _repository = repository;
        _subscriberQueryService = subscriberQueryService;
        _creditCostService = creditCostService;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        if (!string.Equals(request.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleValidationException(new GenericBusinessRule("Only EMAIL broadcasts are supported in v1."));

        // Abuse protection: cooldown between broadcasts per tenant.
        if (await _repository.HasRecentBroadcastAsync(request.OrganizationId, TimeSpan.FromMinutes(1), ct))
            throw new BusinessRuleValidationException(new GenericBusinessRule("Another broadcast was sent very recently. Please wait a moment and try again."));

        var recipientCount = await _subscriberQueryService.GetActiveSubscriberCountAsync(request.OrganizationId);
        if (recipientCount == 0)
            throw new BusinessRuleValidationException(new GenericBusinessRule("No active subscribers to broadcast to."));

        var costPerRecipient = _creditCostService.GetCost(CreditAction.BroadcastEmailPerRecipient);
        var totalCredits = recipientCount * costPerRecipient;

        var broadcast = new Broadcast(request.OrganizationId, request.Subject, request.EmailBody);

        // Reserve credits atomically; throws on insufficient balance (propagates as 402 to the caller).
        var holdId = await _mediator.Send(new ReserveCreditsCommand(
            request.OrganizationId,
            totalCredits,
            broadcast.Id.ToString(),
            $"Broadcast: {request.Subject}"), ct);

        broadcast.Queue(recipientCount, holdId, totalCredits);

        _repository.AddBroadcast(broadcast);
        await _repository.SaveChangesAsync(ct);

        return broadcast.Id;
    }
}
