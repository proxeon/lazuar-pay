using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class DispatchMessageIntegrationEventHandler : IIntegrationEventHandler<DispatchMessageIntegrationEvent>
{
    private readonly IEmailService _emailService;
    private readonly IMessagingService _messagingService;

    public DispatchMessageIntegrationEventHandler(IEmailService emailService, IMessagingService messagingService)
    {
        _emailService = emailService;
        _messagingService = messagingService;
    }

    public async Task HandleAsync(DispatchMessageIntegrationEvent @event)
    {
        if (@event.Channel is "EMAIL" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToEmail) && !string.IsNullOrWhiteSpace(@event.HtmlEmailBody))
        {
            var htmlPayload = EmailTemplateBuilder.WrapWithBrandHtml(@event.HtmlEmailBody);
            await _emailService.SendEmailAsync(@event.ToEmail, @event.Subject, htmlPayload);
        }

        if (@event.Channel is "WHATSAPP" or "ALL" && !string.IsNullOrWhiteSpace(@event.ToPhone) && !string.IsNullOrWhiteSpace(@event.PlainTextPhoneBody))
        {
            await _messagingService.SendMessageAsync(@event.ToPhone, @event.PlainTextPhoneBody);
        }
    }
}
