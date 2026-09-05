using System.Net;
using Lazuar.Pay.Webhooks.Outbound;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Table-driven pins for the outbound webhook address blocklist (issues 001/017, 002/002,
/// and 008 in issues/003): every named non-global IPv4 range — including multicast,
/// reserved, documentation, and benchmarking blocks — and the IPv6 equivalents, with
/// NAT64 64:ff9b::/96 judged by its embedded IPv4. Global unicast samples must keep
/// passing so the list cannot silently over-block.
/// </summary>
public class OutboundUrlRangeTests
{
    static readonly (string Address, bool Blocked)[] Cases =
    [
        // Previously covered ranges must stay blocked.
        ("0.0.0.1", true),
        ("10.1.2.3", true),
        ("127.0.0.1", true),
        ("100.64.0.1", true),
        ("169.254.169.254", true),
        ("172.16.0.1", true),
        ("172.31.255.255", true),
        ("192.168.1.1", true),
        // Issue 008 additions.
        ("192.0.0.1", true),            // IETF protocol assignments
        ("192.0.2.1", true),            // TEST-NET-1
        ("198.18.0.1", true),           // benchmarking
        ("198.19.255.255", true),       // benchmarking
        ("198.51.100.7", true),         // TEST-NET-2
        ("203.0.113.9", true),          // TEST-NET-3
        ("224.0.0.1", true),            // multicast
        ("239.255.255.255", true),      // multicast
        ("240.0.0.1", true),            // reserved
        ("255.255.255.255", true),      // broadcast
        ("::1", true),
        ("fe80::1", true),
        ("fd00::1", true),
        ("ff02::1", true),
        ("::ffff:10.0.0.5", true),      // IPv4-mapped
        ("::ffff:169.254.169.254", true),
        ("64:ff9b::a9fe:a9fe", true),   // NAT64 of 169.254.169.254
        ("64:ff9b::c0a8:101", true),    // NAT64 of 192.168.1.1
        ("2001:db8::1", true),          // documentation
        // Global unicast must keep passing registration, dispatch, and connect pinning.
        ("8.8.8.8", false),
        ("1.1.1.1", false),
        ("172.32.0.1", false),          // just outside 172.16/12
        ("198.20.0.1", false),          // just outside 198.18/15
        ("203.0.114.1", false),         // just outside TEST-NET-3
        ("2606:4700::1111", false),
        ("64:ff9b::808:808", false),    // NAT64 of 8.8.8.8 — public IPv4 behind the prefix
    ];

    [Test]
    public void Named_ranges_are_blocked_and_global_unicast_passes()
    {
        var failures = new List<string>();
        foreach (var (address, blocked) in Cases)
        {
            var ip = IPAddress.Parse(address);
            var actual = OutboundUrl.IsPrivateOrLoopback(ip);
            if (actual != blocked)
            {
                failures.Add($"{address}: expected blocked={blocked}, got {actual}");
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
