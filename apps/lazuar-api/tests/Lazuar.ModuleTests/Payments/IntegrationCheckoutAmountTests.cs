using System.Text.Json;
using FluentAssertions;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Application.Services;
using Modules.Payments.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class IntegrationCheckoutAmountTests
{
    [Test]
    public void TryRead_TwoDecimalMyr_IsExact()
    {
        IntegrationCheckoutAmount.TryRead(Root("""{"amount":10.00,"currency":"MYR"}"""), "MYR", out var amount, out var error)
            .Should().BeTrue();
        amount.Should().Be(10.00m);
        error.Should().BeNull();
    }

    [Test]
    public void TryRead_RepeatingTenth_IsExactDecimal()
    {
        IntegrationCheckoutAmount.TryRead(Root("""{"amount":0.1,"currency":"MYR"}"""), "MYR", out var amount, out _)
            .Should().BeTrue();
        amount.Should().Be(0.1m);
    }

    [Test]
    public void TryRead_ThreeDecimalMyr_IsInvalid()
    {
        IntegrationCheckoutAmount.TryRead(Root("""{"amount":10.015,"currency":"MYR"}"""), "MYR", out _, out var error)
            .Should().BeFalse();
        error.Should().Contain("2 decimal");
    }

    [Test]
    public void CheckoutAmountRules_MyrThreeDecimals_AmountInvalid()
    {
        var act = () => CheckoutAmountRules.ValidateAmountAndCurrency(10.015m, "MYR");
        act.Should().Throw<PaymentIntegrationException>()
            .Which.Code.Should().Be(PaymentErrorCodes.AmountInvalid);
    }

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;
}
