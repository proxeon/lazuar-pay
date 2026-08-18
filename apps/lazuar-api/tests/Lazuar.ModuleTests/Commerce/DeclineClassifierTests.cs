using FluentAssertions;
using Modules.Commerce.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class DeclineClassifierTests
{
    [TestCase("incorrect_number")]
    [TestCase("lost_card")]
    [TestCase("pickup_card")]
    [TestCase("stolen_card")]
    [TestCase("revocation_of_authorization")]
    [TestCase("revocation_of_all_authorizations")]
    [TestCase("authentication_required")]
    [TestCase("highest_risk_level")]
    [TestCase("transaction_not_allowed")]
    [TestCase("expired_card")]
    [TestCase("invalid_expiry_month")]
    [TestCase("invalid_expiry_year")]
    [TestCase("STOLEN_CARD")]
    public void HardCodes_AreHard(string code)
    {
        DeclineClassifier.Classify(code).Should().Be(DeclineClassifier.Hard);
        DeclineClassifier.IsHard(code).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("insufficient_funds")]
    [TestCase("charge_declined")]
    [TestCase("card_declined")]
    [TestCase("generic_decline")]
    [TestCase("chip_error")]
    [TestCase("purchase.payment_failure")]
    public void EverythingElse_IsSoft(string? code)
    {
        DeclineClassifier.Classify(code).Should().Be(DeclineClassifier.Soft);
        DeclineClassifier.IsHard(code).Should().BeFalse();
    }
}
