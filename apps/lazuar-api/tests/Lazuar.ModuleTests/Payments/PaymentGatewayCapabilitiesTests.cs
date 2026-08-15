using FluentAssertions;
using Modules.Payments.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentGatewayCapabilitiesTests
{
    [TestCase("STRIPE", true)]
    [TestCase("stripe", true)]
    [TestCase(" CHIP ", true)]
    [TestCase("BILLPLZ", false)]
    [TestCase("RAZORPAY", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("UNKNOWN", false)]
    public void SupportsOffSession_OnlyStripeAndChip(string? gateway, bool expected)
    {
        PaymentGatewayCapabilities.SupportsOffSession(gateway).Should().Be(expected);
        PaymentGatewayCapabilities.IsReminderOnlyGateway(gateway).Should().Be(!expected);
    }
}
