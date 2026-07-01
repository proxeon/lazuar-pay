// apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class DispatchMessageIntegrationEventHandler : IIntegrationEventHandler<DispatchMessageIntegrationEvent>
{
    private readonly IEmailService _emailService;
    private readonly IMessagingService _messagingService;
    private readonly IBillingQueryService _billingQueryService;
    private readonly ICreditCostService _creditCostService;
    private readonly IMediator _mediator;
    private readonly ILogger<DispatchMessageIntegrationEventHandler> _logger;

    public DispatchMessageIntegrationEventHandler(
        IEmailService emailService, 
        IMessagingService messagingService,
        IBillingQueryService billingQueryService,
        ICreditCostService creditCostService,
        IMediator mediator,
        ILogger<DispatchMessageIntegrationEventHandler> logger)
    {
        _emailService = emailService;
        _messagingService = messagingService;
        _billingQueryService = billingQueryService;
        _creditCostService = creditCostService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(DispatchMessageIntegrationEvent @event)
    {
        var isSystemTenant = @event.OrganizationId == Guid.Empty
            || @event.OrganizationId.ToString() == "00000000-0000-0000-0000-000000000001";

        var wantsEmail = @event.Channel is "EMAIL" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToEmail) && !string.IsNullOrWhiteSpace(@event.HtmlEmailBody);
        var wantsWhatsApp = @event.Channel is "WHATSAPP" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToPhone) && !string.IsNullOrWhiteSpace(@event.PlainTextPhoneBody);

        var emailCost = _creditCostService.GetCost(CreditAction.EmailSend);
        var whatsappCost = _creditCostService.GetCost(CreditAction.WhatsAppSend);

        // Pre-check sufficiency (not just positive balance) so a multi-channel dispatch cannot
        // send on credits it cannot pay for. System tenant is exempt.
        if (!isSystemTenant)
        {
            var plannedCost = (wantsEmail ? emailCost : 0) + (wantsWhatsApp ? whatsappCost : 0);
            if (plannedCost > 0)
            {
                var sufficient = await _billingQueryService.HasSufficientCreditsAsync(@event.OrganizationId, plannedCost);
                if (!sufficient)
                {
                    _logger.LogWarning("Tenant {OrganizationId} has insufficient credits ({PlannedCost}) for message dispatch. Delivery aborted.", @event.OrganizationId, plannedCost);
                    return;
                }
            }
        }

        int actualCost = 0;

        if (wantsEmail)
        {
            var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody!);
            await _emailService.SendEmailAsync(@event.ToEmail, @event.Subject, htmlPayload);
            actualCost += emailCost;
        }

        if (wantsWhatsApp)
        {
            await _messagingService.SendMessageAsync(@event.ToPhone!, @event.PlainTextPhoneBody!);
            actualCost += whatsappCost;
        }

        if (actualCost > 0 && !isSystemTenant)
        {
            try
            {
                // Idempotent on the dispatch event id: a retried delivery cannot double-deduct.
                await _mediator.Send(new DeductTenantCreditCommand(
                    @event.OrganizationId,
                    actualCost,
                    $"Automated message dispatch ({@event.Channel})",
                    @event.Id.ToString()));
            }
            catch (Exception ex)
            {
                // The message was already delivered; propagating would cause a re-send on retry.
                // Log and accept the rare credit leakage rather than double-send.
                _logger.LogError(ex, "Message dispatched to tenant {OrganizationId} but credit deduction failed. Credits not consumed.", @event.OrganizationId);
            }
        }
    }
}
