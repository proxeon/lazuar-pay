using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Communications.Domain;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class LifecycleEventHandlersTests
{
    [Test]
    public void SubscriptionSuspended_DoesNotDispatchPaymentFailed()
    {
        typeof(LifecycleEventHandlers)
            .Should().NotBeAssignableTo<IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>>();
    }

    [Test]
    public async Task Cancel_PopulatesSubjectAndNames()
    {
        var (handler, eventBus, tokens, mail, orgId, subId, clientId) = await CreateSut();
        mail.GetSubscriptionMailContextAsync(orgId, subId)
            .Returns(new SubscriptionMailContext(subId, Guid.CreateVersion7(), "Premium Plan", 99m, "MYR",
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), "CANCELED"));
        tokens.GenerateToken(subId).Returns("cancel-token");

        await handler.HandleAsync(new SubscriptionCanceledIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), []));

        tokens.Received(1).GenerateToken(subId);
        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "aisha@example.com"
            && e.Subject.Contains("Premium Plan")
            && e.Subject.Contains("{{") == false
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Aisha Merchant")
            && e.HtmlEmailBody.Contains("Premium Plan")
            && e.HtmlEmailBody.Contains("Acme Studio")
            && e.HtmlEmailBody.Contains("{{") == false
            && e.Channel == "ALL"));
    }

    [Test]
    public async Task Cancel_MissingProfile_DoesNotDispatch()
    {
        var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(
            orgId, DefaultMessageTemplates.GetByName("Subscription Cancelled")!));
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns((ClientProfileDto?)null);
        var eventBus = Substitute.For<IEventBus>();

        var handler = new LifecycleEventHandlers(
            db,
            crm,
            Substitute.For<IOneQueryService>(),
            Substitute.For<ISubscriberQueryService>(),
            Substitute.For<IMagicLinkTokenService>(),
            Config(),
            eventBus);

        await handler.HandleAsync(new SubscriptionCanceledIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), []));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task Cancel_MissingMailContext_StillDispatchesWithLinks()
    {
        var (handler, eventBus, tokens, mail, orgId, subId, clientId) = await CreateSut();
        mail.GetSubscriptionMailContextAsync(orgId, subId).Returns((SubscriptionMailContext?)null);
        tokens.GenerateToken(subId).Returns("cancel-token");

        await handler.HandleAsync(new SubscriptionCanceledIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), []));

        tokens.Received(1).GenerateToken(subId);
        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "aisha@example.com"
            && e.Subject == "Your  membership has ended"
            && e.Subject.Contains("{{") == false
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Aisha Merchant")
            && e.HtmlEmailBody.Contains("{{") == false));
    }

    [Test]
    public async Task Cancel_WhatsAppBodyPopulatedWhenChannelAll()
    {
        var (handler, eventBus, tokens, mail, orgId, subId, clientId) = await CreateSut();
        mail.GetSubscriptionMailContextAsync(orgId, subId)
            .Returns(new SubscriptionMailContext(subId, Guid.CreateVersion7(), "Premium Plan", 99m, "MYR",
                null, "CANCELED"));
        tokens.GenerateToken(subId).Returns("cancel-token");

        await handler.HandleAsync(new SubscriptionCanceledIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), []));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.Channel == "ALL"
            && e.PlainTextPhoneBody != null
            && e.PlainTextPhoneBody.Contains("Aisha Merchant")
            && e.PlainTextPhoneBody.Contains("Premium Plan")
            && e.PlainTextPhoneBody.Contains("{{") == false));
    }

    private static async Task<(
        LifecycleEventHandlers Handler,
        IEventBus EventBus,
        IMagicLinkTokenService Tokens,
        ISubscriberQueryService Mail,
        Guid OrgId,
        Guid SubId,
        Guid ClientId)> CreateSut()
    {
        var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();

        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(
            orgId, DefaultMessageTemplates.GetByName("Subscription Cancelled")!));
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Aisha Merchant",
            Email = "aisha@example.com",
            Phone = "+60123456789"
        });

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var mail = Substitute.For<ISubscriberQueryService>();
        var tokens = Substitute.For<IMagicLinkTokenService>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new LifecycleEventHandlers(db, crm, one, mail, tokens, Config(), eventBus);
        return (handler, eventBus, tokens, mail, orgId, subId, clientId);
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://portal.test" })
            .Build();

    private static CommunicationsDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
