using BuildingBlocks.Domain;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Infrastructure.Gateways;
using Modules.Lhdn.Infrastructure.Services;
using NSubstitute;
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
    public void ApiBaseUrl_Prod_UsesProductionApiHost()
    {
        var config = new ConfigurationBuilder().Build();
        LhdnEnvironmentUrls.ApiBaseUrl(config, "PROD").Should().Be("https://api.myinvois.hasil.gov.my");
        LhdnEnvironmentUrls.ApiBaseUrl(config, "SANDBOX").Should().Be("https://preprod-api.myinvois.hasil.gov.my");
    }

    [Test]
    public void GetBaseUrl_AfterRememberingProd_UsesProductionApi()
    {
        var config = new ConfigurationBuilder().Build();
        var adapter = new LhdnGatewayAdapter(
            Substitute.For<System.Net.Http.IHttpClientFactory>(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            config,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LhdnGatewayAdapter>.Instance);

        adapter.RememberEnvironment("client-prod", "PROD");
        adapter.GetBaseUrl("client-prod").Should().Be("https://api.myinvois.hasil.gov.my");
        adapter.GetBaseUrl("unknown").Should().Be("https://preprod-api.myinvois.hasil.gov.my");
    }

    [Test]
    public void NormalizeToAlpha3_MapsMyToMys()
    {
        Iso3166Country.NormalizeToAlpha3(null).Should().Be("MYS");
        Iso3166Country.NormalizeToAlpha3("MY").Should().Be("MYS");
        Iso3166Country.NormalizeToAlpha3("my").Should().Be("MYS");
        Iso3166Country.NormalizeToAlpha3("MYS").Should().Be("MYS");
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
