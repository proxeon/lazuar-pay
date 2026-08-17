using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.One.Application.Queries;
using Modules.One.Infrastructure.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class GetPublicPricingQueryHandlerTests
{
    private static GetPublicPricingQueryHandler CreateHandler(
        IReadOnlyDictionary<string, string?>? extra = null,
        ICreditCostService? credits = null)
    {
        credits ??= Substitute.For<ICreditCostService>();
        credits.GetPackages().Returns(
        [
            new CreditPackage(50m, 500),
            new CreditPackage(100m, 1100),
            new CreditPackage(200m, 2500)
        ]);
        credits.GetStarterGrant().Returns(50);
        credits.GetCost(CreditAction.LhdnSubmit).Returns(3);
        credits.GetCost(CreditAction.WhatsAppSend).Returns(0);

        var values = new Dictionary<string, string?>
        {
            ["Saas:Plan:Code"] = "hub_starter",
            ["Saas:Plan:Name"] = "Hub Starter",
            ["Saas:Plan:AmountMyr"] = "0",
            ["Saas:Plan:Interval"] = "mo",
            ["Saas:Plan:Currency"] = "MYR",
            ["Saas:Seller:SstRate"] = "0",
            ["Saas:Seller:SstReason"] = "Supplier not SST-registered",
            ["Messaging:WhatsAppEnabled"] = "true"
        };
        if (extra != null)
        {
            foreach (var (key, value) in extra)
            {
                values[key] = value;
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new GetPublicPricingQueryHandler(credits, config);
    }

    [Test]
    public async Task Gmv_Take_Is_Always_Zero()
    {
        var handler = CreateHandler(new Dictionary<string, string?>
        {
            ["Saas:Plan:AmountMyr"] = "49",
            ["FakeGmvTakePercent"] = "5"
        });

        var dto = await handler.Handle(new GetPublicPricingQuery(), CancellationToken.None);

        Assert.That(dto.Gmv_take_percent, Is.EqualTo(0));
        Assert.That(dto.Gmv_take_percent, Is.Not.EqualTo(5));
        Assert.That(GetPublicPricingQueryHandler.GmvTakePercent, Is.EqualTo(0));
    }

    [Test]
    public async Task Packages_And_Starter_Match_Credit_Service()
    {
        var handler = CreateHandler();
        var dto = await handler.Handle(new GetPublicPricingQuery(), CancellationToken.None);

        Assert.That(dto.Starter_credits, Is.EqualTo(50));
        Assert.That(dto.Packages, Has.Count.EqualTo(3));
        Assert.That(dto.Packages[0].Amount_myr, Is.EqualTo(50));
        Assert.That(dto.Packages[0].Credits, Is.EqualTo(500));
        Assert.That(dto.Packages[1].Amount_myr, Is.EqualTo(100));
        Assert.That(dto.Packages[1].Credits, Is.EqualTo(1100));
        Assert.That(dto.Packages[2].Amount_myr, Is.EqualTo(200));
        Assert.That(dto.Packages[2].Credits, Is.EqualTo(2500));
        Assert.That(dto.Lhdn_submit_credits, Is.EqualTo(3));
        Assert.That(dto.Whatsapp_send_credits, Is.EqualTo(0));
    }

    [Test]
    public async Task Zero_Hub_Plan_Means_Unconfigured_Not_Free()
    {
        var handler = CreateHandler();
        var dto = await handler.Handle(new GetPublicPricingQuery(), CancellationToken.None);

        Assert.That(dto.Checkout_is_free, Is.False);
        Assert.That(dto.Hub_plan.Code, Is.EqualTo("hub_starter"));
        Assert.That(dto.Hub_plan.Amount_myr, Is.EqualTo(0));
        Assert.That(dto.Lhdn_credits_live, Is.False);
        Assert.That(dto.Whatsapp_credits_live, Is.False);
        Assert.That(dto.Sst_rate, Is.EqualTo(0));
        Assert.That(dto.Sst_note, Does.Contain("SST 0%"));
        Assert.That(dto.Sst_note, Does.Contain("Supplier not SST-registered"));
    }

    [Test]
    public async Task Positive_Hub_Plan_Is_A_Software_Fee_Not_Gmv()
    {
        var handler = CreateHandler(new Dictionary<string, string?>
        {
            ["Saas:Plan:AmountMyr"] = "49"
        });

        var dto = await handler.Handle(new GetPublicPricingQuery(), CancellationToken.None);

        Assert.That(dto.Checkout_is_free, Is.False);
        Assert.That(dto.Hub_plan.Amount_myr, Is.EqualTo(49));
        Assert.That(dto.Gmv_take_percent, Is.EqualTo(0));
    }
}
