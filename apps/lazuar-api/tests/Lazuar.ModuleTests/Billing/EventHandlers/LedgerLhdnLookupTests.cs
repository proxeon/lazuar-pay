using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class LedgerLhdnLookupTests
{
    [Test]
    public async Task Matching_FindsLhdnDocumentUuid()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var entry = Payment(org, "pi_uuid", "INV-2026-00001");
        entry.UpdateLhdnStatus("lhdn-uuid-9", LhdnValidationStatuses.Valid);
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        var matches = await LedgerLhdnLookup.MatchingAsync(db.LedgerEntries, org, "lhdn-uuid-9");

        Assert.That(matches.Select(e => e.Id), Is.EquivalentTo(new[] { entry.Id }));
    }

    [Test]
    public async Task Matching_PrefersGatewayPayment_OverRefundWithSameTaxInvoiceId()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var payment = Payment(org, "pi_shared", "INV-2026-00002");
        var refund = new LedgerEntry(org, LedgerReferenceTypes.GatewayRefund, "re_shared", "refund", "B2B");
        refund.AddLine(AccountTypes.AssetCash, -100m, "MYR", -100m, "MYR");
        refund.AddLine(AccountTypes.ContraRevenueRefunds, 100m, "MYR", 100m, "MYR");
        refund.AssignCustomerDocumentNumber("CN-2026-00001");
        typeof(LedgerEntry).GetProperty(nameof(LedgerEntry.TaxInvoiceId))!
            .SetValue(refund, "INV-2026-00002");
        db.LedgerEntries.AddRange(payment, refund);
        await db.SaveChangesAsync();

        var matches = await LedgerLhdnLookup.MatchingAsync(db.LedgerEntries, org, "INV-2026-00002");

        Assert.That(matches.Count, Is.EqualTo(2));
        Assert.That(matches[0].ReferenceType, Is.EqualTo(LedgerReferenceTypes.GatewayPayment));
        Assert.That(matches[0].Id, Is.EqualTo(payment.Id));
    }

    private static LedgerEntry Payment(Guid org, string refId, string invoice)
    {
        var entry = new LedgerEntry(org, LedgerReferenceTypes.GatewayPayment, refId, "sale", "B2B");
        entry.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.AssignB2bInvoice(invoice);
        return entry;
    }

    private static BillingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        return new BillingDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
    }
}
