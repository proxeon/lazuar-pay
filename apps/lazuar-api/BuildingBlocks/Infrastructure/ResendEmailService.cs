using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure;

public sealed class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, Guid? organizationId = null, string? tenantApiKey = null, string? tenantSenderEmail = null)
    {
        string apiKey;
        string senderEmail;

        var isSystemTenant = organizationId == null || 
                             organizationId == Guid.Empty || 
                             organizationId.ToString() == "00000000-0000-0000-0000-000000000001";

        if (!string.IsNullOrWhiteSpace(tenantApiKey) && !string.IsNullOrWhiteSpace(tenantSenderEmail))
        {
            apiKey = tenantApiKey;
            senderEmail = tenantSenderEmail;
        }
        else if (isSystemTenant)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogWarning("[Resend] Platform API Key is missing. Falling back to console log.\nTo: {To}\nSubject: {Subject}\nBody: {Body}", to, subject, body);
                return;
            }
            apiKey = _options.ApiKey;
            senderEmail = _options.SenderEmail;
        }
        else
        {
            throw new InvalidOperationException("No platform fallback allowed for tenant emails. You must configure a valid BYOK Resend API key and Sender Email to dispatch tenant communications.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Resend");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = senderEmail,
                to = new[] { to },
                subject = subject,
                html = body,
                tags = organizationId.HasValue
                    ? new[] { new { name = "org", value = organizationId.Value.ToString() } }
                    : null
            };

            var response = await client.PostAsJsonAsync("emails", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Resend] Failed to send email to {To}. Status: {Status}. Error: {Error}", to, response.StatusCode, error);

                throw new InvalidOperationException($"Failed to send email via Resend: {error}");
            }

            _logger.LogInformation("[Resend] Successfully sent email to {To} with subject '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Resend] Exception occurred while sending email to {To}", to);
            throw;
        }
    }
}
