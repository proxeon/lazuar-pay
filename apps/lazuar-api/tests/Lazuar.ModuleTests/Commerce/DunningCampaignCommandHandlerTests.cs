using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class DunningCampaignCommandHandlerTests
{
    [Test]
    public async Task Create_AutoCharge_OnlyBillplzProducts_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var billplz = Product(orgId, "BILLPLZ");
        var repo = Substitute.For<ICommerceRepository>();
        repo.GetProductsByIdsAsync(orgId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Product> { billplz });

        var handler = new CreateDunningCampaignCommandHandler(repo);
        var act = () => handler.Handle(CreateCommand(orgId, new List<Guid> { billplz.Id }, null), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleValidationException>())
            .WithMessage("*AUTO_CHARGE is not available*");
        repo.DidNotReceive().AddDunningCampaign(Arg.Any<DunningCampaign>());
    }

    [Test]
    public async Task Create_AutoCharge_ManualOnlyTargets_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var repo = Substitute.For<ICommerceRepository>();
        var handler = new CreateDunningCampaignCommandHandler(repo);

        var act = () => handler.Handle(
            CreateCommand(orgId, null, new List<string> { "MANUAL" }),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleValidationException>();
        await repo.DidNotReceive().GetProductsByIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_AutoCharge_StripeProduct_Succeeds()
    {
        var orgId = Guid.CreateVersion7();
        var stripe = Product(orgId, "STRIPE");
        var repo = Substitute.For<ICommerceRepository>();
        repo.GetProductsByIdsAsync(orgId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Product> { stripe });

        DunningCampaign? saved = null;
        repo.When(r => r.AddDunningCampaign(Arg.Any<DunningCampaign>()))
            .Do(ci => saved = ci.Arg<DunningCampaign>());

        var handler = new CreateDunningCampaignCommandHandler(repo);
        var id = await handler.Handle(CreateCommand(orgId, new List<Guid> { stripe.Id }, null), CancellationToken.None);

        id.Should().NotBeEmpty();
        saved.Should().NotBeNull();
        saved!.Steps.Should().Contain(s => s.ActionType == "AUTO_CHARGE");
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_AutoCharge_NoProductFilter_Succeeds()
    {
        var orgId = Guid.CreateVersion7();
        var repo = Substitute.For<ICommerceRepository>();
        repo.ListProductsAsync(orgId, Arg.Any<CancellationToken>()).Returns(new List<Product>());
        var handler = new CreateDunningCampaignCommandHandler(repo);

        await handler.Handle(CreateCommand(orgId, null, null), CancellationToken.None);

        repo.Received(1).AddDunningCampaign(Arg.Any<DunningCampaign>());
        await repo.Received(1).ListProductsAsync(orgId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_AutoCharge_NoProductFilter_AllBillplz_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var billplz = Product(orgId, "BILLPLZ");
        var repo = Substitute.For<ICommerceRepository>();
        repo.ListProductsAsync(orgId, Arg.Any<CancellationToken>()).Returns(new List<Product> { billplz });
        var handler = new CreateDunningCampaignCommandHandler(repo);

        var act = () => handler.Handle(CreateCommand(orgId, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleValidationException>())
            .WithMessage("*AUTO_CHARGE is not available*");
        repo.DidNotReceive().AddDunningCampaign(Arg.Any<DunningCampaign>());
    }

    [Test]
    public async Task GenerateDefaults_EmptyOrg_IncludesAutoChargeAt1And5_SecondCallIsNoOp()
    {
        var orgId = Guid.CreateVersion7();
        var repo = Substitute.For<ICommerceRepository>();
        repo.HasAnyDunningCampaignAsync(orgId, Arg.Any<CancellationToken>()).Returns(false, true);
        repo.ListProductsAsync(orgId, Arg.Any<CancellationToken>()).Returns(new List<Product>());

        DunningCampaign? saved = null;
        repo.When(r => r.AddDunningCampaign(Arg.Any<DunningCampaign>()))
            .Do(ci => saved = ci.Arg<DunningCampaign>());

        var handler = new GenerateDefaultDunningCampaignsCommandHandler(repo);
        await handler.Handle(new GenerateDefaultDunningCampaignsCommand(orgId), CancellationToken.None);
        await handler.Handle(new GenerateDefaultDunningCampaignsCommand(orgId), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Steps.Should().Contain(s =>
            s.DayOffset == -3
            && s.ActionType == "EMAIL"
            && !string.IsNullOrWhiteSpace(s.EmailBody)
            && !s.EmailBody!.Contains("{{update_payment_link}}")
            && s.EmailBody.Contains("{{current_period_end}}")
            && s.EmailBody.Contains("renews on"));
        saved.Steps.Should().Contain(s =>
            s.DayOffset == 0
            && s.ActionType == "EMAIL"
            && s.EmailBody != null
            && s.EmailBody.Contains("{{renewal_link}}")
            && !s.EmailBody.Contains("update your payment method", StringComparison.OrdinalIgnoreCase)
            && s.Subject != null
            && s.Subject.Contains("pay this cycle"));
        saved.Steps.Should().Contain(s =>
            s.DayOffset == 3
            && s.ActionType == "EMAIL"
            && !string.IsNullOrWhiteSpace(s.EmailBody)
            && s.EmailBody!.Contains("{{renewal_link}}"));
        saved.Steps.Should().Contain(s => s.DayOffset == 1 && s.ActionType == "AUTO_CHARGE");
        saved.Steps.Should().Contain(s => s.DayOffset == 5 && s.ActionType == "AUTO_CHARGE");
        repo.Received(1).AddDunningCampaign(Arg.Any<DunningCampaign>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateDefaults_BillplzOnlyOrg_OmitsAutoCharge()
    {
        var orgId = Guid.CreateVersion7();
        var repo = Substitute.For<ICommerceRepository>();
        repo.HasAnyDunningCampaignAsync(orgId, Arg.Any<CancellationToken>()).Returns(false);
        repo.ListProductsAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<Product> { Product(orgId, "BILLPLZ") });

        DunningCampaign? saved = null;
        repo.When(r => r.AddDunningCampaign(Arg.Any<DunningCampaign>()))
            .Do(ci => saved = ci.Arg<DunningCampaign>());

        var handler = new GenerateDefaultDunningCampaignsCommandHandler(repo);
        await handler.Handle(new GenerateDefaultDunningCampaignsCommand(orgId), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Steps.Should().NotContain(s => s.ActionType == "AUTO_CHARGE");
        saved.Steps.Should().Contain(s => s.ActionType == "EMAIL");
    }

    [Test]
    public async Task Update_AutoCharge_OnlyBillplz_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var billplz = Product(orgId, "BILLPLZ");
        var existing = new DunningCampaign(orgId, "Old", "CANCEL", 7);
        var repo = Substitute.For<ICommerceRepository>();
        repo.GetDunningCampaignByIdAsync(orgId, existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        repo.GetProductsByIdsAsync(orgId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Product> { billplz });

        var handler = new UpdateDunningCampaignCommandHandler(repo);
        var act = () => handler.Handle(new UpdateDunningCampaignCommand(
            orgId,
            existing.Id,
            "Updated",
            "CANCEL",
            7,
            0,
            new List<Guid> { billplz.Id },
            null,
            new List<DunningStepData> { new(0, "AUTO_CHARGE", null, null, null) },
            true), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleValidationException>();
    }

    private static CreateDunningCampaignCommand CreateCommand(
        Guid orgId,
        List<Guid>? productIds,
        List<string>? methods) =>
        new(
            orgId,
            "Recovery",
            "CANCEL",
            7,
            0,
            productIds,
            methods,
            new List<DunningStepData> { new(0, "AUTO_CHARGE", null, null, null) });

    private static Product Product(Guid orgId, string gateway) =>
        new(
            orgId,
            "Plan",
            "plan-" + gateway.ToLowerInvariant(),
            10m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            gateway,
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
}
