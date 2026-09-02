using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Webhooks.Outbound;

internal sealed record OutboundUrlResult(bool Ok, string Url, string Error);

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
            if (IsLoopback(uri) && AllowsLoopback(env))
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

    /// <summary>
    /// Registration check including DNS. A literal-only check misses hostnames that resolve
    /// into RFC1918/ULA space. A host that does not resolve today is accepted; the dispatcher
    /// re-resolves on every attempt, so the answer at send time is the one that counts.
    /// </summary>
    public static async Task<OutboundUrlResult> ValidateResolvableAsync(string? raw, IHostEnvironment env, CancellationToken ct)
    {
        if (!TryValidate(raw, env, out var url, out var error))
        {
            return new OutboundUrlResult(false, "", error);
        }

        var uri = new Uri(url, UriKind.Absolute);
        if (IPAddress.TryParse(uri.Host, out _))
        {
            return new OutboundUrlResult(true, url, ""); // literal — fully judged by TryValidate
        }

        foreach (var ip in await ResolveAsync(uri.Host, ct))
        {
            if (IsDisallowed(ip, env))
            {
                return new OutboundUrlResult(false, "", "url is not allowed");
            }
        }

        return new OutboundUrlResult(true, url, "");
    }

    public static bool IsDisallowed(IPAddress ip, IHostEnvironment env) =>
        IsPrivateOrLoopback(ip) && !AllowsLoopback(env);

    /// <summary>
    /// Issue 017 (issues/001): dial the webhook endpoint on an address that passes the
    /// private-range check AT CONNECT TIME. The dispatcher's pre-send resolve is advisory
    /// only — HttpClient re-resolved the hostname independently, so a DNS rebinding could
    /// answer the validation query with a public IP and the dial query with
    /// 169.254.169.254/10.x. TLS is untouched (SNI and verification still use the original
    /// hostname); only the dialed IP is pinned to the validated answer. Loopback stays
    /// dialable in Development/Testing only, mirroring <see cref="AllowsLoopback"/>.
    /// </summary>
    public static async Task<NetworkStream> ConnectValidatedAsync(
        DnsEndPoint endpoint, bool allowLoopback, CancellationToken ct)
    {
        var addresses = IPAddress.TryParse(endpoint.Host, out var literal)
            ? [literal]
            : await ResolveAsync(endpoint.Host, ct);
        if (addresses.Count == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        Exception? last = null;
        foreach (var ip in addresses)
        {
            if (IsPrivateOrLoopback(ip) && !(allowLoopback && IPAddress.IsLoopback(ip)))
            {
                last = new InvalidOperationException($"refusing to dial private address {ip}");
                continue;
            }

            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(new IPEndPoint(ip, endpoint.Port), timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("no dialable address passed validation");
    }

    public static async Task<List<IPAddress>> ResolveAsync(string host, CancellationToken ct)
    {
        try
        {
            return [.. await Dns.GetHostAddressesAsync(host, ct)];
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return [];
        }
    }

    static bool AllowsLoopback(IHostEnvironment env) =>
        env.IsEnvironment("Testing") || env.IsDevelopment();

    static bool IsLoopback(Uri uri) =>
        uri.IsLoopback
        || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host is "127.0.0.1" or "::1";

    static bool IsPrivateOrLoopback(Uri uri) =>
        IsLoopback(uri) || (IPAddress.TryParse(uri.Host, out var ip) && IsPrivateOrLoopback(ip));

    public static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            // ::ffff:a.b.c.d — judge the embedded IPv4, not the wrapper.
            if (b.Length == 16 && b[..10].All(x => x == 0) && b[10] == 0xFF && b[11] == 0xFF)
            {
                return IsPrivateOrLoopback(new IPAddress(b[12..]));
            }

            return b[0] == 0xFF                             // multicast ff00::/8
                || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)  // link-local fe80::/10
                || (b[0] & 0xFE) == 0xFC                    // unique-local fc00::/7
                || b.All(x => x == 0);                      // unspecified ::
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 0
            || bytes[0] == 10
            || bytes[0] == 127
            || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) // CGNAT
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
