using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Modules.Communications.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

/// <summary>
/// GDPR fan-out: suppress pre-wipe email so transactional/marketing mail cannot be re-sent.
/// </summary>
public class ClientProfileAnonymizedIntegrationEventHandler : IIntegrationEventHandler<ClientProfileAnonymizedIntegrationEvent>
{
    private readonly ISuppressionService _suppressionService;
    private readonly ILogger<ClientProfileAnonymizedIntegrationEventHandler> _logger;

    public ClientProfileAnonymizedIntegrationEventHandler(
        ISuppressionService suppressionService,
        ILogger<ClientProfileAnonymizedIntegrationEventHandler> logger)
    {
        _suppressionService = suppressionService;
        _logger = logger;
    }

    public async Task HandleAsync(ClientProfileAnonymizedIntegrationEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            _logger.LogInformation(
                "ClientProfileAnonymized: no pre-wipe email for profile {ProfileId}; skip suppression.",
                @event.ClientProfileId);
            return;
        }

        // Skip synthetic anonymized placeholders if a re-fire ever arrives after wipe.
        if (@event.Email.StartsWith("deleted_", System.StringComparison.OrdinalIgnoreCase)
            && @event.Email.EndsWith("@localhost", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _suppressionService.SuppressAsync(
            @event.OrganizationId,
            @event.Email,
            "ANONYMIZED",
            "gdpr_client_profile_anonymized");

        _logger.LogInformation(
            "ClientProfileAnonymized: suppressed email for profile {ProfileId} org {OrgId}.",
            @event.ClientProfileId, @event.OrganizationId);
    }
}
