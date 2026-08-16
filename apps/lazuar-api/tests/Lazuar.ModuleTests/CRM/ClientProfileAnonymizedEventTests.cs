using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;
using Modules.CRM.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.CRM;

[TestFixture]
public class ClientProfileAnonymizedEventTests
{
    [Test]
    public void Event_CarriesPreWipeContactDetails()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var @event = new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, "user@example.com", "60123456789");

        @event.OrganizationId.Should().Be(orgId);
        @event.ClientProfileId.Should().Be(profileId);
        @event.Email.Should().Be("user@example.com");
        @event.Phone.Should().Be("60123456789");
        @event.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public void Anonymize_WipesPiiAndConsent()
    {
        var profile = new ClientProfileEntity
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            FullName = "Ahmad",
            Email = "ahmad@example.com",
            Phone = "60111111111",
            Tin = "IG123",
            IdType = "NRIC",
            IdValue = "900101011234",
            Address = new BillingAddress("1 Jalan", null, null, "KL", "50000", "14", "MYS"),
            ConsentedToMarketing = true,
            GlobalUserId = Guid.CreateVersion7()
        };

        profile.Anonymize();

        profile.FullName.Should().Be("Anonymized User");
        profile.Email.Should().Be($"deleted_{profile.Id}@localhost");
        profile.IsAnonymized().Should().BeTrue();
        profile.Phone.Should().BeEmpty();
        profile.Tin.Should().BeNull();
        profile.IdType.Should().BeNull();
        profile.IdValue.Should().BeNull();
        profile.Address.Should().BeNull();
        profile.ConsentedToMarketing.Should().BeFalse();
        profile.GlobalUserId.Should().BeNull();
    }

    [Test]
    public void IsAnonymizedEmail_MatchesDummyOnly()
    {
        ClientProfileEntity.IsAnonymizedEmail("deleted_abc@localhost").Should().BeTrue();
        ClientProfileEntity.IsAnonymizedEmail("DELETED_abc@LOCALHOST").Should().BeTrue();
        ClientProfileEntity.IsAnonymizedEmail("buyer@example.com").Should().BeFalse();
        ClientProfileEntity.IsAnonymizedEmail("deleted_abc@example.com").Should().BeFalse();
        ClientProfileEntity.IsAnonymizedEmail(null).Should().BeFalse();
    }

    [Test]
    public void CreateAndResolveCommands_DefaultConsentFalse()
    {
        var create = new CreateClientProfileCommand(
            Guid.CreateVersion7(), "Name", "a@b.com", "601");
        create.ConsentedToMarketing.Should().BeFalse();

        var resolve = new ResolveClientProfileCommand(
            Guid.CreateVersion7(), "Name", "a@b.com", "601");
        resolve.ConsentedToMarketing.Should().BeFalse();
    }

    [Test]
    public async Task Handle_HappyPath_WipesPii_AndPersistsOutboxWithPreWipeEmail()
    {
        var orgId = Guid.CreateVersion7();
        var profile = LiveProfile(orgId, "ahmad@example.com");
        await using var db = CreateDb();
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var bus = new OutboxEventBus<CrmDbContext>(db);
        var handler = new AnonymizeClientProfileCommandHandler(db, bus);

        await handler.Handle(new AnonymizeClientProfileCommand(orgId, profile.Id), CancellationToken.None);

        var reloaded = await db.ClientProfiles.IgnoreQueryFilters().SingleAsync(p => p.Id == profile.Id);
        reloaded.Email.Should().Be($"deleted_{profile.Id}@localhost");
        reloaded.FullName.Should().Be("Anonymized User");
        reloaded.Tin.Should().BeNull();
        reloaded.ConsentedToMarketing.Should().BeFalse();

        var outbox = await db.OutboxMessages.ToListAsync();
        outbox.Should().HaveCount(1);
        outbox[0].Type.Should().Contain("ClientProfileAnonymizedIntegrationEvent");
        outbox[0].Data.Should().Contain("ahmad@example.com");
        outbox[0].Data.Should().NotContain($"deleted_{profile.Id}@localhost");
        outbox[0].ProcessedAt.Should().BeNull();
    }

    [Test]
    public async Task Handle_AlreadyAnonymized_DoesNotPublishSecondEvent()
    {
        var orgId = Guid.CreateVersion7();
        var profile = LiveProfile(orgId, "ahmad@example.com");
        profile.Anonymize();
        await using var db = CreateDb();
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var handler = new AnonymizeClientProfileCommandHandler(db, eventBus);

        await handler.Handle(new AnonymizeClientProfileCommand(orgId, profile.Id), CancellationToken.None);

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<ClientProfileAnonymizedIntegrationEvent>());
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_WrongOrg_ThrowsNotFound_AndDoesNotPublish()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var profile = LiveProfile(ownerOrg, "ahmad@example.com");
        await using var db = CreateDb();
        db.ClientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var handler = new AnonymizeClientProfileCommandHandler(db, eventBus);

        var act = async () => await handler.Handle(
            new AnonymizeClientProfileCommand(attackerOrg, profile.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<ClientProfileAnonymizedIntegrationEvent>());
        profile.Email.Should().Be("ahmad@example.com");
    }

    private static ClientProfileEntity LiveProfile(Guid orgId, string email) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = orgId,
            FullName = "Ahmad",
            Email = email,
            Phone = "60111111111",
            Tin = "IG123",
            ConsentedToMarketing = true,
            GlobalUserId = Guid.CreateVersion7()
        };

    private static CrmDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CrmDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
