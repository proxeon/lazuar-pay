using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.Commerce.Infrastructure.Repositories;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class AnonymizeSubscriberCommandHandlerTests
{
    [Test]
    public async Task Handle_ScrubsLogsForOrgEmail_AndSendsCrmCommand()
    {
        var orgId = Guid.CreateVersion7();
        var otherOrg = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var sub = new Subscription(orgId, profileId, product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));

        await using var db = CreateDb();
        db.Products.Add(product);
        db.Subscriptions.Add(sub);
        var match = NewLog(orgId, "Ahmad", "Buyer@Example.com");
        var otherBuyer = NewLog(orgId, "Siti", "siti@example.com");
        var foreign = NewLog(otherOrg, "Ahmad", "buyer@example.com");
        db.TransactionLogs.AddRange(match, otherBuyer, foreign);
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), profileId).Returns(new ClientProfileDto
        {
            Id = profileId.ToString(),
            Full_name = "Ahmad",
            Email = "buyer@example.com",
            Phone = "601",
            Consented_to_marketing = true
        });
        var mediator = Substitute.For<IMediator>();
        var handler = new AnonymizeSubscriberCommandHandler(new CommerceRepository(db), crm, mediator);

        await handler.Handle(new AnonymizeSubscriberCommand(orgId, sub.Id), CancellationToken.None);

        match.CustomerName.Should().Be("Anonymized User");
        match.CustomerEmail.Should().Be($"deleted_{profileId}@localhost");
        match.Amount.Should().Be(50m);
        otherBuyer.CustomerEmail.Should().Be("siti@example.com");
        foreign.CustomerEmail.Should().Be("buyer@example.com");
        await mediator.Received(1).Send(
            Arg.Is<AnonymizeClientProfileCommand>(c =>
                c.OrganizationId == orgId && c.ClientProfileId == profileId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_AlreadyAnonymized_SkipsLogScrub_StillSendsCrmCommand()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var sub = new Subscription(orgId, profileId, product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), profileId).Returns(new ClientProfileDto
        {
            Id = profileId.ToString(),
            Full_name = "Anonymized User",
            Email = $"deleted_{profileId}@localhost",
            Phone = "",
            Consented_to_marketing = false
        });
        var mediator = Substitute.For<IMediator>();
        var handler = new AnonymizeSubscriberCommandHandler(repository, crm, mediator);

        await handler.Handle(new AnonymizeSubscriberCommand(orgId, sub.Id), CancellationToken.None);

        await repository.DidNotReceive().GetTransactionLogsByCustomerEmailAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(Arg.Any<AnonymizeClientProfileCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WrongOrg_ThrowsNotFound()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var product = CreateProduct(ownerOrg);
        var sub = new Subscription(ownerOrg, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        var mediator = Substitute.For<IMediator>();
        var handler = new AnonymizeSubscriberCommandHandler(
            repository, Substitute.For<ICrmQueryService>(), mediator);

        var act = async () => await handler.Handle(
            new AnonymizeSubscriberCommand(attackerOrg, sub.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
        await mediator.DidNotReceive().Send(Arg.Any<AnonymizeClientProfileCommand>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_UnknownSubscription_ThrowsNotFound()
    {
        var orgId = Guid.CreateVersion7();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);
        var mediator = Substitute.For<IMediator>();
        var handler = new AnonymizeSubscriberCommandHandler(
            repository, Substitute.For<ICrmQueryService>(), mediator);

        var act = async () => await handler.Handle(
            new AnonymizeSubscriberCommand(orgId, Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Test]
    public async Task CommerceConsumer_CancelsMatchingSubs_LeavesOthers()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var otherProfile = Guid.CreateVersion7();
        var product = CreateProduct(orgId);

        var active = new Subscription(orgId, profileId, product.Id);
        active.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        var pastDue = new Subscription(orgId, profileId, product.Id);
        pastDue.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        pastDue.MarkAsPastDue();
        var alreadyCanceled = new Subscription(orgId, profileId, product.Id);
        alreadyCanceled.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        alreadyCanceled.Cancel();
        var other = new Subscription(orgId, otherProfile, product.Id);
        other.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));

        await using var db = CreateDb();
        db.Products.Add(product);
        db.Subscriptions.AddRange(active, pastDue, alreadyCanceled, other);
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ClientProfileAnonymizedIntegrationEventHandler(
            db,
            eventBus,
            Substitute.For<ILogger<ClientProfileAnonymizedIntegrationEventHandler>>());

        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, "buyer@example.com", "601"));

        active.Status.Should().Be("CANCELED");
        pastDue.Status.Should().Be("CANCELED");
        alreadyCanceled.Status.Should().Be("CANCELED");
        other.Status.Should().Be("ACTIVE");

        await eventBus.Received(2).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await eventBus.Received().PublishAsync(Arg.Is<SubscriptionCanceledIntegrationEvent>(e =>
            e.SubscriptionId == active.Id && e.ClientProfileId == profileId));
        await eventBus.Received().PublishAsync(Arg.Is<SubscriptionCanceledIntegrationEvent>(e =>
            e.SubscriptionId == pastDue.Id && e.ClientProfileId == profileId));
    }

    [Test]
    public void TransactionLog_Anonymize_KeepsAmounts_WipesNameEmail()
    {
        var profileId = Guid.CreateVersion7();
        var log = NewLog(Guid.CreateVersion7(), "Ahmad", "ahmad@example.com");

        log.Anonymize(profileId);

        log.CustomerName.Should().Be("Anonymized User");
        log.CustomerEmail.Should().Be($"deleted_{profileId}@localhost");
        log.Amount.Should().Be(50m);
        log.NetAmount.Should().Be(50m);
        log.Status.Should().Be("CONFIRMED");
    }

    private static CommerceTransactionLog NewLog(Guid orgId, string name, string email) =>
        new(orgId, 50m, 0m, "MYR", "CONFIRMED", name, email, "Plan", "SYSTEM", "ref-1");

    private static Product CreateProduct(Guid orgId) =>
        new(orgId, "Plan", "plan", 10m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
