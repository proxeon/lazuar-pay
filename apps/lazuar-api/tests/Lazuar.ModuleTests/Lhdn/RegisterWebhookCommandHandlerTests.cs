using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.One.Contracts;
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
        var webhooks = Substitute.For<ITenantWebhookRegistry>();
        webhooks.RegisterAsync(
                orgId,
                "https://erp.example/hooks",
                Arg.Is<IReadOnlyList<string>>(e =>
                    e.Contains("invoice.valid") && e.Contains("invoice.invalid")),
                Arg.Any<CancellationToken>())
            .Returns(new TenantWebhookRegisterResult(
                liveId,
                "https://erp.example/hooks",
                true,
                new List<string> { "invoice.valid", "invoice.invalid" }));

        var handler = new RegisterWebhookCommandHandler(repo, webhooks);
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
        await webhooks.Received(1).RegisterAsync(
            orgId,
            "https://erp.example/hooks",
            Arg.Is<IReadOnlyList<string>>(e =>
                e.Contains("invoice.valid") && e.Contains("invoice.invalid")),
            Arg.Any<CancellationToken>());
    }
}
