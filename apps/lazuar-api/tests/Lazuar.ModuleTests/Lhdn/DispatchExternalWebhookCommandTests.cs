using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Commerce.Contracts.Events;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

/// <summary>
/// R42 A1: LHDN lifecycle enqueues via OutboundWebhookRequestedIntegrationEvent
/// (fire-and-forget sender retired R43).
/// </summary>
[TestFixture]
public class DispatchExternalWebhookCommandTests
{
    private static readonly Guid OrgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private IEventBus _eventBus = null!;
    private ILhdnLinkService _linkService = null!;
    private DispatchExternalWebhookCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _eventBus = Substitute.For<IEventBus>();
        _linkService = Substitute.For<ILhdnLinkService>();
        _linkService.GetPortalUrl(Arg.Any<string?>()).Returns("https://preprod.myinvois.hasil.gov.my");
        var repo = Substitute.For<ILhdnRepository>();
        repo.GetTenantConfigAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new LhdnTenantConfig(OrgId, false, "C1", "BRN", "1", "SANDBOX"));
        _handler = new DispatchExternalWebhookCommandHandler(_eventBus, _linkService, repo);
    }

    [Test]
    public async Task Valid_Publishes_InvoiceValid_With_Null_TargetUrl_And_Data_Payload()
    {
        OutboundWebhookRequestedIntegrationEvent? published = null;
        _eventBus.When(x => x.PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>()))
            .Do(ci => published = ci.Arg<OutboundWebhookRequestedIntegrationEvent>());

        await _handler.Handle(
            new DispatchExternalWebhookCommand(
                OrgId,
                InternalId: "inv-100",
                Status: "VALID",
                LhdnUuid: "uuid-abc",
                LongId: "long-xyz",
                ErrorMessage: null),
            CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());

        published.Should().NotBeNull();
        published!.OrganizationId.Should().Be(OrgId);
        published.TargetUrl.Should().BeNull();
        published.EventType.Should().Be("invoice.valid");

        var data = published.Payload;
        data.GetProperty("internal_id").GetString().Should().Be("inv-100");
        data.GetProperty("lhdn_uuid").GetString().Should().Be("uuid-abc");
        data.GetProperty("status").GetString().Should().Be("VALID");
        data.GetProperty("qr_link").GetString()
            .Should().Be("https://preprod.myinvois.hasil.gov.my/uuid-abc/share/long-xyz");
        data.GetProperty("error_message").ValueKind.Should().Be(JsonValueKind.Null);

        // Data-only: no legacy top-level "event" wrapper, no timestamp in data (envelope owns created_at).
        data.TryGetProperty("event", out _).Should().BeFalse();
        data.TryGetProperty("timestamp", out _).Should().BeFalse();
    }

    [Test]
    public async Task Invalid_Publishes_InvoiceInvalid_With_ErrorMessage()
    {
        OutboundWebhookRequestedIntegrationEvent? published = null;
        _eventBus.When(x => x.PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>()))
            .Do(ci => published = ci.Arg<OutboundWebhookRequestedIntegrationEvent>());

        await _handler.Handle(
            new DispatchExternalWebhookCommand(
                OrgId,
                InternalId: "inv-200",
                Status: "INVALID",
                LhdnUuid: "uuid-bad",
                LongId: null,
                ErrorMessage: "Schema validation failed"),
            CancellationToken.None);

        published.Should().NotBeNull();
        published!.EventType.Should().Be("invoice.invalid");
        published.TargetUrl.Should().BeNull();
        published.OrganizationId.Should().Be(OrgId);

        var data = published.Payload;
        data.GetProperty("internal_id").GetString().Should().Be("inv-200");
        data.GetProperty("lhdn_uuid").GetString().Should().Be("uuid-bad");
        data.GetProperty("status").GetString().Should().Be("INVALID");
        data.GetProperty("error_message").GetString().Should().Be("Schema validation failed");
        data.GetProperty("qr_link").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public async Task Does_Not_Use_FireAndForget_Sender_Path()
    {
        // Handler publishes via One event bus (R43); portal host comes from tenant Environment.
        // Publish is the sole side effect.
        await _handler.Handle(
            new DispatchExternalWebhookCommand(
                OrgId, "inv-1", "VALID", "u", "l", null),
            CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "invoice.valid" && e.TargetUrl == null));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "invoice.invalid"));
    }
}
