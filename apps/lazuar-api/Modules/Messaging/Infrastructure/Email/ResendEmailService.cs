using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Messaging.Application;
using Modules.Messaging.Infrastructure.Configuration;

namespace Modules.Messaging.Infrastructure.Email;

/// <summary>
/// Resend HTTP adapter. Tags outbound mail with <c>org</c> = organizationId so
/// Communications inbound bounce/complaint webhooks can attribute suppressions.
/// Tenant mail requires BYOK (no platform key fallback). System tenant may use platform key.
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    /// <summary>Resend tag name used for bounce/complaint org attribution. Do not rename.</summary>
    public const string OrgTagName = "org";

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

    public async Task<string?> SendEmailAsync(
        string to,
        string subject,
        string body,
        Guid? organizationId = null,
        string? tenantApiKey = null,
        string? tenantSenderEmail = null,
        string? unsubscribeUrl = null)
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
                return null;
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

            Dictionary<string, string>? headers = null;
            if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            {
                headers = new Dictionary<string, string>
                {
                    // RFC 2369 List-Unsubscribe + RFC 8058 one-click
                    ["List-Unsubscribe"] = $"<{unsubscribeUrl}>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
                };
            }

            var payload = new
            {
                from = senderEmail,
                to = new[] { to },
                subject = subject,
                html = body,
                tags = organizationId.HasValue
                    ? new[] { new { name = OrgTagName, value = organizationId.Value.ToString() } }
                    : null,
                headers
            };

            var response = await client.PostAsJsonAsync("emails", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Resend] Failed to send email to {To}. Status: {Status}. Error: {Error}", to, response.StatusCode, error);

                throw new InvalidOperationException($"Failed to send email via Resend: {error}");
            }

            string? providerId = null;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    providerId = idProp.GetString();
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "[Resend] Sent email to {To} but could not parse provider id.", to);
            }

            _logger.LogInformation("[Resend] Successfully sent email to {To} with subject '{Subject}' (providerId={ProviderId})", to, subject, providerId);
            return providerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Resend] Exception occurred while sending email to {To}", to);
            throw;
        }
    }
}
