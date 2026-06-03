// apps/lazuar-api/BuildingBlocks/Infrastructure/ConsoleEmailService.cs
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure;

public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("[Local Dispatch] [EMAIL] To: {To} | Subject: {Subject} | Body: {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
