using System;
using System.Linq;
using FluentAssertions;
using Modules.Communications.Domain;
using Modules.Communications.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class DefaultMessageTemplatesTests
{
    [Test]
    public void Catalog_IncludesLifecycleAndDocumentTemplatesOnly()
    {
        var names = DefaultMessageTemplates.All.Select(d => d.Name).ToList();

        names.Should().Contain(new[]
        {
            "Payment Failed",
            "Subscription Cancelled",
            "Digital Product Delivery",
            "Quotation Ready",
            "Official Receipt",
            "Tax Invoice",
            "Credit Note",
            "Portal Access",
            "Invoice Reminder"
        });

        names.Should().NotContain("Community Welcome");
        names.Should().NotContain("Abandoned Cart (12h)");
        names.Should().NotContain("Generic Receipt");
    }

    [Test]
    public void OrphanNames_DoesNotIncludeLifecycleTemplates()
    {
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Payment Failed");
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Subscription Cancelled");
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Quotation Ready");
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Official Receipt");
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Digital Product Delivery");
        DefaultMessageTemplates.OrphanNames.Should().NotContain("Portal Access");

        DefaultMessageTemplates.OrphanNames.Should().Contain("Abandoned Cart (12h)");
        DefaultMessageTemplates.OrphanNames.Should().Contain("Community Welcome");
    }

    [Test]
    public void RestoreFromDefault_RestoresCatalogContentAndMarksDefault()
    {
        var orgId = Guid.CreateVersion7();
        var def = DefaultMessageTemplates.GetByName("Payment Failed")!;
        var template = DefaultMessageTemplates.CreateEntity(orgId, def);

        template.UpdateContent("custom subject", "custom body", "custom wa");
        template.IsDefault.Should().BeFalse();

        template.RestoreFromDefault(
            def.Subject, def.EmailBody, def.WhatsAppBody, def.Channel,
            def.RequiredVariables, def.OptionalVariables);

        template.Subject.Should().Be(def.Subject);
        template.EmailBody.Should().Be(def.EmailBody);
        template.WhatsAppBody.Should().Be(def.WhatsAppBody);
        template.IsDefault.Should().BeTrue();
    }

    [Test]
    public void PaymentFailed_RequiresUpdatePaymentLink_AndDoesNotHardcodePortalHost()
    {
        var def = DefaultMessageTemplates.GetByName("Payment Failed")!;
        def.RequiredVariables.Should().Contain("{{update_payment_link}}");
        def.OptionalVariables.Should().Contain("{{renewal_link}}");
        def.EmailBody.Should().Contain("{{update_payment_link}}");
        def.WhatsAppBody.Should().Contain("{{update_payment_link}}");
        def.EmailBody.Should().NotContain("https://portal.lazuar.com");
        def.WhatsAppBody.Should().NotContain("https://portal.lazuar.com");
        def.EmailBody.Should().NotContain("{{renewal_link}}");
    }

    [Test]
    public void PortalAccess_IsEmailOnly_AndRequiresMagicLink()
    {
        var def = DefaultMessageTemplates.GetByName("Portal Access")!;
        def.Channel.Should().Be("EMAIL");
        def.RequiredVariables.Should().Contain("{{portal_magic_link}}");
        def.EmailBody.Should().Contain("{{portal_magic_link}}");
        def.EmailBody.Should().NotContain("download your file");
    }

    [Test]
    public void CreateAllForTenant_ProducesOneRowPerCatalogEntry()
    {
        var templates = DefaultMessageTemplates.CreateAllForTenant(Guid.CreateVersion7()).ToList();
        templates.Should().HaveCount(DefaultMessageTemplates.All.Count);
        templates.Should().OnlyContain(t => t.IsDefault);
    }
}
