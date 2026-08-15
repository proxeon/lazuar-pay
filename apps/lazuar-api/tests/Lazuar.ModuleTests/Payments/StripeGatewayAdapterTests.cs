using System;
using System.Collections.Generic;
using FluentAssertions;
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
}
