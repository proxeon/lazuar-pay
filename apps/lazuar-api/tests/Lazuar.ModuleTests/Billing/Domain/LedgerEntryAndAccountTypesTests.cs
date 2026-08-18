using System;
using System.Linq;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Domain;

[TestFixture]
public class LedgerEntryAndAccountTypesTests
{
    [Test]
    public void AssignB2cReceipt_SetsCustomerDocument_AndPendingConsolidation()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.ValidateBalanced();

        entry.AssignB2cReceipt("RCPT-2026-0001");

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("RCPT-2026-0001"));
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Pending));
        Assert.That(entry.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.B2cReceipt));
    }

    [Test]
    public void UpdateLhdnStatus_DoesNotOverwriteCustomerDocumentNumber()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AssignB2cReceipt("RCPT-KEEP");

        entry.UpdateLhdnStatus("uuid-from-myinvois", LhdnValidationStatuses.Valid);

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("RCPT-KEEP"));
        Assert.That(entry.LhdnDocumentUuid, Is.EqualTo("uuid-from-myinvois"));
        Assert.That(entry.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.Valid));
        Assert.That(entry.TaxInvoiceId, Is.Not.EqualTo("uuid-from-myinvois"));
    }

    [Test]
    public void UpdateLhdnStatus_KeepsConsolidationTaxInvoiceId()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AssignB2cReceipt("RCPT-KEEP");
        entry.MarkConsolidatedPending("B2C-CONS-202607-abc");

        entry.UpdateLhdnStatus("uuid-from-myinvois", LhdnValidationStatuses.Valid);

        Assert.That(entry.TaxInvoiceId, Is.EqualTo("RCPT-KEEP"));
        Assert.That(entry.LhdnDocumentUuid, Is.EqualTo("uuid-from-myinvois"));
    }

    [Test]
    public void MarkConsolidatedPending_KeepsCustomerDocument_SetsConsolidated()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AssignB2cReceipt("RCPT-KEEP");

        entry.MarkConsolidatedPending("B2C-CONS-202607-abc");

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("RCPT-KEEP"));
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(entry.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.ConsolidatedPending));
        Assert.That(entry.TaxInvoiceId, Is.EqualTo("RCPT-KEEP"));
    }

    [Test]
    public void AccountTypes_ExposeExpectedChartCodes()
    {
        Assert.That(AccountTypes.RevenueGross, Is.EqualTo("REVENUE_GROSS"));
        Assert.That(AccountTypes.LiabilityTaxPayable, Is.EqualTo("LIABILITY_TAX_PAYABLE"));
        Assert.That(AccountTypes.ContraRevenueRefunds, Is.EqualTo("CONTRA_REVENUE_REFUNDS"));
        Assert.That(AccountTypes.ExpenseGatewayFee, Is.EqualTo("EXPENSE_GATEWAY_FEE"));
        Assert.That(LedgerReferenceTypes.SystemCreditChargeback, Is.EqualTo("SYSTEM_CREDIT_CHARGEBACK"));
        Assert.That(LedgerReferenceTypes.SystemSaasFee, Is.EqualTo("SYSTEM_SAAS_FEE"));
        Assert.That(LedgerReferenceTypes.SystemSaasFeeReverse, Is.EqualTo("SYSTEM_SAAS_FEE_REVERSE"));
    }

    [Test]
    public void AssignB2bInvoice_SetsCustomerNumber_WithoutB2cReceiptStatus()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2B");
        entry.AssignB2bInvoice("INV-2026-00001");

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("INV-2026-00001"));
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(entry.LhdnValidationStatus, Is.Null);
        Assert.That(entry.TaxInvoiceId, Is.EqualTo("INV-2026-00001"));
    }

    [Test]
    public void ConvertNeedsBuyerTinToB2b_FlipsType_KeepsReceiptNumber()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AssignB2cReceipt("RCPT-2026-00009");
        entry.MarkConsolidationNotRequired();
        entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);

        entry.ConvertNeedsBuyerTinToB2b();

        Assert.That(entry.CustomerType, Is.EqualTo("B2B"));
        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("RCPT-2026-00009"));
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(entry.LhdnValidationStatus, Is.Null);
    }

    [Test]
    public void ConvertNeedsBuyerTinToB2b_RefusesOtherStatuses()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2C");
        entry.AssignB2cReceipt("RCPT-2026-00010");

        Assert.That(
            () => entry.ConvertNeedsBuyerTinToB2b(),
            Throws.InvalidOperationException.With.Message.Contains("NEEDS_BUYER_TIN"));
    }

    [Test]
    public void AssignB2bInvoice_DoesNotOverwriteExistingNumber()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx1", "sale", "B2B");
        entry.AssignB2bInvoice("INV-2026-00001");
        entry.AssignB2bInvoice("INV-2026-00099");

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("INV-2026-00001"));
    }

    [Test]
    public void AssignPlatformDocumentNumber_DoesNotStartB2cConsolidation()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.SystemSaasFee, "tx1", "hub", "B2B");
        entry.AssignPlatformDocumentNumber("SAAS-2026-00001");
        entry.MarkConsolidationNotRequired();

        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("SAAS-2026-00001"));
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(entry.LhdnValidationStatus, Is.Null);
    }
}
