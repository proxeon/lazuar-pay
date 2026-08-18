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
    public void BuildPaymentLinkRequest_NeverMintsCardRegistration()
    {
        var req = RazorpayGatewayAdapter.BuildPaymentLinkRequest(
            10m, "myr", "Plan", "buyer@example.com", "https://ok",
            new Dictionary<string, string>(), 1);

        req.Should().NotContainKey("subscription_registration");
        req.Should().NotContainKey("type");
        req["amount"].Should().Be(1000);
        req["currency"].Should().Be("MYR");
        var customer = (Dictionary<string, object>)req["customer"];
        customer.Should().NotContainKey("contact");
    }

    [Test]
    public async Task ParseWebhook_InvoiceExpired_IsIgnoredNotPaymentFailed()
    {
        var body = """
            {
              "event": "invoice.expired",
              "payload": {
                "invoice": { "entity": { "id": "inv_exp_1" } }
              }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(body)
        };

        var result = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, body, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("invoice.expired");
        result.EventType.Should().NotBe("PAYMENT_FAILED");
        result.EventId.Should().BeEmpty();
    }

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
        result.Currency.Should().Be("INR");
    }

    [Test]
    public async Task ParseWebhook_PaymentFailed_MapsPaymentFailed()
    {
        var body = """
            {
              "event": "payment.failed",
              "payload": {
                "payment": {
                  "entity": {
                    "id": "pay_fail1",
                    "amount": 5000,
                    "currency": "MYR",
                    "notes": { "subscription_id": "sub-1" }
                  }
                }
              }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(body),
            ["X-Razorpay-Event-Id"] = "evt_fail_1"
        };

        var result = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, body, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_FAILED");
        result.GatewayTransactionId.Should().Be("pay_fail1");
        result.Metadata.Should().ContainKey("subscription_id");
    }

    [Test]
    public async Task ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr()
    {
        var body = """
            {
              "event": "payment.captured",
              "payload": {
                "payment": {
                  "entity": {
                    "id": "pay_noccy",
                    "amount": 10000
                  }
                }
              }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(body),
            ["X-Razorpay-Event-Id"] = "evt_noccy"
        };

        var result = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, body, headers);

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("currency");
        result.Currency.Should().NotBe("MYR");
    }

    [Test]
    public async Task ParseWebhook_FailThenCapture_WithoutHeader_UseDistinctEventIds()
    {
        var failBody = """
            {
              "event": "payment.failed",
              "payload": {
                "payment": {
                  "entity": {
                    "id": "pay_same",
                    "amount": 5000,
                    "currency": "MYR"
                  }
                }
              }
            }
            """;
        var captureBody = """
            {
              "event": "payment.captured",
              "payload": {
                "payment": {
                  "entity": {
                    "id": "pay_same",
                    "amount": 5000,
                    "currency": "MYR"
                  }
                }
              }
            }
            """;
        var failHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(failBody)
        };
        var captureHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Razorpay-Signature"] = Sign(captureBody)
        };

        var failed = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, failBody, failHeaders);
        var captured = await CreateAdapter().ParseWebhookAsync("key:secret", WebhookSecret, captureBody, captureHeaders);

        failed.Verified.Should().BeTrue();
        captured.Verified.Should().BeTrue();
        failed.EventId.Should().Be("PAYMENT_FAILED:pay_same");
        captured.EventId.Should().Be("PAYMENT_COMPLETED:pay_same");
        failed.EventId.Should().NotBe(captured.EventId);
        failed.GatewayTransactionId.Should().Be("pay_same");
        captured.GatewayTransactionId.Should().Be("pay_same");
    }
}
