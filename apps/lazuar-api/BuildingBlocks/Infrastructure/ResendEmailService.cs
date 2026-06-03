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

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[Resend] API Key is missing. Falling back to console log.\nTo: {To}\nSubject: {Subject}\nBody: {Body}", to, subject, body);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Resend");
            
            var payload = new
            {
                from = _options.SenderEmail,
                to = new[] { to },
                subject = subject,
                html = body // Using HTML payload, replace with 'text' if you send plaintext
            };

            var response = await client.PostAsJsonAsync("emails", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Resend] Failed to send email to {To}. Status: {Status}. Error: {Error}", to, response.StatusCode, error);
                
                // Throwing ensures the Outbox/Inbox worker will mark the message as FAILED
                // and automatically retry it later!
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
