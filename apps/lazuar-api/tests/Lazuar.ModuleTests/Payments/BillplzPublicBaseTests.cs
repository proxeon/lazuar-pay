using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class BillplzPublicBaseTests
{
    [Test]
    public void Localhost_Rejected_AndNeverLooksPublic()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        BillplzPublicBase.TryResolveCallbackBase(cfg, "http://localhost:8090/api/v1", out _, out var err)
            .Should().BeFalse();
        err.Should().StartWith(BillplzPublicBase.CallbackBaseNotPublic);
    }

    [Test]
    public void LazuarLocalDev_Rejected()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        BillplzPublicBase.TryResolveCallbackBase(
                cfg, "http://lazuar-local-dev.com:8090/api/v1", out _, out var err)
            .Should().BeFalse();
        err.Should().StartWith(BillplzPublicBase.CallbackBaseNotPublic);
    }

    [Test]
    public void HttpsTunnel_Accepted()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        BillplzPublicBase.TryResolveCallbackBase(
                cfg, "https://pay-local.example.com/api/v1", out var bas, out var err)
            .Should().BeTrue();
        err.Should().BeNull();
        bas.Should().Be("https://pay-local.example.com/api/v1");
    }

    [Test]
    public void HubHostname_DoesNotForce_LiveBillplz()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        BillplzPublicBase.IsProductionApi(cfg, "https://pay-local.lazuar.com/api/v1")
            .Should().BeFalse();
        BillplzPublicBase.IsProductionApi(cfg, "https://hub.lazuar.com/api/v1")
            .Should().BeFalse();
        BillplzPublicBase.IsProductionApi(cfg, "https://hub.lazuar.com/api/v1", "live")
            .Should().BeTrue();
        BillplzPublicBase.IsProductionApi(cfg, "https://hub.lazuar.com/api/v1", "test")
            .Should().BeFalse();
    }

    [Test]
    public void InsecureFlag_AllowsLocalhost()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:AllowInsecureBillplzCallback"] = "true",
        }).Build();
        BillplzPublicBase.TryResolveCallbackBase(cfg, "http://localhost:8090/api/v1", out var bas, out _)
            .Should().BeTrue();
        bas.Should().Contain("localhost");
    }
}
