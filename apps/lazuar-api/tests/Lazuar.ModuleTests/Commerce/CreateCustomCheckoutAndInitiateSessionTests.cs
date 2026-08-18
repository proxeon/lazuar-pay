using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts.Commands;
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
public class CreateCustomCheckoutAndInitiateSessionTests
{
    [Test]
    public async Task CreateCustomCheckout_AllocatesQuoteNumberOnce()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        CheckoutSession? captured = null;

        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => captured = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(profileId);
        mediator.Send(Arg.Any<GetPaymentConfigQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<PaymentConfigDto>());
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("QT-2026-00001");

        var handler = new CreateCustomCheckoutCommandHandler(repository, mediator);
        var id = await handler.Handle(new CreateCustomCheckoutCommand(
            orgId,
            "buyer@example.com",
            "Buyer",
            new List<CustomLineItemData> { new("Design", 1, 500m) },
            null,
            false,
            null), CancellationToken.None);

        id.Should().Be(captured!.Id);
        captured.DocumentNumber.Should().Be("QT-2026-00001");
        await mediator.Received(1).Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateCustomCheckout_Net30_SetsDueAtAbout30Days()
    {
        var orgId = Guid.CreateVersion7();
        CheckoutSession? captured = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => captured = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GetPaymentConfigQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<PaymentConfigDto>());
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("QT-2026-00002");

        var before = DateTime.UtcNow;
        var handler = new CreateCustomCheckoutCommandHandler(repository, mediator);
        await handler.Handle(new CreateCustomCheckoutCommand(
            orgId,
            "buyer@example.com",
            "Buyer",
            new List<CustomLineItemData> { new("Design", 1, 500m) },
            null,
            false,
            null,
            null,
            "net_30"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DueAt.Should().NotBeNull();
        captured.DueAt!.Value.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromMinutes(2));
        captured.ExpiresAt.Should().BeOnOrAfter(captured.DueAt.Value.AddDays(14).AddMinutes(-1));
    }

    [Test]
    public async Task InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Work", 1, 100m) },
            DateTime.UtcNow.AddDays(7),
            isB2bRequired: true,
            "BILLPLZ");

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);
        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);
        var mediator = Substitute.For<IMediator>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "http://localhost:3004" })
            .Build();

        var handler = new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);

        var missingTin = async () => await handler.Handle(new InitiateCheckoutCommand(
            "acme", "custom", "Buyer", "buyer@example.com", null, null, null,
            null, null, null, null, null, 1, true, null, session.Id), CancellationToken.None);

        await missingTin.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tax ID*");

        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://pay.example/hop2");
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(session.ClientProfileId);

        var result = await handler.Handle(new InitiateCheckoutCommand(
            "acme", "custom", "Buyer", "buyer@example.com", null, "C111122223333", "Buyer Sdn Bhd",
            null, null, null, null, null, 1, true, null, session.Id,
            IdType: "BRN",
            IdValue: "202401001234"), CancellationToken.None);

        result.Url.Should().Be("https://pay.example/hop2");
        await mediator.Received().Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Metadata.ContainsKey("is_b2b_required")
                && q.Metadata["is_b2b_required"] == "true"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_CompletedSession_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Work", 1, 100m) },
            DateTime.UtcNow.AddDays(7),
            false);
        session.Complete();

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);
        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var handler = new InitiateCheckoutCommandHandler(
            one, repository, Substitute.For<IMediator>(), new ConfigurationBuilder().Build(), comms);

        var act = async () => await handler.Handle(new InitiateCheckoutCommand(
            "acme", "custom", "Buyer", "buyer@example.com", null, null, null,
            null, null, null, null, null, 1, true, null, session.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");
    }
}
