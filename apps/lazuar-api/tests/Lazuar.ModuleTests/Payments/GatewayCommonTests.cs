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
    public void ResolveEmail_UsesPlaceholderWhenBlank()
    {
        GatewayCommon.ResolveEmail(null).Should().Be(GatewayCommon.PlaceholderEmail);
        GatewayCommon.ResolveEmail("").Should().Be(GatewayCommon.PlaceholderEmail);
        GatewayCommon.ResolveEmail("  ").Should().Be(GatewayCommon.PlaceholderEmail);
        GatewayCommon.ResolveEmail("user@example.com").Should().Be("user@example.com");
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
    public void ToMinorUnitsTruncating_MatchesBillplzCast()
    {
        // (int)(10.009m * 1 * 100) truncates toward zero → 1000
        GatewayCommon.ToMinorUnitsTruncating(10.009m).Should().Be(1000);
        GatewayCommon.ToMinorUnitsTruncating(10.50m, 2).Should().Be(2100);
    }

    [Test]
    public void ToMinorUnitsRounded_MatchesChipRound()
    {
        // Math.Round(10.005m * 100, 0) with default MidpointRounding.ToEven
        GatewayCommon.ToMinorUnitsRounded(10.005m).Should().Be((int)Math.Round(10.005m * 100m, 0));
        GatewayCommon.ToMinorUnitsRounded(10.50m, 2).Should().Be(2100);
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
