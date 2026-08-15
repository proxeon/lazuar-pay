using System;

namespace Modules.Payments.Application.Exceptions;

/// <summary>
/// Stable error codes for M2M integration checkout (ProblemDetails.code).
/// </summary>
public sealed class PaymentIntegrationException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public PaymentIntegrationException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public static PaymentIntegrationException PaymentsNotConfigured(string? gateway = null) =>
        new(
            PaymentErrorCodes.PaymentsNotConfigured,
            string.IsNullOrWhiteSpace(gateway)
                ? "No active payment gateway is configured for this workspace."
                : $"Payment gateway '{gateway}' is not configured or disabled for this workspace.",
            statusCode: 422);

    public static PaymentIntegrationException AmountInvalid(string detail) =>
        new(PaymentErrorCodes.AmountInvalid, detail);

    public static PaymentIntegrationException AmountBelowMinimum(decimal min, string currency) =>
        new(PaymentErrorCodes.AmountBelowMinimum, $"Amount must be at least {min} {currency}.");

    public static PaymentIntegrationException CurrencyInvalid(string detail) =>
        new(PaymentErrorCodes.CurrencyInvalid, detail);

    public static PaymentIntegrationException UrlsRequired(string detail) =>
        new(PaymentErrorCodes.UrlsRequired, detail);

    public static PaymentIntegrationException MetadataInvalid(string detail) =>
        new(PaymentErrorCodes.MetadataInvalid, detail);

    public static PaymentIntegrationException InvalidRequest(string detail) =>
        new(PaymentErrorCodes.InvalidRequest, detail);

    public static PaymentIntegrationException IdempotencyConflict() =>
        new(
            PaymentErrorCodes.IdempotencyConflict,
            "Idempotency key was already used with a different request payload.",
            statusCode: 409);

    public static PaymentIntegrationException GatewayError(string? detail) =>
        new(
            PaymentErrorCodes.GatewayError,
            string.IsNullOrWhiteSpace(detail) ? "Payment gateway failed to create checkout session." : detail,
            statusCode: 502);

    public static PaymentIntegrationException CallbackBaseNotPublic(string? detail) =>
        new(
            PaymentErrorCodes.CallbackBaseNotPublic,
            string.IsNullOrWhiteSpace(detail)
                ? "Pay App:ApiBaseUrl must be a public https origin Billplz can POST."
                : detail,
            statusCode: 422);
}

public static class PaymentErrorCodes
{
    public const string PaymentsNotConfigured = "PAYMENTS_NOT_CONFIGURED";
    public const string AmountInvalid = "AMOUNT_INVALID";
    public const string AmountBelowMinimum = "AMOUNT_BELOW_MINIMUM";
    public const string CurrencyInvalid = "CURRENCY_INVALID";
    public const string UrlsRequired = "URLS_REQUIRED";
    public const string MetadataInvalid = "METADATA_INVALID";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string GatewayError = "GATEWAY_ERROR";
    public const string CallbackBaseNotPublic = "CALLBACK_BASE_NOT_PUBLIC";
    public const string CheckoutNotFound = "CHECKOUT_NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string KeyModeMismatch = "KEY_MODE_MISMATCH";
}
