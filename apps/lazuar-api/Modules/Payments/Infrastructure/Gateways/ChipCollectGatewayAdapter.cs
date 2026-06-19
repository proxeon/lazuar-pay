using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;

namespace Modules.Payments.Infrastructure.Gateways;

public class ChipCollectGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChipCollectGatewayAdapter> _logger;

    public ChipCollectGatewayAdapter(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<ChipCollectGatewayAdapter> logger)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string GatewayType => "CHIP";

    public Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail, string successUrl, string cancelUrl,
        Dictionary<string, string> metadata, string? merchantId, bool setupFutureUsage = false)
    {
        throw new NotImplementedException("Implementation will be added in Phase 3.");
    }

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey, string webhookSecret, string rawBody, Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0, decimal fixedFee = 0, decimal taxRate = 0)
    {
        throw new NotImplementedException("Implementation will be added in Phase 4.");
    }

    public Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, 
        string currency, string description, string receipt)
    {
        throw new NotImplementedException("Implementation will be added in Phase 5.");
    }

    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        throw new NotImplementedException("Implementation will be added in Phase 6.");
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("CHIP Collect does not provide a managed customer billing portal.");
    }
}
