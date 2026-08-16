using FluentAssertions;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Application.Services;
using Modules.Payments.Domain;
using NUnit.Framework;

using PaymentErrorCodes = Modules.Payments.Application.Exceptions.PaymentErrorCodes;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentGatewayEnvironmentTests
{
    [Test]
    public void Test_Key_Vs_Live_Config_Mismatches()
    {
        var act = () => CheckoutSessionCashier.EnsureKeyModeMatchesConfigEnvironment(true, "live");
        act.Should().Throw<PaymentIntegrationException>().Which.Code.Should().Be(PaymentErrorCodes.KeyModeMismatch);
    }

    [Test]
    public void Live_Key_Vs_Test_Config_Mismatches()
    {
        var act = () => CheckoutSessionCashier.EnsureKeyModeMatchesConfigEnvironment(false, "test");
        act.Should().Throw<PaymentIntegrationException>();
    }

    [Test]
    public void Hosted_Null_K1_Does_Not_Throw()
    {
        var act = () => CheckoutSessionCashier.EnsureKeyModeMatchesConfigEnvironment(null, "live");
        act.Should().NotThrow();
    }

    [Test]
    public void Infer_Stripe_Prefix()
    {
        PaymentGatewayEnvironment.InferFromStripeShapedKey("sk_test_abc").Should().Be("test");
        PaymentGatewayEnvironment.InferFromStripeShapedKey("sk_live_abc").Should().Be("live");
        PaymentGatewayEnvironment.InferFromStripeShapedKey("billplz-secret").Should().BeNull();
    }
}
