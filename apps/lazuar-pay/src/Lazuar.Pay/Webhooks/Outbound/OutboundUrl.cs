using System.Net;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Webhooks.Outbound;

internal static class OutboundUrl
{
    public static bool TryValidate(string? raw, IHostEnvironment env, out string url, out string error)
    {
        url = "";
        error = "";
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            error = "url is required";
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            error = "url must be http or https";
            return false;
        }

        if (uri.Host.Equals("169.254.169.254", StringComparison.OrdinalIgnoreCase)
            || IsPrivateOrLoopback(uri))
        {
            var testing = env.IsEnvironment("Testing") || env.IsDevelopment();
            if (IsLoopback(uri) && testing)
            {
                url = uri.ToString();
                return true;
            }

            error = "url is not allowed";
            return false;
        }

        url = uri.ToString();
        return true;
    }

    static bool IsLoopback(Uri uri) =>
        uri.IsLoopback
        || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host is "127.0.0.1" or "::1";

    static bool IsPrivateOrLoopback(Uri uri)
    {
        if (IsLoopback(uri))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ip))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        return false;
    }
}
