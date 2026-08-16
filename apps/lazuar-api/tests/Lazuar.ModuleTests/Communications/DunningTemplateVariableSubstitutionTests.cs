using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Communications.Application;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class DunningTemplateVariableSubstitutionTests
{
    [Test]
    public async Task HandleAsync_Dunning_ReplacesPlanNameAmountCurrencyAndDaysOverdue()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var eventBus = Substitute.For<IEventBus>();
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(subscriptionId).Returns("magic-token");

        var handler = CreateHandler(db, crm, one, eventBus, tokens);

        await handler.HandleAsync(DunningEvent(orgId, clientId, subscriptionId));

        tokens.Received(1).GenerateToken(subscriptionId);

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.OrganizationId == orgId
            && e.ToEmail == "aisha@example.com"
            && e.Subject == "Action Needed: Payment issue for Premium Mastermind"
            && e.Subject.Contains("{{plan_name}}") == false
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Premium Mastermind")
            && e.HtmlEmailBody.Contains("{{plan_name}}") == false
            && e.HtmlEmailBody.Contains("MYR")
            && e.HtmlEmailBody.Contains("99.00")
            && e.HtmlEmailBody.Contains("3")
            && e.HtmlEmailBody.Contains("Aisha Merchant")
            && e.HtmlEmailBody.Contains("Acme Studio")
            && e.HtmlEmailBody.Contains($"https://portal.test/acme/update-payment/{subscriptionId}")
            && e.HtmlEmailBody.Contains("{{update_payment_link}}") == false
            && e.HtmlEmailBody.Contains("Renew:")
            && e.HtmlEmailBody.Contains($"href=\"https://portal.test/acme/update-payment/{subscriptionId}\"")
            && e.HtmlEmailBody.Contains("href=\"https://portal.test/acme/portal\"") == false
            && e.HtmlEmailBody.Contains("https://portal.test/acme/portal?token=magic-token")
            && e.HtmlEmailBody.Contains("{{portal_magic_link}}") == false
            && e.HtmlEmailBody.Contains("{{") == false
            && e.PlainTextPhoneBody == "Premium Mastermind overdue 3"
            && e.PlainTextPhoneBody.Contains("{{plan_name}}") == false));
    }

    [Test]
    public async Task HandleAsync_Dunning_PublishesDispatchMessageToCommunicationsOutbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(subscriptionId).Returns("magic-token");

        var handler = CreateHandler(db, crm, one, new OutboxEventBus<CommunicationsDbContext>(db), tokens);

        await handler.HandleAsync(DunningEvent(orgId, clientId, subscriptionId));

        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(DispatchMessageIntegrationEvent));
        row.Data.Should().Contain("aisha@example.com");
        row.Data.Should().Contain("Action Needed: Payment issue for Premium Mastermind");
        row.ProcessedAt.Should().BeNull();
    }

    [Test]
    public async Task HandleAsync_Dunning_MissingCrmProfile_ThrowsAndDoesNotWriteOutbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns((ClientProfileDto?)null);

        var handler = CreateHandler(
            db,
            crm,
            Substitute.For<IOneQueryService>(),
            new OutboxEventBus<CommunicationsDbContext>(db),
            Substitute.For<IMagicLinkTokenService>());

        var act = () => handler.HandleAsync(DunningEvent(orgId, clientId, subscriptionId));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CRM profile*");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_Dunning_MissingClientProfileId_ThrowsAndDoesNotWriteOutbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var handler = CreateHandler(
            db,
            Substitute.For<ICrmQueryService>(),
            Substitute.For<IOneQueryService>(),
            new OutboxEventBus<CommunicationsDbContext>(db),
            Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            subscription_id = Guid.CreateVersion7().ToString(),
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Pay now"
        });

        var act = () => handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId,
            InternalTargetApp: "COMMUNICATIONS",
            EventType: "reminder.dunning",
            Payload: payload));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*client_profile_id*");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_Dunning_EmptyProfileEmail_ThrowsAndDoesNotWriteOutbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "No Email",
            Email = "   ",
            Phone = "+6012"
        });

        var handler = CreateHandler(
            db,
            crm,
            Substitute.For<IOneQueryService>(),
            new OutboxEventBus<CommunicationsDbContext>(db),
            Substitute.For<IMagicLinkTokenService>());

        var act = () => handler.HandleAsync(DunningEvent(orgId, clientId, subscriptionId));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no email*");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_Dunning_EmptyEmailBody_ThrowsAndDoesNotWriteOutbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var handler = CreateHandler(
            db,
            crm,
            one,
            new OutboxEventBus<CommunicationsDbContext>(db),
            Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            action_type = "EMAIL",
            subject = "Still due",
            email_body = ""
        });

        var act = () => handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId,
            InternalTargetApp: "COMMUNICATIONS",
            EventType: "reminder.dunning",
            Payload: payload));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty EMAIL*");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_Dunning_AmountJsonNumber_FormatsIntoBody()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, crm, one, eventBus, Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            plan_name = "Premium Mastermind",
            amount = 99.00m,
            currency = "MYR",
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Charge {{currency}} {{amount}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId, "COMMUNICATIONS", "reminder.dunning", payload));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("MYR")
            && e.HtmlEmailBody.Contains("99.00")
            && e.HtmlEmailBody.Contains("{{amount}}") == false));
    }

    [Test]
    public async Task HandleAsync_Dunning_CurrentPeriodEndIso_RendersHumanDate()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, crm, one, eventBus, Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            current_period_end = "2026-12-31T00:00:00.0000000Z",
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Ends {{current_period_end}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId, "COMMUNICATIONS", "reminder.dunning", payload));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("31 Dec 2026")
            && e.HtmlEmailBody.Contains("{{current_period_end}}") == false));
    }

    [Test]
    public async Task HandleAsync_Dunning_MissingOptionalKeys_LeavesEmptyNotRawTags()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, crm, one, eventBus, Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Amount={{amount}} Currency={{currency}} End={{current_period_end}} Days={{days_overdue}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId, "COMMUNICATIONS", "reminder.dunning", payload));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Amount=")
            && e.HtmlEmailBody.Contains("{{amount}}") == false
            && e.HtmlEmailBody.Contains("{{currency}}") == false
            && e.HtmlEmailBody.Contains("{{current_period_end}}") == false
            && e.HtmlEmailBody.Contains("{{days_overdue}}") == false));
    }

    [Test]
    public async Task HandleAsync_Dunning_CheckoutUrl_PrefersHostedBillForRenewalLink()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        const string hosted = "https://www.billplz-sandbox.com/bills/renew-1";

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, crm, one, eventBus, Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            checkout_url = hosted,
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Pay {{renewal_link}} Update {{update_payment_link}} Alias {{checkout_url}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId, "COMMUNICATIONS", "reminder.dunning", payload));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains(hosted)
            && e.HtmlEmailBody.Contains($"/update-payment/{subscriptionId}")
            && e.HtmlEmailBody.Contains("{{renewal_link}}") == false
            && e.HtmlEmailBody.Contains("{{update_payment_link}}") == false
            && e.HtmlEmailBody.Contains("{{checkout_url}}") == false));
    }

    [Test]
    public async Task HandleAsync_Dunning_WithoutCheckoutUrl_RenewalLinkFallsBackToUpdatePaymentPage()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(Profile(clientId));
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(db, crm, one, eventBus, Substitute.For<IMagicLinkTokenService>());

        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            action_type = "EMAIL",
            subject = "Due",
            email_body = "Pay {{renewal_link}} Update {{update_payment_link}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId, "COMMUNICATIONS", "reminder.dunning", payload));

        var expectedPage = $"https://portal.test/acme/update-payment/{subscriptionId}";
        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains(expectedPage)
            && e.HtmlEmailBody.Contains("billplz") == false
            && e.HtmlEmailBody.Contains("{{renewal_link}}") == false
            && e.HtmlEmailBody.Contains("{{update_payment_link}}") == false));
    }

    [Test]
    public void DefaultDunningCopy_WithHydrator_LeavesNoRawPlaceholder()
    {
        var ctx = new MessageTemplateContext(
            "Aisha", "a@x.com", "+60", "Acme", "Founders Mastermind",
            "99.00", "99.00", "MYR", "0", "31 Dec 2026",
            "https://www.billplz-sandbox.com/bills/renew-1",
            "https://portal.test/acme/portal?token=t",
            "https://portal.test/acme/update-payment/s");

        var subject = MessageTemplateHydrator.Populate(
            "{{plan_name}} is due — pay this cycle", ctx);
        var body = MessageTemplateHydrator.Populate(
            "{{plan_name}} is due today ({{amount}} {{currency}}). [Pay now]({{renewal_link}})",
            ctx);

        subject.Should().Be("Founders Mastermind is due — pay this cycle");
        body.Should().Contain("Founders Mastermind");
        body.Should().Contain("https://www.billplz-sandbox.com/bills/renew-1");
        body.Should().NotContain("update your payment method");
        subject.Should().NotContain("{{");
        body.Should().NotContain("{{");
    }

    private static FulfillmentRequestedIntegrationEventHandler CreateHandler(
        CommunicationsDbContext db,
        ICrmQueryService crm,
        IOneQueryService one,
        IEventBus eventBus,
        IMagicLinkTokenService tokens)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        return new FulfillmentRequestedIntegrationEventHandler(
            Substitute.For<ICommunicationsRepository>(),
            crm,
            one,
            db,
            eventBus,
            config,
            tokens,
            NullLogger<FulfillmentRequestedIntegrationEventHandler>.Instance);
    }

    private static CommunicationsDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

    private static ClientProfileDto Profile(Guid clientId) => new()
    {
        Id = clientId.ToString(),
        Full_name = "Aisha Merchant",
        Email = "aisha@example.com",
        Phone = "+60123456789"
    };

    private static FulfillmentRequestedIntegrationEvent DunningEvent(
        Guid orgId,
        Guid clientId,
        Guid subscriptionId)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            client_profile_id = clientId.ToString(),
            subscription_id = subscriptionId.ToString(),
            plan_name = "Premium Mastermind",
            amount = "99.00",
            total_price = "99.00",
            currency = "MYR",
            days_overdue = "3",
            action_type = "EMAIL",
            subject = "Action Needed: Payment issue for {{plan_name}}",
            email_body = "Hi {{customer_name}}, {{plan_name}} is {{currency}} {{amount}} overdue by {{days_overdue}} days. Fix: {{update_payment_link}} Renew: {{renewal_link}} Biz: {{business_name}} Portal: {{portal_magic_link}}",
            whatsapp_body = "{{plan_name}} overdue {{days_overdue}}"
        });

        return new FulfillmentRequestedIntegrationEvent(
            orgId,
            InternalTargetApp: "COMMUNICATIONS",
            EventType: "reminder.dunning",
            Payload: payload);
    }
}
