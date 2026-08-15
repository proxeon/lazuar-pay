using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
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
public class PortalAccessEmailHandlerTests
{
    [Test]
    public async Task SubscriptionActivated_FirstPayment_DispatchesPortalAccessWithToken()
    {
        var (handler, eventBus, tokens, orgId, subId, clientId) = await CreateSut();

        tokens.GenerateToken(subId).Returns("magic-token");

        await handler.HandleAsync(new SubscriptionActivatedIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), [], IsFirstPayment: true));

        tokens.Received(1).GenerateToken(subId);
        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "aisha@example.com"
            && e.ToPhone == null
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("token=magic-token")
            && e.HtmlEmailBody.Contains("/acme/portal?token=")
            && e.Channel == "EMAIL"));
    }

    [Test]
    public async Task SubscriptionActivated_NotFirstPayment_NoDispatch()
    {
        var (handler, eventBus, tokens, orgId, subId, clientId) = await CreateSut();

        await handler.HandleAsync(new SubscriptionActivatedIntegrationEvent(
            orgId, subId, clientId, Guid.CreateVersion7(), [], IsFirstPayment: false));

        tokens.DidNotReceive().GenerateToken(Arg.Any<Guid>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task RequestMagicLink_MatchingEmail_Dispatches()
    {
        var (handler, eventBus, tokens, orgId, subId, clientId) = await CreateSut();
        tokens.GenerateToken(subId).Returns("request-token");

        await handler.HandleAsync(new PortalMagicLinkRequestedIntegrationEvent(orgId, subId, clientId));

        tokens.Received(1).GenerateToken(subId);
        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "aisha@example.com"
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("token=request-token")));
    }

    private static async Task<(
        PortalAccessEmailHandlers Handler,
        IEventBus EventBus,
        IMagicLinkTokenService Tokens,
        Guid OrgId,
        Guid SubId,
        Guid ClientId)> CreateSut()
    {
        var db = new CommunicationsDbContext(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();

        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, DefaultMessageTemplates.GetByName("Portal Access")!));
        await db.SaveChangesAsync();

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
        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://portal.test" })
            .Build();

        var handler = new PortalAccessEmailHandlers(db, crm, one, tokens, config, eventBus);
        return (handler, eventBus, tokens, orgId, subId, clientId);
    }
}
