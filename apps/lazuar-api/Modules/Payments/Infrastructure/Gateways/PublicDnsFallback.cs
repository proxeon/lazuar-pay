using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// HttpClient connect hook that resolves hosts via 1.1.1.1 / 8.8.8.8 when the
/// machine resolver cannot (common for www.billplz-sandbox.com on some LANs).
/// </summary>
internal static class PublicDnsFallback
{
    public const string HttpClientName = "Billplz";

    private static readonly IPAddress[] Resolvers =
    [
        IPAddress.Parse("1.1.1.1"),
        IPAddress.Parse("8.8.8.8"),
    ];

    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext ctx,
        CancellationToken ct)
    {
        var host = ctx.DnsEndPoint.Host;
        var port = ctx.DnsEndPoint.Port;
        var addresses = await ResolveAsync(host, ct);

        Exception? last = null;
        foreach (var addr in addresses)
        {
            var socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(addr, port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                last = ex;
                socket.Dispose();
            }
        }

        throw last ?? new SocketException((int)SocketError.HostNotFound);
    }

    internal static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
    {
        foreach (var resolver in Resolvers)
        {
            var found = await QueryAAsync(host, resolver, ct);
            if (found.Length > 0)
                return found;
        }

        return await Dns.GetHostAddressesAsync(host, ct);
    }

    internal static async Task<IPAddress[]> QueryAAsync(
        string host,
        IPAddress resolver,
        CancellationToken ct)
    {
        var id = (ushort)Random.Shared.Next(0, ushort.MaxValue);
        var query = EncodeQuery(host, id);
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 2000;
        udp.Client.SendTimeout = 2000;

        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        sendCts.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await udp.SendAsync(query, new IPEndPoint(resolver, 53), sendCts.Token);
            var reply = await udp.ReceiveAsync(sendCts.Token);
            return DecodeARecords(reply.Buffer, id);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            return [];
        }
    }

    internal static byte[] EncodeQuery(string host, ushort id)
    {
        var labels = host.Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        var qnameLen = 1;
        foreach (var label in labels)
            qnameLen += 1 + Encoding.ASCII.GetByteCount(label);

        var buf = new byte[12 + qnameLen + 4];
        buf[0] = (byte)(id >> 8);
        buf[1] = (byte)id;
        buf[2] = 0x01; // recursion desired
        buf[5] = 0x01; // QDCOUNT = 1

        var offset = 12;
        foreach (var label in labels)
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            buf[offset++] = (byte)bytes.Length;
            Buffer.BlockCopy(bytes, 0, buf, offset, bytes.Length);
            offset += bytes.Length;
        }

        buf[offset++] = 0;
        buf[offset++] = 0;
        buf[offset++] = 1; // A
        buf[offset++] = 0;
        buf[offset] = 1; // IN
        return buf;
    }

    internal static IPAddress[] DecodeARecords(byte[] message, ushort expectedId)
    {
        if (message.Length < 12)
            return [];
        var id = (ushort)((message[0] << 8) | message[1]);
        if (id != expectedId)
            return [];
        if ((message[3] & 0x0F) != 0)
            return [];

        var qd = (message[4] << 8) | message[5];
        var an = (message[6] << 8) | message[7];
        var offset = 12;
        for (var i = 0; i < qd; i++)
        {
            if (!SkipName(message, ref offset))
                return [];
            offset += 4;
            if (offset > message.Length)
                return [];
        }

        var found = new List<IPAddress>();
        for (var i = 0; i < an; i++)
        {
            if (!SkipName(message, ref offset) || offset + 10 > message.Length)
                return found.ToArray();
            var type = (message[offset] << 8) | message[offset + 1];
            var rdlength = (message[offset + 8] << 8) | message[offset + 9];
            offset += 10;
            if (offset + rdlength > message.Length)
                return found.ToArray();
            if (type == 1 && rdlength == 4)
                found.Add(new IPAddress(message.AsSpan(offset, 4).ToArray()));
            offset += rdlength;
        }

        return found.ToArray();
    }

    private static bool SkipName(byte[] message, ref int offset)
    {
        var hops = 0;
        var jumped = false;
        var end = offset;
        while (offset < message.Length && hops++ < 32)
        {
            var len = message[offset];
            if (len == 0)
            {
                if (!jumped)
                    end = offset + 1;
                offset = end;
                return true;
            }

            if ((len & 0xC0) == 0xC0)
            {
                if (offset + 1 >= message.Length)
                    return false;
                if (!jumped)
                    end = offset + 2;
                offset = ((len & 0x3F) << 8) | message[offset + 1];
                jumped = true;
                continue;
            }

            offset += 1 + len;
        }

        return false;
    }
}
