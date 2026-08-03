using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Infrastructure.Commands;

public class UpdatePaymentConfigCommandHandler : ICommandHandler<UpdatePaymentConfigCommand>
{
    private readonly PaymentsDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public UpdatePaymentConfigCommandHandler(
        PaymentsDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task Handle(UpdatePaymentConfigCommand request, CancellationToken ct)
    {
        var config = await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId && c.GatewayType == request.GatewayType.ToUpperInvariant(), ct);

        var finalApiKey = string.IsNullOrEmpty(request.ApiKey) || request.ApiKey.Contains("••••") 
            ? config?.ApiKey 
            : request.ApiKey.Trim();

        var finalWebhookSecret = string.IsNullOrEmpty(request.WebhookSecret) || request.WebhookSecret.Contains("••••") 
            ? config?.WebhookSecret 
            : request.WebhookSecret.Trim();

        var finalSecretKey = string.IsNullOrEmpty(request.SecretKey) || request.SecretKey.Contains("••••") 
            ? config?.ApiKey // In Stripe, SecretKey replaces ApiKey
            : request.SecretKey.Trim();
            
        var finalMerchantId = string.IsNullOrEmpty(request.MerchantId) || request.MerchantId.Contains("••••") 
            ? config?.MerchantId 
            : request.MerchantId.Trim();

        var resolvedGatewayKey = request.GatewayType.ToUpperInvariant() == "STRIPE" ? finalSecretKey : finalApiKey;

        // Automatically fetch RSA Public Key and register webhooks when a new CHIP key is supplied
        if (request.GatewayType.ToUpperInvariant() == "CHIP" && 
            !string.IsNullOrEmpty(request.ApiKey) && 
            !request.ApiKey.Contains("••••") &&
            !string.IsNullOrEmpty(resolvedGatewayKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resolvedGatewayKey);
                
                var pubKeyResponse = await client.GetAsync("https://gate.chip-in.asia/api/v1/public_key/", ct);
                pubKeyResponse.EnsureSuccessStatusCode();
                var rawKey = await pubKeyResponse.Content.ReadAsStringAsync(ct);
                
                // Format the JSON string literal into a valid PEM payload
                finalWebhookSecret = rawKey.Trim('"').Replace("\\n", "\n");

                var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
                var webhookUrl = $"{apiBaseUrl}/webhooks/payments/chip/{request.OrganizationId}";
                
                // Ensure local development hooks don't break external API validation
                if (webhookUrl.Contains("localhost"))
                {
                    webhookUrl = webhookUrl.Replace("localhost", "lazuar-local-dev.com");
                }

                var webhookPayload = new
                {
                    title = "Lazuar Platform Webhook",
                    events = new[] { "purchase.paid", "purchase.payment_failure", "payment.refunded", "purchase.preauthorized" },
                    callback = webhookUrl
                };

                var webhookResponse = await client.PostAsJsonAsync("https://gate.chip-in.asia/api/v1/webhooks/", webhookPayload, ct);
                webhookResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new BusinessRuleValidationException(new GenericBusinessRule($"Failed to setup CHIP Collect. Please verify your API Key. Detail: {ex.Message}"));
            }
        }

        if (config == null)
        {
            config = new TenantPaymentConfiguration(
                request.OrganizationId,
                request.GatewayType,
                resolvedGatewayKey,
                finalWebhookSecret,
                finalMerchantId);
            _context.TenantPaymentConfigurations.Add(config);
        }
        else
        {
            config.UpdateCredentials(
                request.GatewayType,
                resolvedGatewayKey,
                finalWebhookSecret,
                finalMerchantId);
        }
        
        await _context.SaveChangesAsync(ct);
    }
}
