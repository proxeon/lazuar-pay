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
}
