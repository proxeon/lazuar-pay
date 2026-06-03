// apps/lazuar-api/BuildingBlocks/Infrastructure/ConsoleMessagingService.cs
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure;

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
