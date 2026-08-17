using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Communications.Contracts;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class QuoteOfflineSstTests
{
    [Test]
    public async Task InitiateCustom_SstMerchant_ChargesGrossAndStampsMetadata()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Design", 1, 5000m) },
            DateTime.UtcNow.AddDays(7),
            isB2bRequired: false,
            "BILLPLZ");

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);
        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://pay.example/hop2");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "http://localhost:3004" })
            .Build();

        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, SstBilling(orgId));

        await handler.Handle(new InitiateCheckoutCommand(
            "acme", "custom", "Buyer", "buyer@example.com", null, null, null,
            null, null, null, null, null, 1, true, null, session.Id), CancellationToken.None);

        await mediator.Received().Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Amount == 5400m
                && q.Quantity == 1
                && q.Metadata["sst_tax_type"] == "02"
                && q.Metadata["sst_tax_amount"] == "400.00"
                && q.Metadata["sst_rate_percent"] == "8"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MarkPaid_ProductSst_BooksGross()
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId, "Plan", "plan", 100m, "FIXED", 0m, "MYR", "mo", "BILLPLZ",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        product.SetSst("02", 8m);
        var session = new CheckoutSession(
            orgId, Guid.CreateVersion7(), product.Id, null, DateTime.UtcNow.AddHours(1));

        CommerceTransactionLog? log = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>()))
            .Do(ci => log = ci.Arg<CommerceTransactionLog>());

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(session.ClientProfileId).Returns(new ClientProfileDto
        {
            Id = session.ClientProfileId.ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });

        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
            repository, Substitute.For<IEventBus>(), crm, SstBilling(orgId));
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Amount.Should().Be(108m);
    }

    [Test]
    public async Task MarkPaid_CustomSst_BooksGross()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Consulting", 1, 250m) },
            DateTime.UtcNow.AddDays(1),
            isB2bRequired: false);

        CommerceTransactionLog? log = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>()))
            .Do(ci => log = ci.Arg<CommerceTransactionLog>());

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(session.ClientProfileId).Returns(new ClientProfileDto
        {
            Id = session.ClientProfileId.ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });

        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
            repository, Substitute.For<IEventBus>(), crm, SstBilling(orgId));
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Amount.Should().Be(270m);
    }

    [Test]
    public void CustomQuoteBreakdown_MatchesGrossBreakdown()
    {
        SubscriptionBillingAmount.CustomQuoteBreakdown(5000m, true).Gross.Should().Be(5400m);
        SubscriptionBillingAmount.CustomQuoteBreakdown(5000m, false).Gross.Should().Be(5000m);
    }

    private static IBillingQueryService SstBilling(Guid organizationId)
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
        {
            Legal_name = "Studio",
            Tin = "C12345678901",
            Sst_registration_number = "W10-1234-12345678"
        });
        return billing;
    }
}
