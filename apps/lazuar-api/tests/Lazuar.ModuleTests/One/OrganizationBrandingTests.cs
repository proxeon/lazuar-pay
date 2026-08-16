using System;
using FluentAssertions;
using Modules.One.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OrganizationBrandingTests
{
    [Test]
    public void UpdateBranding_Null_Clears()
    {
        var org = new Organization("Acme", "acme");
        org.UpdateBranding("https://cdn.example/vault/x.png", "#0a7c42");
        org.UpdateBranding(null, null);
        org.LogoUrl.Should().BeNull();
        org.PrimaryColor.Should().BeNull();
    }

    [Test]
    public void UpdateBranding_Canonicalizes_Hex()
    {
        var org = new Organization("Acme", "acme");
        org.UpdateBranding(null, "#0a7c42");
        org.PrimaryColor.Should().Be("#0A7C42");
    }

    [TestCase("red")]
    [TestCase("rgb(0,0,0)")]
    [TestCase("#fff")]
    [TestCase("#0a7c42ff")]
    public void UpdateBranding_Rejects_Bad_Color(string color)
    {
        var org = new Organization("Acme", "acme");
        var act = () => org.UpdateBranding(null, color);
        act.Should().Throw<InvalidOperationException>().WithMessage("*#RRGGBB*");
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("data:image/png;base64,xx")]
    public void UpdateBranding_Rejects_Bad_Logo(string url)
    {
        var org = new Organization("Acme", "acme");
        var act = () => org.UpdateBranding(url, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void UpdateBranding_Accepts_Https_Logo()
    {
        var org = new Organization("Acme", "acme");
        org.UpdateBranding("https://cdn.example/vault/x.png", null);
        org.LogoUrl.Should().Be("https://cdn.example/vault/x.png");
    }
}
