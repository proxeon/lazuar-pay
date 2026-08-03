using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts;
using Modules.Communications.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class DispatchMessageIntegrationEventHandler : IIntegrationEventHandler<DispatchMessageIntegrationEvent>
{
    private readonly IEmailService _emailService;
    private readonly IMessagingService _messagingService;
    private readonly IBillingQueryService _billingQueryService;
    private readonly ICreditCostService _creditCostService;
    private readonly ISuppressionService _suppressionService;
    private readonly ICommunicationsQueryService _communicationsQueryService;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DispatchMessageIntegrationEventHandler> _logger;

    public DispatchMessageIntegrationEventHandler(
        IEmailService emailService,
        IMessagingService messagingService,
        IBillingQueryService billingQueryService,
        ICreditCostService creditCostService,
        ISuppressionService suppressionService,
        ICommunicationsQueryService communicationsQueryService,
        IMediator mediator,
        IConfiguration configuration,
        ILogger<DispatchMessageIntegrationEventHandler> logger)
    {
        _emailService = emailService;
        _messagingService = messagingService;
        _billingQueryService = billingQueryService;
        _creditCostService = creditCostService;
        _suppressionService = suppressionService;
        _communicationsQueryService = communicationsQueryService;
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(DispatchMessageIntegrationEvent @event)
    {
        var isSystemTenant = @event.OrganizationId == Guid.Empty
            || @event.OrganizationId.ToString() == "00000000-0000-0000-0000-000000000001";

        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);

        var wantsEmail = @event.Channel is "EMAIL" or "ALL"
            && !string.IsNullOrWhiteSpace(@event.ToEmail)
            && !string.IsNullOrWhiteSpace(@event.HtmlEmailBody);
        var wantsWhatsApp = @event.Channel is "WHATSAPP" or "ALL"
            && !string.IsNullOrWhiteSpace(@event.ToPhone)
            && !string.IsNullOrWhiteSpace(@event.PlainTextPhoneBody);

        if (!whatsAppEnabled && wantsWhatsApp)
        {
            _logger.LogInformation(
                "WhatsApp channel disabled (Messaging:WhatsAppEnabled=false). Skipping WhatsApp for Organization {OrganizationId}, Channel {Channel}, Event {EventId}.",
                @event.OrganizationId, @event.Channel, @event.Id);
            wantsWhatsApp = false;
        }

        if (wantsEmail && !isSystemTenant && await _suppressionService.IsSuppressedAsync(@event.OrganizationId, @event.ToEmail))
        {
            _logger.LogInformation("Skipping email to {Email} for tenant {OrganizationId}: address is suppressed.", @event.ToEmail, @event.OrganizationId);
            wantsEmail = false;
        }

        var whatsappCost = _creditCostService.GetCost(CreditAction.WhatsAppSend);
        var billedViaHold = @event.CreditHoldId.HasValue;
        var whatsAppBlockedByCredits = false;

        if (!isSystemTenant && !billedViaHold && wantsWhatsApp)
        {
            if (whatsappCost > 0)
            {
                var sufficient = await _billingQueryService.HasSufficientCreditsAsync(@event.OrganizationId, whatsappCost);
                if (!sufficient)
                {
                    _logger.LogError(
                        "Tenant {OrganizationId} has insufficient credits ({PlannedCost}) for WhatsApp message dispatch. WhatsApp delivery aborted (Channel={Channel}, Event={EventId}).",
                        @event.OrganizationId, whatsappCost, @event.Channel, @event.Id);
                    wantsWhatsApp = false;
                    whatsAppBlockedByCredits = true;
                }
            }
        }

        int actualCost = 0;
        var emailSent = false;

        if (wantsEmail)
        {
            string? tenantApiKey = null;
            string? tenantSenderEmail = null;

            if (!isSystemTenant)
            {
                var emailConfig = await _communicationsQueryService.GetEmailConfigAsync(@event.OrganizationId);
                // Fix: Use NSwag generated C# PascalCase properties
                if (emailConfig != null && emailConfig.Is_active && !string.IsNullOrWhiteSpace(emailConfig.Api_key) && !string.IsNullOrWhiteSpace(emailConfig.Sender_email))
                {
                    tenantApiKey = emailConfig.Api_key;
                    tenantSenderEmail = emailConfig.Sender_email;
                }
            }

            var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody!, @event.UnsubscribeUrl);
            await _emailService.SendEmailAsync(
                @event.ToEmail,
                @event.Subject,
                htmlPayload,
                @event.OrganizationId,
                tenantApiKey,
                tenantSenderEmail,
                @event.UnsubscribeUrl);
            emailSent = true;
        }

        if (wantsWhatsApp)
        {
            await _messagingService.SendMessageAsync(@event.ToPhone!, @event.PlainTextPhoneBody!);
            actualCost += whatsappCost;
        }

        if (actualCost > 0 && !isSystemTenant && !billedViaHold)
        {
            try
            {
                await _mediator.Send(new DeductTenantCreditCommand(
                    @event.OrganizationId,
                    actualCost,
                    $"Automated message dispatch ({@event.Channel})",
                    @event.Id.ToString()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message dispatched to tenant {OrganizationId} but credit deduction failed. Credits not consumed.", @event.OrganizationId);
            }
        }

        // Pure WhatsApp credit failure must not look like a silent success — throw so outbox retries.
        // Mixed EMAIL+WA where email succeeded: WA failure is already logged at Error; do not fail the whole dispatch.
        if (whatsAppBlockedByCredits && !emailSent)
        {
            throw new InvalidOperationException(
                $"Insufficient WhatsApp credits for organization {@event.OrganizationId}; channel={@event.Channel}, event={@event.Id}.");
        }
    }
}
