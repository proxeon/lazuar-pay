namespace Lazuar.Pay.Rails.Solana;

public static class SolanaBase58
{
    const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var zeros = 0;
        while (zeros < data.Length && data[zeros] == 0)
        {
            zeros++;
        }

        var size = data.Length * 138 / 100 + 1;
        var buf = new byte[size];
        var length = 0;
        for (var i = zeros; i < data.Length; i++)
        {
            int carry = data[i];
            var j = 0;
            for (var k = buf.Length - 1; k >= 0; k--, j++)
            {
                if (carry == 0 && j >= length)
                {
                    break;
                }

                carry += 256 * buf[k];
                buf[k] = (byte)(carry % 58);
                carry /= 58;
            }

            length = j;
        }

        var skip = 0;
        while (skip < buf.Length && buf[skip] == 0)
        {
            skip++;
        }

        var chars = new char[zeros + (buf.Length - skip)];
        for (var i = 0; i < zeros; i++)
        {
            chars[i] = '1';
        }

        for (var i = 0; i < buf.Length - skip; i++)
        {
            chars[zeros + i] = Alphabet[buf[skip + i]];
        }

        return new string(chars);
    }

    public static bool TryDecode(string? input, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var s = input.Trim();
        var zeros = 0;
        while (zeros < s.Length && s[zeros] == '1')
        {
            zeros++;
        }

        var size = s.Length * 733 / 1000 + 1;
        var buf = new byte[size];
        var length = 0;
        for (var i = zeros; i < s.Length; i++)
        {
            var ch = s[i];
            var val = Alphabet.IndexOf(ch);
            if (val < 0)
            {
                return false;
            }

            var carry = val;
            var j = 0;
            for (var k = buf.Length - 1; k >= 0; k--, j++)
            {
                if (carry == 0 && j >= length)
                {
                    break;
                }

                carry += 58 * buf[k];
                buf[k] = (byte)(carry % 256);
                carry /= 256;
            }

            if (carry != 0)
            {
                return false;
            }

            length = j;
        }

        var skip = 0;
        while (skip < buf.Length && buf[skip] == 0)
        {
            skip++;
        }

        bytes = new byte[zeros + (buf.Length - skip)];
        for (var i = 0; i < zeros; i++)
        {
            bytes[i] = 0;
        }

        Buffer.BlockCopy(buf, skip, bytes, zeros, buf.Length - skip);
        return true;
    }
}
