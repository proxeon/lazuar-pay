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
    string? Error);

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
        string? merchantId);

    Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string webhookSecret,
        string rawBody,
        Dictionary<string, string> headers);

    Task<bool> IssueRefundAsync(
        string apiKey,
        string transactionId,
        decimal amount);

    Task<string> GenerateCustomerPortalAsync(
        string apiKey,
        string customerEmail,
        string returnUrl);
}

public interface IPaymentGatewayFactory
{
    IPaymentGatewayAdapter GetAdapter(string gatewayType);
}
