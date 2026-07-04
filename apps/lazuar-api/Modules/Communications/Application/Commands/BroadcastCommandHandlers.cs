using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using MediatR;
using Modules.Communications.Application;
using Modules.Communications.Contracts.Commands;
using Modules.Communications.Domain.Aggregates;
using Modules.Commerce.Contracts;

namespace Modules.Communications.Application.Commands;

public class SendBroadcastCommandHandler : ICommandHandler<SendBroadcastCommand, Guid>
{
    private readonly ICommunicationsRepository _repository;
    private readonly ISubscriberQueryService _subscriberQueryService;

    public SendBroadcastCommandHandler(
        ICommunicationsRepository repository,
        ISubscriberQueryService subscriberQueryService)
    {
        _repository = repository;
        _subscriberQueryService = subscriberQueryService;
    }

    public async Task<Guid> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        if (!string.Equals(request.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleValidationException(new GenericBusinessRule("Only EMAIL broadcasts are supported in v1."));

        if (await _repository.HasRecentBroadcastAsync(request.OrganizationId, TimeSpan.FromMinutes(1), ct))
            throw new BusinessRuleValidationException(new GenericBusinessRule("Another broadcast was sent very recently. Please wait a moment and try again."));

        var recipientCount = await _subscriberQueryService.GetActiveSubscriberCountAsync(request.OrganizationId);
        if (recipientCount == 0)
            throw new BusinessRuleValidationException(new GenericBusinessRule("No active subscribers to broadcast to."));

        var broadcast = new Broadcast(request.OrganizationId, request.Subject, request.EmailBody);
        broadcast.Queue(recipientCount);

        _repository.AddBroadcast(broadcast);
        await _repository.SaveChangesAsync(ct);

        return broadcast.Id;
    }
}
