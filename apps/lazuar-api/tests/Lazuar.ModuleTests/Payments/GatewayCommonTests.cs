using System;
using System.Collections.Generic;
using FluentAssertions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class GatewayCommonTests
{
    [TestCase(null, "Customer")]
    [TestCase("", "Customer")]
    [TestCase("   ", "Customer")]
    [TestCase("no-at-sign", "Customer")]
    [TestCase("@orphan.com", "Customer")]
    [TestCase("alice@example.com", "alice")]
    [TestCase("bob.smith@lazuar.com", "bob.smith")]
    public void ExtractName_ReturnsExpected(string? email, string expected)
    {
        GatewayCommon.ExtractName(email).Should().Be(expected);
    }

    [Test]
    public void TryResolveEmail_RefusesBlankAndPlaceholder()
    {
        GatewayCommon.TryResolveEmail(null, out _, out _).Should().BeFalse();
        GatewayCommon.TryResolveEmail("", out _, out _).Should().BeFalse();
        GatewayCommon.TryResolveEmail("  ", out _, out _).Should().BeFalse();
        GatewayCommon.TryResolveEmail(GatewayCommon.PlaceholderEmail, out _, out _).Should().BeFalse();
        GatewayCommon.TryResolveEmail("user@example.com", out var email, out var error).Should().BeTrue();
        email.Should().Be("user@example.com");
        error.Should().BeNull();
    }

    [Test]
    public void ProductDescription_DefaultsAndQuantitySuffix()
    {
        GatewayCommon.ProductDescription(null, 1).Should().Be(GatewayCommon.DefaultProductName);
        GatewayCommon.ProductDescription("", 1).Should().Be(GatewayCommon.DefaultProductName);
        GatewayCommon.ProductDescription("Pro Plan", 1).Should().Be("Pro Plan");
        GatewayCommon.ProductDescription("Pro Plan", 3).Should().Be("Pro Plan (x3)");
    }

    [Test]
    public void ToMinorUnits_UsesAwayFromZeroAndZeroDecimal()
    {
        GatewayCommon.ToMinorUnits(10.005m).Should().Be(1001);
        GatewayCommon.ToMinorUnits(10.50m, "MYR", 2).Should().Be(2100);
        GatewayCommon.ToMinorUnits(1000m, "JPY").Should().Be(1000);
        GatewayCommon.ToMinorUnitsTruncating(10.009m).Should().Be(1001);
        GatewayCommon.ToMinorUnitsRounded(10.50m, 2).Should().Be(2100);
    }

    [Test]
    public void TryNormalizeCurrency_UppercasesAndRejectsBlank()
    {
        GatewayCommon.TryNormalizeCurrency("myr", out var myr).Should().BeTrue();
        myr.Should().Be("MYR");
        GatewayCommon.TryNormalizeCurrency(null, out _).Should().BeFalse();
        GatewayCommon.TryNormalizeCurrency("MYRX", out _).Should().BeFalse();
    }

    [Test]
    public void StampGatewayFeeStatus_DoesNotTreatUnknownZeroAsKnownFee()
    {
        var unknown = new Dictionary<string, string>();
        GatewayCommon.StampGatewayFeeStatus(unknown, feeKnown: false);
        unknown[GatewayCommon.GatewayFeeStatusKey].Should().Be(GatewayCommon.GatewayFeeStatusUnknown);

        var known = new Dictionary<string, string>();
        GatewayCommon.StampGatewayFeeStatus(known, feeKnown: true);
        known[GatewayCommon.GatewayFeeStatusKey].Should().Be(GatewayCommon.GatewayFeeStatusKnown);
    }

    [Test]
    public void ApplyPayingTenantMetadata_PreservesPayingTenant_AndStampsPlatformTenant()
    {
        var paying = Guid.CreateVersion7();
        var system = Guid.CreateVersion7();
        var metadata = new Dictionary<string, string> { ["tenant_id"] = paying.ToString() };

        GatewayCommon.ApplyPayingTenantMetadata(metadata, system);

        metadata["tenant_id"].Should().Be(paying.ToString());
        metadata["platform_tenant_id"].Should().Be(system.ToString());
    }
}
