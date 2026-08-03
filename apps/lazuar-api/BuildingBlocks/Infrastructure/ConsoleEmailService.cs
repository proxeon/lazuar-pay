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

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        Guid? organizationId = null,
        string? tenantApiKey = null,
        string? tenantSenderEmail = null,
        string? unsubscribeUrl = null)
    {
        var authMethod = string.IsNullOrWhiteSpace(tenantApiKey) ? "Platform Fallback" : "BYOK Context";
        _logger.LogInformation(
            "[Local Dispatch] [EMAIL] Auth: {Auth} | Org: {Org} | To: {To} | Subject: {Subject} | Unsubscribe: {Unsubscribe} | Body: {Body}",
            authMethod, organizationId, to, subject, unsubscribeUrl ?? "(none)", body);
        return Task.CompletedTask;
    }
}
