using System;
using System.Net;
using FluentAssertions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PublicDnsFallbackTests
{
    [Test]
    public void EncodeQuery_WritesRecursionAndQuestion()
    {
        var q = PublicDnsFallback.EncodeQuery("www.billplz-sandbox.com", 0x1234);
        q.Should().HaveCountGreaterThan(12);
        q[0].Should().Be(0x12);
        q[1].Should().Be(0x34);
        q[2].Should().Be(0x01);
        q[5].Should().Be(0x01);
        q[^3].Should().Be(1); // type A
        q[^1].Should().Be(1); // class IN
    }

    [Test]
    public void DecodeARecords_ReadsAnswer()
    {
        var id = (ushort)0x1234;
        var query = PublicDnsFallback.EncodeQuery("a.com", id);
        // Response = header/question from query + one A answer (pointer to offset 12).
        var response = new byte[query.Length + 16];
        Buffer.BlockCopy(query, 0, response, 0, query.Length);
        response[2] = 0x81;
        response[3] = 0x80;
        response[6] = 0;
        response[7] = 1; // ANCOUNT
        var o = query.Length;
        response[o] = 0xC0;
        response[o + 1] = 12;
        response[o + 2] = 0;
        response[o + 3] = 1; // A
        response[o + 4] = 0;
        response[o + 5] = 1; // IN
        response[o + 10] = 0;
        response[o + 11] = 4;
        response[o + 12] = 1;
        response[o + 13] = 2;
        response[o + 14] = 3;
        response[o + 15] = 4;

        PublicDnsFallback.DecodeARecords(response, id)
            .Should().Equal(IPAddress.Parse("1.2.3.4"));
    }
}
