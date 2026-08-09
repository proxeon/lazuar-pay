using Microsoft.Extensions.Logging;
using Modules.Messaging.Application;

namespace Modules.Messaging.Infrastructure.Messaging;

/// <summary>
/// Console stand-in for WhatsApp/SMS transport. Decision 00.4 freezes real WhatsApp product work.
/// </summary>
public sealed class ConsoleMessagingService : IMessagingService
{
    private readonly ILogger<ConsoleMessagingService> _logger;

    public ConsoleMessagingService(ILogger<ConsoleMessagingService> logger)
    {
        _logger = logger;
    }

    public Task SendMessageAsync(string recipient, string text)
    {
        _logger.LogInformation("[Local Dispatch] [MESSAGING/SMS] To: {Recipient} | Text: {Text}", recipient, text);
        return Task.CompletedTask;
    }
}
