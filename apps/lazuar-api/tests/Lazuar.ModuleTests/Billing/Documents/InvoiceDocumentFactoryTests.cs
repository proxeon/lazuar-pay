using System;
using FluentAssertions;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Domain.ValueObjects;
using Modules.Billing.Infrastructure.Documents;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Documents;

[TestFixture]
public class InvoiceDocumentFactoryTests
{
    [Test]
    public void CreateHeader_MapsSstSsmAndFullAddress()
    {
        var profile = new TenantBillingProfile(Guid.CreateVersion7(), "Acme Sdn Bhd", "C12345678901");
        profile.UpdateProfile(
            "Acme Sdn Bhd",
            "C12345678901",
            "202001012345",
            "W10-1808-12345678",
            "https://cdn.example/logo.png",
            new TenantBillingAddress("Line 1", "Line 2", null, "Kuala Lumpur", "50000", "14", "MYS"));

        var workspace = new WorkspaceSnapshotDto(profile.OrganizationId, "Trading Name", "acme", true, DateTime.UtcNow);
        var customer = new CommerceCustomerDisplay("Aisha", "aisha@example.com", "IG123456789012", "Buyer Sdn Bhd", "Buyer 1", null, "PJ", "46000", "10");

        var model = InvoiceDocumentFactory.CreateHeader(
            "Tax Invoice",
            "INV-2026-00001",
            DateTime.UtcNow,
            profile,
            workspace,
            customer,
            logoBytes: new byte[] { 1, 2, 3 });

        model.CompanyName.Should().Be("Acme Sdn Bhd");
        model.CompanyTin.Should().Be("C12345678901");
        model.CompanyRegistrationNumber.Should().Be("202001012345");
        model.CompanySstNumber.Should().Be("W10-1808-12345678");
        model.CompanyAddress.Should().Contain("Line 1").And.Contain("Line 2").And.Contain("50000");
        model.CustomerTin.Should().Be("IG123456789012");
        model.CustomerCompanyName.Should().Be("Buyer Sdn Bhd");
        model.CompanyLogo.Should().NotBeNull();
    }

    [Test]
    public void CreateHeader_WithoutProfile_UsesWorkspaceName_NotLazuarMerchant()
    {
        var workspace = new WorkspaceSnapshotDto(Guid.CreateVersion7(), "Studio Nine", "studio", true, DateTime.UtcNow);
        var model = InvoiceDocumentFactory.CreateHeader(
            "Official Receipt",
            "RCPT-2026-00001",
            DateTime.UtcNow,
            profile: null,
            workspace,
            customer: null,
            logoBytes: null);

        model.CompanyName.Should().Be("Studio Nine");
        model.CompanyName.Should().NotBe("Lazuar Merchant");
        model.CompanyTin.Should().BeEmpty();
        model.Notes.Should().Contain("not a validated MyInvois tax invoice");
    }

    [Test]
    public void CreateHeader_TaxInvoice_DoesNotAddReceiptDisclaimer()
    {
        var profile = new TenantBillingProfile(Guid.CreateVersion7(), "Acme Sdn Bhd", "C12345678901");
        var model = InvoiceDocumentFactory.CreateHeader(
            "Tax Invoice",
            "INV-2026-00001",
            DateTime.UtcNow,
            profile,
            workspace: null,
            customer: null,
            logoBytes: null);

        model.CompanyTin.Should().Be("C12345678901");
        model.Notes.Should().BeNull();
    }

    [Test]
    public void OfficialReceiptDisclaimer_OnlyForReceipts()
    {
        InvoiceDocumentFactory.OfficialReceiptDisclaimer("Official Receipt")
            .Should().Contain("Official Receipt");
        InvoiceDocumentFactory.OfficialReceiptDisclaimer("Tax Invoice").Should().BeNull();
    }
}
