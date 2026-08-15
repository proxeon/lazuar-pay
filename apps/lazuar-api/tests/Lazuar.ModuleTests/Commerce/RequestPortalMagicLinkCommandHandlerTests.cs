using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class RequestPortalMagicLinkCommandHandlerTests
{
    [Test]
    public async Task RequestMagicLink_MatchingEmail_PublishesPortalMagicLinkRequested()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var sub = new Subscription(orgId, clientId, Guid.CreateVersion7());

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileByEmailAsync(orgId, "aisha@example.com").Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Email = "aisha@example.com"
        });

        var repo = Substitute.For<ICommerceRepository>();
        repo.GetNewestSubscriptionForClientAsync(orgId, clientId, Arg.Any<CancellationToken>()).Returns(sub);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new RequestPortalMagicLinkCommandHandler(one, crm, repo, eventBus);

        await handler.Handle(new RequestPortalMagicLinkCommand("acme", "aisha@example.com"), CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<PortalMagicLinkRequestedIntegrationEvent>(e =>
            e.OrganizationId == orgId
            && e.SubscriptionId == sub.Id
            && e.ClientProfileId == clientId));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RequestMagicLink_UnknownEmail_NoDispatch_Returns200()
    {
        var orgId = Guid.CreateVersion7();
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileByEmailAsync(orgId, "unknown@example.com").Returns((ClientProfileDto?)null);

        var repo = Substitute.For<ICommerceRepository>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new RequestPortalMagicLinkCommandHandler(one, crm, repo, eventBus);

        await handler.Handle(new RequestPortalMagicLinkCommand("acme", "unknown@example.com"), CancellationToken.None);

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<PortalMagicLinkRequestedIntegrationEvent>());
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
