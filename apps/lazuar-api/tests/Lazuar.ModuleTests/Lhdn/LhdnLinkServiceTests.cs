using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnLinkServiceTests
{
    [Test]
    public void GetPortalUrl_Sandbox_UsesPreprodHost()
    {
        var service = new LhdnLinkService(new ConfigurationBuilder().Build());

        service.GetPortalUrl("SANDBOX").Should().Be("https://preprod.myinvois.hasil.gov.my");
        service.GetPortalUrl(null).Should().Be("https://preprod.myinvois.hasil.gov.my");
    }

    [Test]
    public void GetPortalUrl_Prod_UsesProductionHost()
    {
        var service = new LhdnLinkService(new ConfigurationBuilder().Build());

        service.GetPortalUrl("PROD").Should().Be("https://myinvois.hasil.gov.my");
        service.GetPortalUrl("production").Should().Be("https://myinvois.hasil.gov.my");
    }

    [Test]
    public void Encode_ShareUrl_ReturnsPngBytes()
    {
        var png = MyInvoisQrPng.Encode("https://myinvois.hasil.gov.my/uuid/share/long");

        png.Length.Should().BeGreaterThan(20);
        png[0].Should().Be(0x89);
        png[1].Should().Be((byte)'P');
        png[2].Should().Be((byte)'N');
        png[3].Should().Be((byte)'G');
    }
}
