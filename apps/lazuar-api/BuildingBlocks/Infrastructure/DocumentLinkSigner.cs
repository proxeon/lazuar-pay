using System;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// HMAC-SHA256 signed document / draft links (payload + expiry).
/// Shared by final ledger PDFs and draft proforma downloads.
/// </summary>
public static class DocumentLinkSigner
{
    public static long ExpiryUnixSeconds(TimeSpan lifetime) =>
        DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();

    public static string Sign(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, payloadBytes)).ToLowerInvariant();
    }

    public const int ClockSkewSeconds = 60;

    public static bool TryValidate(string secret, string payload, string? sig, long exp, out string? error)
    {
        error = null;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp + ClockSkewSeconds)
        {
            error = "This secure document link has expired.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sig))
        {
            error = "Missing document signature.";
            return false;
        }

        var expected = Sign(secret, payload);
        byte[] expectedBytes;
        byte[] actualBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expected);
            actualBytes = Convert.FromHexString(sig);
        }
        catch (FormatException)
        {
            error = "Invalid document signature.";
            return false;
        }

        if (expectedBytes.Length != actualBytes.Length
            || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            error = "Invalid document signature.";
            return false;
        }

        return true;
    }

    /// <summary>Final issued document: <c>tenantSlug:ledgerEntryId:exp</c>.</summary>
    public static string FinalDocumentPayload(string tenantSlug, Guid ledgerEntryId, long exp) =>
        $"{tenantSlug}:{ledgerEntryId}:{exp}";

    /// <summary>Draft proforma: <c>tenantSlug:draft:sessionId:exp</c>.</summary>
    public static string DraftDocumentPayload(string tenantSlug, Guid sessionId, long exp) =>
        $"{tenantSlug}:draft:{sessionId}:{exp}";

    public static string ResolveSecret(string? configuredSecret)
    {
        if (string.IsNullOrWhiteSpace(configuredSecret)
            || configuredSecret == "secure_development_key_minimum_32_characters_long")
        {
            throw new InvalidOperationException(
                "Jwt:Secret is required to sign document links and must not be the well-known development default.");
        }

        return configuredSecret;
    }
}
