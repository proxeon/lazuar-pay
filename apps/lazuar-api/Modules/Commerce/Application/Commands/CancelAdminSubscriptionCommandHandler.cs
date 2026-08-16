using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CancelAdminSubscriptionCommandHandler : ICommandHandler<CancelAdminSubscriptionCommand, string>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IAuditRecorder? _auditRecorder;

    public CancelAdminSubscriptionCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _auditRecorder = auditRecorder;
    }

    public async Task<string> Handle(CancelAdminSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        var status = await SubscriptionCancelApplier.ApplyAndPersistAsync(
            _repository,
            _eventBus,
            subscription,
            request.AtPeriodEnd,
            canceledStatus: "CANCELED",
            ct);

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                request.OrganizationId,
                "subscriber.canceled",
                "subscription",
                request.SubscriptionId.ToString(),
                new { at_period_end = request.AtPeriodEnd, status },
                ct: ct);
        }

        return status;
    }
}
