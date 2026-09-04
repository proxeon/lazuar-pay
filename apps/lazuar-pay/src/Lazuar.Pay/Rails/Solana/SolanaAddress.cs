namespace Lazuar.Pay.Rails.Solana;

public static class SolanaAddress
{
    public static bool TryNormalize(string? raw, out string address)
    {
        address = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (LooksLikeSecret(trimmed))
        {
            return false;
        }

        if (!SolanaBase58.TryDecode(trimmed, out var bytes) || bytes.Length != 32)
        {
            return false;
        }

        address = SolanaBase58.Encode(bytes);
        return address.Length is >= 32 and <= 44;
    }

    public static string Last4(string address) =>
        address.Length >= 4 ? address[^4..] : address;

    public static bool LooksLikeSecret(string raw)
    {
        if (raw.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("-----END", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("-----", StringComparison.Ordinal)
            || raw.Contains(' ')
            || raw.Contains(':')
            || raw.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("http://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var prefix in new[] { "sk_", "rk_", "whsec_", "lzr_sk_" })
        {
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (raw.Length is 64 or 128)
        {
            var hex = true;
            foreach (var c in raw)
            {
                if (!char.IsAsciiHexDigit(c))
                {
                    hex = false;
                    break;
                }
            }

            if (hex)
            {
                return true;
            }
        }

        return false;
    }
}
