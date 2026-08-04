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

    public Task<string?> SendEmailAsync(
        string to,
        string subject,
        string body,
        Guid? organizationId = null,
        string? tenantApiKey = null,
        string? tenantSenderEmail = null,
        string? unsubscribeUrl = null)
    {
        var authMethod = string.IsNullOrWhiteSpace(tenantApiKey) ? "Platform Fallback" : "BYOK Context";
        var providerId = $"console_{Guid.CreateVersion7():N}";
        _logger.LogInformation(
            "[Local Dispatch] [EMAIL] Auth: {Auth} | Org: {Org} | To: {To} | Subject: {Subject} | ProviderId: {ProviderId} | Unsubscribe: {Unsubscribe} | Body: {Body}",
            authMethod, organizationId, to, subject, providerId, unsubscribeUrl ?? "(none)", body);
        return Task.FromResult<string?>(providerId);
    }
}
