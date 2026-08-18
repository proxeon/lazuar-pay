using System;
using BuildingBlocks.Application;

namespace Modules.Communications.Infrastructure.Services;

/// <summary>
/// Resolves a stored tenant Resend key. Decrypt must succeed, or the stored
/// value must already be a plaintext <c>re_</c> key. Garbage ciphertext is not a live key.
/// </summary>
public static class TenantEmailKey
{
    public static bool TryResolve(ISecretVault vault, string? stored, out string plainKey)
    {
        plainKey = "";
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            var decrypted = vault.Decrypt(stored);
            if (!string.IsNullOrWhiteSpace(decrypted))
            {
                plainKey = decrypted;
                return true;
            }
        }
        catch
        {
            if (stored.StartsWith("re_", StringComparison.Ordinal))
            {
                plainKey = stored;
                return true;
            }

            return false;
        }

        return false;
    }
}
