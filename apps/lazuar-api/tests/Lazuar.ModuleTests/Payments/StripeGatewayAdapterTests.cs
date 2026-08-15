using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class StripeGatewayAdapterTests
{
    [Test]
    public void ApplySetupFutureUsage_WhenTrue_SetsOffSessionAndAlwaysCreatesCustomer()
    {
        var options = new SessionCreateOptions
        {
            PaymentIntentData = new SessionPaymentIntentDataOptions()
        };

        StripeGatewayAdapter.ApplySetupFutureUsage(options, setupFutureUsage: true);

        options.PaymentIntentData!.SetupFutureUsage.Should().Be("off_session");
        options.CustomerCreation.Should().Be("always");
    }

    [Test]
    public void ApplySetupFutureUsage_WhenFalse_DoesNotSetCustomerCreation()
    {
        var options = new SessionCreateOptions
        {
            PaymentIntentData = new SessionPaymentIntentDataOptions()
        };

        StripeGatewayAdapter.ApplySetupFutureUsage(options, setupFutureUsage: false);

        options.PaymentIntentData!.SetupFutureUsage.Should().BeNull();
        options.CustomerCreation.Should().BeNull();
    }

    [Test]
    public void CreateOffSessionRequestOptions_WhenKeyPresent_SetsIdempotencyKey()
    {
        var eventId = Guid.CreateVersion7().ToString();

        var options = StripeGatewayAdapter.CreateOffSessionRequestOptions(eventId);

        options.Should().NotBeNull();
        options!.IdempotencyKey.Should().Be(eventId);
    }

    [Test]
    public void ResolveOffSessionIdempotencyKey_PrefersChargeAttemptId()
    {
        var attemptId = Guid.CreateVersion7();
        var fallback = Guid.CreateVersion7().ToString();

        var key = StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(attemptId, fallback);

        key.Should().Be("lazuar-offsession:" + attemptId);
        StripeGatewayAdapter.FormatOffSessionIdempotencyKey(attemptId)
            .Should().Be(key);
    }

    [Test]
    public void ResolveOffSessionIdempotencyKey_FallsBackWhenAttemptMissing()
    {
        StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(null, "evt_1")
            .Should().Be("evt_1");
        StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(null, " ")
            .Should().BeNull();
    }

    [Test]
    public void BuildOffSessionMetadata_IncludesChargeAttemptId()
    {
        var tenantId = Guid.CreateVersion7();
        var campaignId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        var receipt = Guid.CreateVersion7().ToString();

        var meta = StripeGatewayAdapter.BuildOffSessionMetadata(receipt, tenantId, campaignId, attemptId);

        meta["type"].Should().Be("commerce_subscription");
        meta["subscription_id"].Should().Be(receipt);
        meta["tenant_id"].Should().Be(tenantId.ToString());
        meta["dunning_campaign_id"].Should().Be(campaignId.ToString());
        meta["charge_attempt_id"].Should().Be(attemptId.ToString());
    }

    [Test]
    public void CreateOffSessionRequestOptions_WhenMissing_ReturnsNull()
    {
        StripeGatewayAdapter.CreateOffSessionRequestOptions(null).Should().BeNull();
        StripeGatewayAdapter.CreateOffSessionRequestOptions(" ").Should().BeNull();
    }

    [Test]
    public void MapPaymentIntentPaymentFailed_UsesPiMetadataAndId()
    {
        var subscriptionId = Guid.CreateVersion7();
        var pi = new PaymentIntent
        {
            Id = "pi_failed_renew",
            Amount = 4990,
            Currency = "myr",
            CustomerId = "cus_1",
            PaymentMethodId = "pm_1",
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = subscriptionId.ToString(),
                ["receipt"] = subscriptionId.ToString()
            }
        };

        var parsed = StripeGatewayAdapter.MapPaymentIntentPaymentFailed(pi, "evt_pi_failed");

        parsed.Verified.Should().BeTrue();
        parsed.EventType.Should().Be("PAYMENT_FAILED");
        parsed.EventId.Should().Be("evt_pi_failed");
        parsed.GatewayTransactionId.Should().Be("pi_failed_renew");
        parsed.AmountPaid.Should().Be(49.90m);
        parsed.Metadata["subscription_id"].Should().Be(subscriptionId.ToString());
        parsed.Metadata["receipt"].Should().Be(subscriptionId.ToString());
        parsed.GatewayCustomerId.Should().Be("cus_1");
        parsed.GatewayTokenId.Should().Be("pm_1");
    }

    private const string WebhookSecret = "whsec_test_lp090";
    private const string StripeApiVersion = "2025-03-31.basil";

    [Test]
    public async Task ParseWebhook_MissingStripeSignature_IsNotVerified()
    {
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var result = await adapter.ParseWebhookAsync(
            "sk_test",
            WebhookSecret,
            SessionCompletedJson("evt_1", "cs_1", "pi_1"),
            new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("Stripe-Signature");
    }

    [Test]
    public async Task ParseWebhook_BadSecret_IsNotVerified()
    {
        var json = SessionCompletedJson("evt_bad", "cs_1", "pi_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", "whsec_wrong", json, headers);

        result.Verified.Should().BeFalse();
    }

    [Test]
    public async Task ParseWebhook_CheckoutSessionCompleted_UsesEventIdAndPaymentIntent()
    {
        var json = SessionCompletedJson("evt_cs_1", "cs_test_1", "pi_test_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_cs_1");
        result.GatewayTransactionId.Should().Be("pi_test_1");
    }

    [Test]
    public async Task ParseWebhook_PaymentIntentSucceeded_UsesPaymentIntentId()
    {
        var json = PaymentIntentSucceededJson("evt_pi_1", "pi_test_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_pi_1");
        result.GatewayTransactionId.Should().Be("pi_test_1");
    }

    [Test]
    public async Task ParseWebhook_UnmappedType_IsVerifiedWithStripeType()
    {
        var json = $$"""
            {
              "id": "evt_unmapped",
              "object": "event",
              "api_version": "{{StripeApiVersion}}",
              "request": null,
              "type": "customer.updated",
              "data": {
                "object": {
                  "id": "cus_1",
                  "object": "customer"
                }
              }
            }
            """;
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("customer.updated");
        result.EventId.Should().Be("evt_unmapped");
    }

    private static string SessionCompletedJson(string eventId, string sessionId, string paymentIntentId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "amount_total": 5000,
              "currency": "myr",
              "payment_intent": "{{paymentIntentId}}",
              "metadata": {}
            }
          }
        }
        """;

    private static string PaymentIntentSucceededJson(string eventId, string paymentIntentId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": 5000,
              "amount_received": 5000,
              "currency": "myr",
              "status": "succeeded",
              "metadata": {}
            }
          }
        }
        """;

    private static string SignStripe(string json, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = timestamp + "." + json;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={hex}";
    }
}
