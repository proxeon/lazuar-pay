using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Application.Commands;

public record DispatchExternalWebhookCommand(
    Guid OrganizationId,
    string InternalId,
    string Status,
    string? LhdnUuid,
    string? LongId,
    string? ErrorMessage) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// Enqueue LHDN invoice.valid / invoice.invalid via One's durable outbound path (R42/R43).
/// Publishes <see cref="OutboundWebhookRequestedIntegrationEvent"/> on LhdnEventBus only —
/// pure publish path; fire-and-forget sender retired (R43).
/// </summary>
public class DispatchExternalWebhookCommandHandler : ICommandHandler<DispatchExternalWebhookCommand>
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IEventBus _eventBus;
    private readonly ILhdnLinkService _linkService;
    private readonly ILhdnRepository _repository;

    public DispatchExternalWebhookCommandHandler(
        [FromKeyedServices("LhdnEventBus")] IEventBus eventBus,
        ILhdnLinkService linkService,
        ILhdnRepository repository)
    {
        _eventBus = eventBus;
        _linkService = linkService;
        _repository = repository;
    }

    public async Task Handle(DispatchExternalWebhookCommand request, CancellationToken ct)
    {
        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        var portalUrl = _linkService.GetPortalUrl(config?.Environment);

        var qrLink = (!string.IsNullOrEmpty(request.LhdnUuid) && !string.IsNullOrEmpty(request.LongId))
            ? $"{portalUrl}/{request.LhdnUuid}/share/{request.LongId}"
            : null;

        // Data-only payload; One wraps platform envelope (id, event_type, created_at, data).
        var dataObj = new
        {
            internal_id = request.InternalId,
            lhdn_uuid = request.LhdnUuid,
            status = request.Status,
            qr_link = qrLink,
            error_message = request.ErrorMessage
        };

        var payload = JsonSerializer.SerializeToElement(dataObj, PayloadJsonOptions);
        var eventType = $"invoice.{request.Status.ToLowerInvariant()}";

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            OrganizationId: request.OrganizationId,
            TargetUrl: null,
            EventType: eventType,
            Payload: payload));
    }
}
