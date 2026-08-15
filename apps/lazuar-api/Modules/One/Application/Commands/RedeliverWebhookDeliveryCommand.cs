using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RedeliverWebhookDeliveryResult(
    Guid Id,
    string EventType,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTime CreatedAt);

public record RedeliverWebhookDeliveryCommand(Guid OrganizationId, Guid DeliveryId)
    : ICommand<RedeliverWebhookDeliveryResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RedeliverWebhookDeliveryCommandHandler
    : ICommandHandler<RedeliverWebhookDeliveryCommand, RedeliverWebhookDeliveryResult>
{
    private readonly IOneRepository _repository;

    public RedeliverWebhookDeliveryCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task<RedeliverWebhookDeliveryResult> Handle(
        RedeliverWebhookDeliveryCommand request,
        CancellationToken ct)
    {
        var source = await _repository.GetWebhookDeliveryAsync(
            request.OrganizationId, request.DeliveryId, ct)
            ?? throw new InvalidOperationException("Webhook delivery not found.");

        if (string.Equals(source.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Delivery is already pending.");
        }

        var endpoint = await _repository.GetWebhookEndpointByIdAsync(source.EndpointId, ct);
        if (endpoint is null
            || endpoint.OrganizationId != request.OrganizationId
            || !endpoint.IsActive)
        {
            throw new InvalidOperationException("Webhook endpoint is missing or inactive.");
        }

        var clone = new WebhookDeliveryOutbox(
            source.OrganizationId,
            source.EndpointId,
            source.EventType,
            source.Payload);

        _repository.AddWebhookDelivery(clone);
        await _repository.SaveChangesAsync(ct);

        return new RedeliverWebhookDeliveryResult(
            clone.Id,
            clone.EventType,
            clone.Status,
            clone.AttemptCount,
            clone.LastError,
            clone.CreatedAt);
    }
}
