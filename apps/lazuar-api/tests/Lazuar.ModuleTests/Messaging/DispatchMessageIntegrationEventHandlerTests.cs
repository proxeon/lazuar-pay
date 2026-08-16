using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Communications.Contracts;
using Modules.Messaging.Application;
using Modules.Messaging.Contracts;
using Modules.Messaging.Infrastructure;
using Modules.Messaging.Infrastructure.EventHandlers;
using Modules.Messaging.Infrastructure.Messaging;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class DispatchMessageIntegrationEventHandlerTests
{
    private MessagingDbContext _db = null!;
    private IEmailService _email = null!;
    private IMessagingService _messaging = null!;
    private IBillingQueryService _billing = null!;
    private ICreditCostService _creditCost = null!;
    private ISuppressionService _suppression = null!;
    private ICommunicationsQueryService _comms = null!;
    private IMediator _mediator = null!;
    private DispatchMessageIntegrationEventHandler _sut = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.Empty);

        _db = new MessagingDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        _email = Substitute.For<IEmailService>();
        _messaging = Substitute.For<IMessagingService>();
        _billing = Substitute.For<IBillingQueryService>();
        _creditCost = Substitute.For<ICreditCostService>();
        _suppression = Substitute.For<ISuppressionService>();
        _comms = Substitute.For<ICommunicationsQueryService>();
        _mediator = Substitute.For<IMediator>();

        _creditCost.GetCost(CreditAction.WhatsAppSend).Returns(0);
        _suppression.IsSuppressedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<SuppressionLane>()).Returns(false);

        _sut = CreateSut();
    }

    private DispatchMessageIntegrationEventHandler CreateSut(
        IMessagingService? messaging = null,
        bool whatsAppEnabled = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:WhatsAppEnabled"] = whatsAppEnabled ? "true" : "false"
            })
            .Build();

        return new DispatchMessageIntegrationEventHandler(
            _email,
            messaging ?? _messaging,
            _billing,
            _creditCost,
            _suppression,
            _comms,
            _db,
            _mediator,
            config,
            NullLogger<DispatchMessageIntegrationEventHandler>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task HandleAsync_EmailChannel_WrapsBrandAndSendsViaIEmailService()
    {
        var orgId = Guid.CreateVersion7();
        _comms.GetEmailConfigCredentialsAsync(orgId).Returns(new TenantEmailCredentials(
            "tenant_key",
            "from@tenant.test",
            IsActive: true));

        _email.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>()).Returns("re_abc");

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "user@example.com",
            ToPhone: null,
            Subject: "Hello",
            HtmlEmailBody: "Line1\nLine2",
            PlainTextPhoneBody: null,
            Channel: "EMAIL");

        await _sut.HandleAsync(evt);

        await _email.Received(1).SendEmailAsync(
            "user@example.com",
            "Hello",
            Arg.Is<string>(html =>
                html.Contains("Line1<br/>Line2")
                && html.Contains("Powered by")
                && html.Contains("Lazuar")),
            orgId,
            "tenant_key",
            "from@tenant.test",
            null);

        await _messaging.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<string>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
        _creditCost.DidNotReceive().GetCost(CreditAction.EmailSend);

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("EMAIL");
        log.ProviderMessageId.Should().Be("re_abc");
    }

    [Test]
    public async Task HandleAsync_TenantEmail_InactiveByok_LogsFailedAndThrowsNoFallback()
    {
        var orgId = Guid.CreateVersion7();
        _comms.GetEmailConfigCredentialsAsync(orgId).Returns(new TenantEmailCredentials(
            "tenant_key",
            "from@tenant.test",
            IsActive: false));

        _email.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Is<string?>(k => string.IsNullOrWhiteSpace(k)),
            Arg.Any<string?>(),
            Arg.Any<string?>())
            .Returns<string?>(_ => throw new InvalidOperationException(
                "No platform fallback allowed for tenant emails. You must configure a valid BYOK Resend API key and Sender Email to dispatch tenant communications."));

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "user@example.com",
            ToPhone: null,
            Subject: "Hello",
            HtmlEmailBody: "<p>Hi</p>",
            PlainTextPhoneBody: null,
            Channel: "EMAIL");

        var act = () => _sut.HandleAsync(evt);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No platform fallback*");

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("FAILED");
        log.Channel.Should().Be("EMAIL");
        log.Error.Should().Contain("No platform fallback");
    }

    [Test]
    public async Task HandleAsync_TenantEmail_NullByok_LogsFailedAndThrowsNoFallback()
    {
        var orgId = Guid.CreateVersion7();
        _comms.GetEmailConfigCredentialsAsync(orgId).Returns((TenantEmailCredentials?)null);

        _email.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Is<string?>(k => string.IsNullOrWhiteSpace(k)),
            Arg.Any<string?>(),
            Arg.Any<string?>())
            .Returns<string?>(_ => throw new InvalidOperationException(
                "No platform fallback allowed for tenant emails. You must configure a valid BYOK Resend API key and Sender Email to dispatch tenant communications."));

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "user@example.com",
            ToPhone: null,
            Subject: "Hello",
            HtmlEmailBody: "<p>Hi</p>",
            PlainTextPhoneBody: null,
            Channel: "EMAIL");

        var act = () => _sut.HandleAsync(evt);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No platform fallback*");

        (await _db.MessageDeliveryLogs.SingleAsync()).Status.Should().Be("FAILED");
    }

    [Test]
    public async Task HandleAsync_SuppressedAddress_SkipsEmailAndDoesNotSend()
    {
        var orgId = Guid.CreateVersion7();
        _suppression.IsSuppressedAsync(orgId, "user@example.com", SuppressionLane.Transactional).Returns(true);
        _comms.GetEmailConfigCredentialsAsync(orgId).Returns(new TenantEmailCredentials(
            "tenant_key",
            "from@tenant.test",
            IsActive: true));

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "user@example.com",
            ToPhone: null,
            Subject: "Hello",
            HtmlEmailBody: "<p>Hi</p>",
            PlainTextPhoneBody: null,
            Channel: "EMAIL");

        await _sut.HandleAsync(evt);

        await _email.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>());

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Channel.Should().Be("EMAIL");
        log.Error.Should().Contain("suppressed");
    }

    [Test]
    public async Task HandleAsync_WhatsAppDisabled_SkipsWhatsAppAndDoesNotCallIMessagingService()
    {
        var orgId = Guid.CreateVersion7();
        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "",
            ToPhone: "+6012",
            Subject: "",
            HtmlEmailBody: null,
            PlainTextPhoneBody: "hi",
            Channel: "WHATSAPP");

        await _sut.HandleAsync(evt);

        await _messaging.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<string>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Channel.Should().Be("WHATSAPP");
        log.Error.Should().Contain("WhatsApp channel disabled");
    }

    [Test]
    public async Task HandleAsync_WhatsAppEnabled_ConsoleTransport_DoesNotDeduct()
    {
        var orgId = Guid.CreateVersion7();
        _creditCost.GetCost(CreditAction.WhatsAppSend).Returns(2);
        _billing.HasSufficientCreditsAsync(orgId, 2).Returns(true);

        var console = new ConsoleMessagingService(NullLogger<ConsoleMessagingService>.Instance);
        var sut = CreateSut(messaging: console, whatsAppEnabled: true);

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "",
            ToPhone: "+6012",
            Subject: "",
            HtmlEmailBody: null,
            PlainTextPhoneBody: "hi",
            Channel: "WHATSAPP");

        await sut.HandleAsync(evt);

        console.Should().BeOfType<ConsoleMessagingService>();
        console.IsBillable.Should().BeFalse();
        _creditCost.Received().GetCost(CreditAction.WhatsAppSend);
        await _mediator.DidNotReceive().Send(
            Arg.Is<DeductTenantCreditCommand>(c => c.Amount == 2 || c.Amount == 0),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
        await _billing.DidNotReceive().HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>());

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("WHATSAPP");
    }

    [Test]
    public async Task HandleAsync_WhatsAppEnabled_CostZero_SubstituteTransport_DoesNotDeduct()
    {
        var orgId = Guid.CreateVersion7();
        _creditCost.GetCost(CreditAction.WhatsAppSend).Returns(0);
        var sut = CreateSut(whatsAppEnabled: true);

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "",
            ToPhone: "+6012",
            Subject: "",
            HtmlEmailBody: null,
            PlainTextPhoneBody: "hi",
            Channel: "WHATSAPP");

        await sut.HandleAsync(evt);

        await _messaging.Received(1).SendMessageAsync("+6012", "hi");
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
        await _billing.DidNotReceive().HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>());

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SENT");
        log.Channel.Should().Be("WHATSAPP");
    }

    [Test]
    public async Task HandleAsync_WhatsAppDisabled_CostTwo_DoesNotDeduct()
    {
        var orgId = Guid.CreateVersion7();
        _creditCost.GetCost(CreditAction.WhatsAppSend).Returns(2);
        var sut = CreateSut(whatsAppEnabled: false);

        var evt = new DispatchMessageIntegrationEvent(
            OrganizationId: orgId,
            ToEmail: "",
            ToPhone: "+6012",
            Subject: "",
            HtmlEmailBody: null,
            PlainTextPhoneBody: "hi",
            Channel: "WHATSAPP");

        await sut.HandleAsync(evt);

        await _messaging.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<string>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());

        var log = await _db.MessageDeliveryLogs.SingleAsync();
        log.Status.Should().Be("SKIPPED");
        log.Error.Should().Contain("WhatsApp channel disabled");
    }
}
