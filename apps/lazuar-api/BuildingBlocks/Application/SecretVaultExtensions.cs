using System;

namespace BuildingBlocks.Application;

/// <summary>
/// Helpers for AES vault values that may still be legacy plaintext until re-saved.
/// </summary>
public static class SecretVaultExtensions
{
    /// <summary>
    /// Decrypts ciphertext; on crypto failure returns the original string (legacy plaintext rows).
    /// </summary>
    public static string DecryptOrPlaintext(this ISecretVault vault, string ciphertextOrPlain)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentException.ThrowIfNullOrEmpty(ciphertextOrPlain);

        try
        {
            return vault.Decrypt(ciphertextOrPlain);
        }
        catch
        {
            return ciphertextOrPlain;
        }
    }

    /// <summary>
    /// Decrypts when non-empty; null/whitespace stays null/empty. Legacy plaintext on decrypt failure.
    /// </summary>
    public static string? DecryptOrPlaintextNullable(this ISecretVault vault, string? ciphertextOrPlain)
    {
        if (string.IsNullOrWhiteSpace(ciphertextOrPlain))
        {
            return ciphertextOrPlain;
        }

        return vault.DecryptOrPlaintext(ciphertextOrPlain);
    }

    /// <summary>
    /// Last-4 hint of the plaintext secret (decrypt when possible).
    /// </summary>
    public static string? HintLast4(this ISecretVault vault, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        string plain;
        try
        {
            plain = vault.Decrypt(stored);
        }
        catch
        {
            plain = stored;
        }

        return plain.Length <= 4 ? "****" : $"…{plain[^4..]}";
    }

    /// <summary>
    /// True when the client sent an empty/mask value meaning "keep existing secret".
    /// </summary>
    public static bool IsKeepExistingSecret(string? incoming) =>
        string.IsNullOrWhiteSpace(incoming) || incoming.Contains("••••", StringComparison.Ordinal);
}
