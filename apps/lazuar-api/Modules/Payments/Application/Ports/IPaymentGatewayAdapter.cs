// apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Modules.Payments.Application.Ports;

public record GatewayCheckoutResult(bool Success, string? CheckoutUrl, string? SessionId, string? Error);

public record GatewayWebhookParsedResult(
    bool Verified,
    string EventType,
    string EventId,
    decimal AmountPaid,
    string Currency,
    string? GatewayTransactionId,
    Dictionary<string, string> Metadata,
    decimal GatewayFee,
    decimal TaxAmount,
    decimal NetAmount,
    decimal FxRate,
    string BaseCurrency,
    string? Error,
    string? GatewayCustomerId = null,
    string? GatewayTokenId = null,
    bool UnusableAfterVerify = false)
{
    public GatewayWebhookParsedResult AsUnusable() => this with { UnusableAfterVerify = true };
}

public interface IPaymentGatewayAdapter
{
    string GatewayType { get; }
    
    Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey,
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        string? merchantId,
        bool setupFutureUsage = false,
        int quantity = 1);
        
    Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey,
        string webhookSecret,
        string rawBody,
        Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0,
        decimal fixedFee = 0,
        decimal taxRate = 0);
        
    Task<bool> IssueRefundAsync(
        string apiKey,
        string transactionId,
        decimal amount);
        
    Task<string> GenerateCustomerPortalAsync(
        string apiKey,
        string customerEmail,
        string returnUrl);

    Task<bool> ChargeOffSessionAsync(
        string apiKey,
        string customerId,
        string tokenId,
        decimal amount,
        string currency,
        string description,
        string receipt,
        Guid tenantId,
        Guid? dunningCampaignId = null,
        string? idempotencyKey = null,
        Guid? chargeAttemptId = null,
        decimal taxAmount = 0,
        string? taxType = null);
}

public interface IPaymentGatewayFactory
{
    IPaymentGatewayAdapter GetAdapter(string gatewayType);
}
