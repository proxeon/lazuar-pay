using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
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

public class DispatchExternalWebhookCommandHandler : ICommandHandler<DispatchExternalWebhookCommand>
{
    private readonly ILhdnRepository _repository;
    private readonly IWebhookSenderService _webhookSender;

    public DispatchExternalWebhookCommandHandler(ILhdnRepository repository, IWebhookSenderService webhookSender)
    {
        _repository = repository;
        _webhookSender = webhookSender;
    }

    public async Task Handle(DispatchExternalWebhookCommand request, CancellationToken ct)
    {
        var webhooks = await _repository.GetActiveWebhooksAsync(request.OrganizationId, ct);

        var qrLink = (!string.IsNullOrEmpty(request.LhdnUuid) && !string.IsNullOrEmpty(request.LongId))
            ? $"https://myinvois.hasil.gov.my/{request.LhdnUuid}/share/{request.LongId}"
            : null;

        var payload = new
        {
            @event = $"invoice.{request.Status.ToLowerInvariant()}",
            data = new
            {
                internal_id = request.InternalId,
                lhdn_uuid = request.LhdnUuid,
                status = request.Status,
                qr_link = qrLink,
                error_message = request.ErrorMessage,
                timestamp = DateTime.UtcNow
            }
        };

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        foreach (var webhook in webhooks)
        {
            await _webhookSender.SendWebhookAsync(webhook, payloadJson, ct);
        }
    }
}
