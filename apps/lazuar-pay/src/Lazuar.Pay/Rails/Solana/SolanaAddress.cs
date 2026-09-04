using System.Numerics;

namespace Lazuar.Pay.Rails.Solana;

public static class SolanaAddress
{
    static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    static readonly BigInteger D = Mod(-121665 * ModPow(121666, P - 2));

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

        if (!IsOnEd25519(bytes))
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

    /// <summary>RFC 8032 point decode: 32-byte compressed ed25519 public key is on the curve.</summary>
    public static bool IsOnEd25519(ReadOnlySpan<byte> pk)
    {
        if (pk.Length != 32)
        {
            return false;
        }

        Span<byte> yBytes = stackalloc byte[32];
        pk.CopyTo(yBytes);
        yBytes[31] &= 0x7f;
        var y = new BigInteger(yBytes, isUnsigned: true, isBigEndian: false);
        if (y.CompareTo(P) >= 0)
        {
            return false;
        }

        var y2 = Mod(y * y);
        var u = Mod(y2 - BigInteger.One);
        var v = Mod(D * y2 + BigInteger.One);
        if (v.IsZero)
        {
            return false;
        }

        var x2 = Mod(u * ModPow(v, P - 2));
        if (x2.IsZero)
        {
            return true;
        }

        return ModPow(x2, (P - 1) / 2).Equals(BigInteger.One);
    }

    static BigInteger Mod(BigInteger value)
    {
        var r = value % P;
        return r.Sign < 0 ? r + P : r;
    }

    static BigInteger ModPow(BigInteger value, BigInteger exp) =>
        BigInteger.ModPow(Mod(value), exp, P);
}
