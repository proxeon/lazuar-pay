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

    public Task SendEmailAsync(string to, string subject, string body, Guid? organizationId = null)
    {
        _logger.LogInformation("[Local Dispatch] [EMAIL] Org: {Org} | To: {To} | Subject: {Subject} | Body: {Body}", organizationId, to, subject, body);
        return Task.CompletedTask;
    }
}
