using System;
using FluentAssertions;
using Modules.Communications.Application;
using Modules.Communications.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class MessageTemplateHydratorTests
{
    private static readonly MessageTemplateContext Sample = new(
        CustomerName: "Aisha Merchant",
        CustomerEmail: "aisha@example.com",
        CustomerPhone: "+60123456789",
        BusinessName: "Acme Studio",
        PlanName: "Premium Mastermind",
        Amount: "99.00",
        TotalPrice: "99.00",
        Currency: "MYR",
        DaysOverdue: "3",
        CurrentPeriodEnd: "31 Dec 2026",
        RenewalLink: "https://portal.test/acme/update-payment/sub-1",
        PortalMagicLink: "https://portal.test/acme/portal?token=magic",
        UpdatePaymentLink: "https://portal.test/acme/update-payment/sub-1");

    [Test]
    public void Populate_IsCaseInsensitive()
    {
        var result = MessageTemplateHydrator.Populate("Hi {{CUSTOMER_NAME}} — {{Plan_Name}}", Sample);

        result.Should().Be("Hi Aisha Merchant — Premium Mastermind");
    }

    [Test]
    public void Populate_NullOrEmpty_ReturnsEmpty()
    {
        MessageTemplateHydrator.Populate(null, Sample).Should().BeEmpty();
        MessageTemplateHydrator.Populate("", Sample).Should().BeEmpty();
    }

    [Test]
    public void Populate_EveryContextField_RoundTrips()
    {
        const string template =
            "{{customer_name}}|{{customer_email}}|{{customer_phone}}|{{business_name}}|{{plan_name}}|" +
            "{{amount}}|{{total_price}}|{{currency}}|{{days_overdue}}|{{current_period_end}}|" +
            "{{renewal_link}}|{{portal_magic_link}}|{{update_payment_link}}";

        var result = MessageTemplateHydrator.Populate(template, Sample);

        result.Should().Be(
            "Aisha Merchant|aisha@example.com|+60123456789|Acme Studio|Premium Mastermind|" +
            "99.00|99.00|MYR|3|31 Dec 2026|" +
            "https://portal.test/acme/update-payment/sub-1|https://portal.test/acme/portal?token=magic|https://portal.test/acme/update-payment/sub-1");
    }

    [Test]
    public void Populate_UnknownTag_IsNotStripped()
    {
        MessageTemplateHydrator.Populate("See {{garbage}} please", Sample)
            .Should().Be("See {{garbage}} please");
    }

    [Test]
    public void Populate_CatalogPaymentFailedAndCancelled_LeaveNoKnownTags()
    {
        var failed = DefaultMessageTemplates.GetByName("Payment Failed")!;
        var cancelled = DefaultMessageTemplates.GetByName("Subscription Cancelled")!;

        AssertNoKnownTags(MessageTemplateHydrator.Populate(failed.Subject, Sample));
        AssertNoKnownTags(MessageTemplateHydrator.Populate(failed.EmailBody, Sample));
        AssertNoKnownTags(MessageTemplateHydrator.Populate(failed.WhatsAppBody, Sample));
        AssertNoKnownTags(MessageTemplateHydrator.Populate(cancelled.Subject, Sample));
        AssertNoKnownTags(MessageTemplateHydrator.Populate(cancelled.EmailBody, Sample));
        AssertNoKnownTags(MessageTemplateHydrator.Populate(cancelled.WhatsAppBody, Sample));
    }

    [Test]
    public void Populate_DefaultCampaignCopy_LeavesNoKnownTags()
    {
        string[] copy =
        [
            "Upcoming renewal for {{plan_name}}",
            "Your {{plan_name}} subscription will renew in 3 days. Ensure your payment method is up to date here: {{update_payment_link}}",
            "Action Required: {{plan_name}} renewal due today",
            "Your {{plan_name}} subscription is due today. To maintain access, please update your payment method here: {{update_payment_link}}",
            "Your {{plan_name}} subscription is past due",
            "Hey {{customer_name}}, your {{plan_name}} subscription is past due. You can securely update your payment method to restore access here: {{update_payment_link}}"
        ];

        foreach (var template in copy)
        {
            AssertNoKnownTags(MessageTemplateHydrator.Populate(template, Sample));
        }
    }

    [Test]
    public void RenewalLink_IsAliasOfUpdatePaymentLink()
    {
        var links = MessageLinkBuilder.Build("https://portal.test", "acme", "sub-1", "tok");
        links.RenewalLink.Should().Be(links.UpdatePaymentLink);
        links.UpdatePaymentLink.Should().Be("https://portal.test/acme/update-payment/sub-1");
        links.PortalMagicLink.Should().Be("https://portal.test/acme/portal?token=tok");
    }

    [Test]
    public void FormatPeriodEnd_UsesEnGbHumanDate()
    {
        MessageTemplateHydrator.FormatPeriodEnd("2026-12-31").Should().Be("31 Dec 2026");
        MessageTemplateHydrator.FormatPeriodEnd(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be("31 Dec 2026");
    }

    [Test]
    public void FormatMoney_UsesInvariantTwoDecimals()
    {
        MessageTemplateHydrator.FormatMoney(99m).Should().Be("99.00");
        MessageTemplateHydrator.FormatMoney("99").Should().Be("99.00");
        MessageTemplateHydrator.FormatMoney("").Should().BeEmpty();
    }

    [Test]
    public void PopulatePreview_IncludesUpdatePaymentLink()
    {
        var preview = MessageTemplateHydrator.PopulatePreview("Pay {{update_payment_link}} / {{amount}} {{currency}}");
        preview.Should().Contain("/acme/update-payment/");
        preview.Should().Contain("99.00");
        preview.Should().Contain("MYR");
        preview.Should().NotContain("{{");
    }

    private static void AssertNoKnownTags(string populated)
    {
        populated.Should().NotContain("{{customer_name}}");
        populated.Should().NotContain("{{customer_email}}");
        populated.Should().NotContain("{{customer_phone}}");
        populated.Should().NotContain("{{business_name}}");
        populated.Should().NotContain("{{plan_name}}");
        populated.Should().NotContain("{{amount}}");
        populated.Should().NotContain("{{total_price}}");
        populated.Should().NotContain("{{currency}}");
        populated.Should().NotContain("{{days_overdue}}");
        populated.Should().NotContain("{{current_period_end}}");
        populated.Should().NotContain("{{renewal_link}}");
        populated.Should().NotContain("{{portal_magic_link}}");
        populated.Should().NotContain("{{update_payment_link}}");
    }
}
