// apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Billing.Application.Commands;
using Modules.Billing.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class DispatchMessageIntegrationEventHandler : IIntegrationEventHandler<DispatchMessageIntegrationEvent>
{
    private readonly IEmailService _emailService;
    private readonly IMessagingService _messagingService;
    private readonly IBillingQueryService _billingQueryService;
    private readonly IMediator _mediator;
    private readonly ILogger<DispatchMessageIntegrationEventHandler> _logger;

    public DispatchMessageIntegrationEventHandler(
        IEmailService emailService, 
        IMessagingService messagingService,
        IBillingQueryService billingQueryService,
        IMediator mediator,
        ILogger<DispatchMessageIntegrationEventHandler> logger)
    {
        _emailService = emailService;
        _messagingService = messagingService;
        _billingQueryService = billingQueryService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(DispatchMessageIntegrationEvent @event)
    {
        // System Tenant (Guid.Empty or fallback) skips credit checks
        if (@event.OrganizationId != Guid.Empty && @event.OrganizationId.ToString() != "00000000-0000-0000-0000-000000000001")
        {
            var hasCredits = await _billingQueryService.HasPositiveCreditBalanceAsync(@event.OrganizationId);
            if (!hasCredits)
            {
                _logger.LogWarning("Tenant {OrganizationId} attempted to dispatch a message but has insufficient utility credits. Delivery aborted.", @event.OrganizationId);
                return;
            }
        }

        int deductAmount = 0;

        if (@event.Channel is "EMAIL" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToEmail) && !string.IsNullOrWhiteSpace(@event.HtmlEmailBody))
        {
            var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody);
            await _emailService.SendEmailAsync(@event.ToEmail, @event.Subject, htmlPayload);
            deductAmount++;
        }

        if (@event.Channel is "WHATSAPP" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToPhone) && !string.IsNullOrWhiteSpace(@event.PlainTextPhoneBody))
        {
            await _messagingService.SendMessageAsync(@event.ToPhone, @event.PlainTextPhoneBody);
            deductAmount++;
        }

        if (deductAmount > 0 && @event.OrganizationId != Guid.Empty && @event.OrganizationId.ToString() != "00000000-0000-0000-0000-000000000001")
        {
            await _mediator.Send(new DeductTenantCreditCommand(
                @event.OrganizationId, 
                deductAmount, 
                $"Automated message dispatch ({@event.Channel})"));
        }
    }
}
