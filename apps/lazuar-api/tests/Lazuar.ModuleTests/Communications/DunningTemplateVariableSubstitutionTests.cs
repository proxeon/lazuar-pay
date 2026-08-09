using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Communications.Application;
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
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var repository = Substitute.For<ICommunicationsRepository>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Aisha Merchant",
            Email = "aisha@example.com",
            Phone = "+60123456789"
        });

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(
            new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(subscriptionId).Returns("magic-token");

        var handler = new FulfillmentRequestedIntegrationEventHandler(
            repository,
            crm,
            one,
            eventBus,
            config,
            tokens);

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
            email_body = "Hi {{customer_name}}, {{plan_name}} is {{currency}} {{amount}} overdue by {{days_overdue}} days. Fix: {{update_payment_link}} Portal: {{portal_magic_link}}",
            whatsapp_body = "{{plan_name}} overdue {{days_overdue}}"
        });

        await handler.HandleAsync(new FulfillmentRequestedIntegrationEvent(
            orgId,
            InternalTargetApp: "COMMUNICATIONS",
            EventType: "reminder.dunning",
            Payload: payload));

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
            && e.HtmlEmailBody.Contains("https://portal.test/acme/portal?token=magic-token")
            && e.HtmlEmailBody.Contains("{{portal_magic_link}}") == false
            && e.PlainTextPhoneBody == "Premium Mastermind overdue 3"
            && e.PlainTextPhoneBody.Contains("{{plan_name}}") == false));
    }

    [Test]
    public void DefaultDunningCopy_WithPlanNamePayload_LeavesNoRawPlaceholder()
    {
        // Documents default dunning copy contract used by AppEntitlementGranted templates / engine payloads.
        const string subjectTemplate = "Action Needed: Payment issue for {{plan_name}}";
        const string bodyTemplate =
            "We tried to process your renewal for {{plan_name}}, but the payment didn't go through.";

        var planName = "Founders Mastermind";
        var subject = subjectTemplate.Replace("{{plan_name}}", planName, StringComparison.OrdinalIgnoreCase);
        var body = bodyTemplate.Replace("{{plan_name}}", planName, StringComparison.OrdinalIgnoreCase);

        subject.Should().Be("Action Needed: Payment issue for Founders Mastermind");
        body.Should().Contain("Founders Mastermind");
        subject.Should().NotContain("{{");
        body.Should().NotContain("{{plan_name}}");
    }
}
