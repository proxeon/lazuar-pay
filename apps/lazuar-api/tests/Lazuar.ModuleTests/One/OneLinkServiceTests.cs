using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Messaging.Contracts;
using Modules.One.Application;
using Modules.One.Application.EventHandlers;
using Modules.One.Domain.Events;
using Modules.One.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OneLinkServiceTests
{
    [Test]
    public void GetOpsBaseUrl_UsesOpsUrl_AndInviteUrlDoesNotContainClientUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:OpsUrl"] = "http://localhost:3003/",
                ["App:ClientUrl"] = "http://localhost:3004",
            })
            .Build();

        var sut = new OneLinkService(config);
        var inviteUrl = $"{sut.GetOpsBaseUrl()}/accept-invite?token=invite-token";

        inviteUrl.Should().Be("http://localhost:3003/accept-invite?token=invite-token");
        inviteUrl.Should().NotContain("localhost:3004");
        inviteUrl.Should().NotContain(sut.GetClientBaseUrl());
        sut.GetClientBaseUrl().Should().Be("http://localhost:3004");
    }

    [Test]
    public void GetOpsBaseUrl_FallsBackToLocalOps()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        new OneLinkService(config).GetOpsBaseUrl().Should().Be("http://localhost:3003");
    }

    [Test]
    public async Task InviteEmail_UsesOpsAcceptUrl_NotClientUrl()
    {
        var links = Substitute.For<IOneLinkService>();
        links.GetOpsBaseUrl().Returns("http://localhost:3003");
        links.GetClientBaseUrl().Returns("http://localhost:3004");

        var eventBus = Substitute.For<IEventBus>();
        var handler = new NotificationDispatchDomainEventHandlers(eventBus, links);
        var evt = new WorkspaceInvitationCreatedDomainEvent(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "staff@example.com",
            "MEMBER",
            "invite-token");

        await handler.Handle(evt, CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.OrganizationId == Guid.Empty
            && e.ToEmail == "staff@example.com"
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("http://localhost:3003/accept-invite?token=invite-token")
            && !e.HtmlEmailBody.Contains("localhost:3004")
            && !e.HtmlEmailBody.Contains("http://localhost:3004")));
    }
}
