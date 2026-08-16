using System;

namespace Modules.Payments.Domain;

public static class PaymentGatewayEnvironment
{
    public const string Test = "test";
    public const string Live = "live";
    public const string MetadataKey = "hub_payment_environment";

    public static string Normalize(string? raw)
    {
        if (string.Equals(raw, Live, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "production", StringComparison.OrdinalIgnoreCase))
        {
            return Live;
        }

        return Test;
    }

    public static string? InferFromStripeShapedKey(string? plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey))
        {
            return null;
        }

        var k = plainKey.Trim();
        if (k.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase))
        {
            return Live;
        }

        if (k.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
        {
            return Test;
        }

        return null;
    }
}
