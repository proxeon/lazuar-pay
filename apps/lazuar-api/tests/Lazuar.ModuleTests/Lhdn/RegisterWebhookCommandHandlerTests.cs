using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.One.Application.Commands;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class RegisterWebhookCommandHandlerTests
{
    [Test]
    public async Task Register_DualWritesWorkspaceEndpoint_AndReturnsLiveId()
    {
        var orgId = Guid.CreateVersion7();
        var liveId = Guid.CreateVersion7();
        var repo = Substitute.For<ILhdnRepository>();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CreateWebhookEndpointCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateWebhookEndpointResult(
                liveId,
                "https://erp.example/hooks",
                "whsec_test",
                true,
                new List<string> { "invoice.valid", "invoice.invalid" },
                DateTime.UtcNow));

        var handler = new RegisterWebhookCommandHandler(repo, mediator);
        var id = await handler.Handle(
            new RegisterWebhookCommand(orgId, new RegisterWebhookRequestDto
            {
                Url = "https://erp.example/hooks",
                Secret = "legacy-secret"
            }),
            CancellationToken.None);

        id.Should().Be(liveId);
        repo.Received(1).AddWebhookSubscription(Arg.Any<WebhookSubscription>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<CreateWebhookEndpointCommand>(c =>
                c.OrganizationId == orgId
                && c.Url == "https://erp.example/hooks"
                && c.EnabledEvents != null
                && c.EnabledEvents.Contains("invoice.valid")
                && c.EnabledEvents.Contains("invoice.invalid")),
            Arg.Any<CancellationToken>());
    }
}
