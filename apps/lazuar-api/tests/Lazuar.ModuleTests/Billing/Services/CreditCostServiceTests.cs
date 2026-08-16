using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;
using Modules.Billing.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Services;

[TestFixture]
public class CreditCostServiceTests
{
    [Test]
    public void GetCost_ConfiguredLhdnAndZeroWhatsApp_ReturnsExactValues()
    {
        var sut = CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int>
            {
                ["LhdnSubmit"] = 3,
                ["WhatsAppSend"] = 0
            },
            StarterGrant = 50,
            Packages =
            [
                new CreditPackageOption { AmountMyr = 50, Credits = 500 },
                new CreditPackageOption { AmountMyr = 100, Credits = 1100 }
            ]
        });

        sut.GetCost(CreditAction.LhdnSubmit).Should().Be(3);
        sut.GetCost(CreditAction.WhatsAppSend).Should().Be(0);
        sut.GetStarterGrant().Should().Be(50);
        sut.GetPackages().Should().Equal(
            new CreditPackage(50m, 500),
            new CreditPackage(100m, 1100));
    }

    [Test]
    public void GetCost_EmptyCosts_AllActionsAreZero()
    {
        var sut = CreateSut(new CreditCostOptions());

        foreach (var action in Enum.GetValues<CreditAction>())
            sut.GetCost(action).Should().Be(0);
    }

    [Test]
    public void GetCost_OmittedEmailAndBroadcast_AreZero()
    {
        var sut = CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int>
            {
                ["LhdnSubmit"] = 3,
                ["WhatsAppSend"] = 0
            }
        });

        sut.GetCost(CreditAction.EmailSend).Should().Be(0);
        sut.GetCost(CreditAction.BroadcastEmailPerRecipient).Should().Be(0);
    }

    [Test]
    public void Constructor_UnknownJsonKey_IsIgnored()
    {
        var act = () => CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int>
            {
                ["NotARealAction"] = 99,
                ["LhdnSubmit"] = 3
            }
        });

        act.Should().NotThrow();

        var sut = CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int>
            {
                ["NotARealAction"] = 99,
                ["LhdnSubmit"] = 3
            }
        });

        sut.GetCost(CreditAction.LhdnSubmit).Should().Be(3);
        sut.GetCost(CreditAction.WhatsAppSend).Should().Be(0);
        sut.GetCost(CreditAction.EmailSend).Should().Be(0);
    }

    [Test]
    public void GetCost_UnknownAction_ReturnsZero()
    {
        var sut = CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int> { ["LhdnSubmit"] = 3 }
        });

        sut.GetCost((CreditAction)999).Should().Be(0);
        sut.GetCost((CreditAction)(-1)).Should().Be(0);
    }

    [Test]
    public void GetCost_WhatsAppSendOmitted_DefaultsToZero()
    {
        var sut = CreateSut(new CreditCostOptions
        {
            Costs = new Dictionary<string, int> { ["LhdnSubmit"] = 3 }
        });

        sut.GetCost(CreditAction.WhatsAppSend).Should().Be(0);
        sut.GetCost(CreditAction.LhdnSubmit).Should().Be(3);
    }

    [Test]
    public void GetCost_AppsettingsJson_WhatsAppSendDefaultsToZero()
    {
        var path = FindApiAppsettings();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var costs = doc.RootElement.GetProperty("Credits").GetProperty("Costs");

        costs.GetProperty("WhatsAppSend").GetInt32().Should().Be(0);
        costs.GetProperty("LhdnSubmit").GetInt32().Should().Be(3);
        costs.TryGetProperty("EmailSend", out _).Should().BeFalse();
        costs.TryGetProperty("BroadcastEmailPerRecipient", out _).Should().BeFalse();

        var bound = new CreditCostOptions();
        foreach (var prop in costs.EnumerateObject())
            bound.Costs[prop.Name] = prop.Value.GetInt32();

        var sut = CreateSut(bound);
        sut.GetCost(CreditAction.WhatsAppSend).Should().Be(0);
        sut.GetCost(CreditAction.LhdnSubmit).Should().Be(3);
        sut.GetCost((CreditAction)999).Should().Be(0);
    }

    private static CreditCostService CreateSut(CreditCostOptions options) =>
        new(Options.Create(options));

    private static string FindApiAppsettings()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lazuar.Api", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate apps/lazuar-api/src/Lazuar.Api/appsettings.json");
    }
}
