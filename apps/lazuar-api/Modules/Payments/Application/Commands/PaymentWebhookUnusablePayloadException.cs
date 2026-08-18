using System;

namespace Modules.Payments.Application.Commands;

/// <summary>
/// Signature verified (or construct succeeded) but the payload cannot be fulfilled.
/// HTTP 400 so the gateway stops retrying. Bad HMAC stays <see cref="InvalidOperationException"/> (500).
/// </summary>
public sealed class PaymentWebhookUnusablePayloadException : InvalidOperationException
{
    public PaymentWebhookUnusablePayloadException(string message) : base(message)
    {
    }
}
