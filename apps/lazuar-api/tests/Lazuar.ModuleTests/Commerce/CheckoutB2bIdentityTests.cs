using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Communications.Contracts;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CheckoutB2bIdentityTests
{
    [Test]
    public async Task InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage()
    {
        var handler = CreateHandler(out _, out _, requiresTaxId: true);

        var act = async () => await handler.Handle(GuestCommand(taxId: null, companyName: "Acme"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This product requires a tax ID at checkout.");
    }

    [Test]
    public async Task InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b()
    {
        var handler = CreateHandler(out var mediator, out _, requiresTaxId: true);
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay");

        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd", idType: "BRN", idValue: "202401001234"),
            CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ResolveClientProfileCommand>(c =>
                c.Tin == "C12345678901"
                && c.CompanyName == "Acme Sdn Bhd"
                && c.IdValue == "202401001234"
                && c.IdType == "BRN"),
            Arg.Any<CancellationToken>());

        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Metadata != null
                && q.Metadata.ContainsKey("is_b2b_required")
                && q.Metadata["is_b2b_required"] == "true"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_ProductFlagOff_DoesNotStampB2b()
    {
        var handler = CreateHandler(out var mediator, out _, requiresTaxId: false);
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay");

        await handler.Handle(GuestCommand(taxId: null, companyName: null), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Metadata == null || !q.Metadata.ContainsKey("is_b2b_required")
                || q.Metadata["is_b2b_required"] != "true"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Consulting", 1, 250m) },
            DateTime.UtcNow.AddDays(1),
            isB2bRequired: true);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/custom");

        var handler = CreateHandler(orgId, repository, mediator);
        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd") with { SessionId = session.Id },
            CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Metadata != null
                && q.Metadata.GetValueOrDefault("is_b2b_required") == "true"),
            Arg.Any<CancellationToken>());

        await mediator.Received(1).Send(
            Arg.Is<ResolveClientProfileCommand>(c =>
                c.Tin == "C12345678901"
                && c.CompanyName == "Acme Sdn Bhd"
                && c.IdValue != "Acme Sdn Bhd"
                && c.IdValue == null
                && c.IdType == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Consulting", 1, 250m) },
            DateTime.UtcNow.AddDays(1),
            isB2bRequired: true);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/custom");
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());

        var handler = CreateHandler(orgId, repository, mediator);
        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd", idType: "BRN", idValue: "202401001234")
                with { SessionId = session.Id },
            CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ResolveClientProfileCommand>(c =>
                c.Tin == "C12345678901"
                && c.CompanyName == "Acme Sdn Bhd"
                && c.IdType == "BRN"
                && c.IdValue == "202401001234"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void MergeClientIntoGateway_StampsB2bWhenRequested()
    {
        var merged = CommerceCheckoutMetadata.MergeClientIntoGateway(
            null, Guid.CreateVersion7(), Guid.CreateVersion7(), isB2bRequired: true);
        merged["is_b2b_required"].Should().Be("true");
    }

    private static InitiateCheckoutCommand GuestCommand(
        string? taxId,
        string? companyName,
        string? idType = null,
        string? idValue = null) =>
        new(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: taxId,
            IdType: idType,
            IdValue: idValue,
            CompanyName: companyName,
            AddressLine1: null,
            City: null,
            PostalCode: null,
            StateCode: null,
            CountryCode: null,
            Quantity: 1,
            IsGuestCheckout: true,
            CouponCode: null);

    private static InitiateCheckoutCommandHandler CreateHandler(
        out IMediator mediator,
        out ICommerceRepository repository,
        bool requiresTaxId)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Pro Plan",
            "pro-plan",
            100m,
            "FIXED",
            0m,
            "MYR",
            "one_time",
            "STRIPE",
            new CheckoutConfiguration(false, requiresTaxId, false),
            new[] { "telegram" });

        repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        mediator = Substitute.For<IMediator>();
        return CreateHandler(orgId, repository, mediator);
    }

    private static InitiateCheckoutCommandHandler CreateHandler(
        Guid orgId,
        ICommerceRepository repository,
        IMediator mediator)
    {
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        return new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);
    }
}
