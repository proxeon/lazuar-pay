using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class RazorpayGatewayAdapterTests
{
    private const string WebhookSecret = "rzp_test_webhook_secret";

    private static RazorpayGatewayAdapter CreateAdapter() =>
        new(NullLogger<RazorpayGatewayAdapter>.Instance);

    private static string Sign(string body) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret),
            Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    [Test]
    public async Task ParseWebhook_MissingSignature_IsNotVerified()
    {
        var result = await CreateAdapter().ParseWebhookAsync(
            "key:secret",
            WebhookSecret,
            """{"event":"payment.captured"}""",
            new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("X-Razorpay-Signature");
    }

    [Test]
    public async Task ParseWebhook_CapturedWithoutHeaderAndPaymentId_IsNotVerified()
    {
        var body = """
            {
              "event": "payment.captured",
              "payload": {
                "payment": {
                  "entity": {
                    "amount": 10000,
                    "currency": "INR"
                  }
                }
              }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(body)
        };

        var result = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, body, headers);

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("EventId");
        Guid.TryParse(result.EventId, out _).Should().BeFalse();
        result.EventId.Should().BeEmpty();
    }

    [Test]
    public async Task ParseWebhook_HeaderEventIdAndPaymentId_MapsIdentities()
    {
        var body = """
            {
              "event": "payment.captured",
              "payload": {
                "payment": {
                  "entity": {
                    "id": "pay_abc123",
                    "amount": 10000,
                    "currency": "INR",
                    "notes": {}
                  }
                }
              }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(body),
            ["X-Razorpay-Event-Id"] = "evt_rzp_1"
        };

        var result = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, body, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_rzp_1");
        result.GatewayTransactionId.Should().Be("pay_abc123");
    }
}
