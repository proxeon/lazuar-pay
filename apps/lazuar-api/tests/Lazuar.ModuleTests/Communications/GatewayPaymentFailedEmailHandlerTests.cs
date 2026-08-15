using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts;
using Modules.Communications.Domain;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class GatewayPaymentFailedEmailHandlerTests
{
    [Test]
    public async Task GatewayPaymentFailed_DispatchesPaymentFailed_WithUpdatePaymentUrl()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();

        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, DefaultMessageTemplates.GetByName("Payment Failed")!));
        await db.SaveChangesAsync();

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetSubscriptionCommsContextAsync(orgId, subId, Arg.Any<CancellationToken>())
            .Returns(new CommerceSubscriptionCommsContext(clientId, "PAST_DUE", "Premium Plan"));

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Aisha Merchant",
            Email = "aisha@example.com"
        });

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(subId).Returns("portal-token");

        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, lookup, crm, one, tokens, eventBus);

        await handler.HandleAsync(FailedEvent(orgId, subId));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "aisha@example.com"
            && e.ToPhone == null
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains($"/acme/update-payment/{subId}")
            && e.HtmlEmailBody.Contains("portal.lazuar.com/checkout/update") == false
            && e.Subject.Contains("Premium Plan")
            && e.Subject.Contains("{{plan_name}}") == false));
    }

    [Test]
    public async Task GatewayPaymentFailed_CanceledSub_NoDispatch()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();

        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, DefaultMessageTemplates.GetByName("Payment Failed")!));
        await db.SaveChangesAsync();

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetSubscriptionCommsContextAsync(orgId, subId, Arg.Any<CancellationToken>())
            .Returns(new CommerceSubscriptionCommsContext(clientId, "CANCELED", "Plan"));

        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(
            db,
            lookup,
            Substitute.For<ICrmQueryService>(),
            Substitute.For<IOneQueryService>(),
            Substitute.For<IMagicLinkTokenService>(),
            eventBus);

        await handler.HandleAsync(FailedEvent(orgId, subId));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task GatewayPaymentFailed_NoSubscriptionMetadata_NoDispatch()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, DefaultMessageTemplates.GetByName("Payment Failed")!));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(
            db,
            Substitute.For<ICommerceDocumentLookup>(),
            Substitute.For<ICrmQueryService>(),
            Substitute.For<IOneQueryService>(),
            Substitute.For<IMagicLinkTokenService>(),
            eventBus);

        await handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            orgId,
            "pi_oneoff",
            new Dictionary<string, string> { ["type"] = "one_time" }));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    private static GatewayPaymentFailedIntegrationEventHandler CreateHandler(
        CommunicationsDbContext db,
        ICommerceDocumentLookup lookup,
        ICrmQueryService crm,
        IOneQueryService one,
        IMagicLinkTokenService tokens,
        IEventBus eventBus)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://portal.test" })
            .Build();

        return new GatewayPaymentFailedIntegrationEventHandler(db, lookup, crm, one, tokens, config, eventBus);
    }

    private static GatewayPaymentFailedIntegrationEvent FailedEvent(Guid orgId, Guid subscriptionId) =>
        new(
            orgId,
            "pi_fail",
            new Dictionary<string, string> { ["subscription_id"] = subscriptionId.ToString() });

    private static CommunicationsDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
