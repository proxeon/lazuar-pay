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
using Modules.Billing.Infrastructure.Repositories;
using Modules.Lhdn.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class LhdnDocumentCancelledIntegrationEventHandlerTests
{
    [Test]
    public async Task Cancel_AfterGatewayRefund_DoesNotPostSecondContra()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var payment = new LedgerEntry(org, LedgerReferenceTypes.GatewayPayment, "pi_b2b_1", "sale", "B2B");
        payment.AddLine(AccountTypes.AssetCash, 105m, "MYR", 105m, "MYR");
        payment.AddLine(AccountTypes.ExpenseGatewayFee, 3m, "MYR", 3m, "MYR");
        payment.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        payment.AddLine(AccountTypes.LiabilityTaxPayable, -8m, "MYR", -8m, "MYR");
        payment.AssignB2bInvoice("INV-2026-00001");
        payment.ValidateBalanced();

        var refund = new LedgerEntry(
            org,
            LedgerReferenceTypes.GatewayRefund,
            Guid.CreateVersion7().ToString("N") + ":" + Guid.CreateVersion7().ToString("N"),
            "Refund processed for subscription x (gateway tx pi_b2b_1)");
        refund.AddLine(AccountTypes.AssetCash, -108m, "MYR", -108m, "MYR");
        refund.AddLine(AccountTypes.ContraRevenueRefunds, 100m, "MYR", 100m, "MYR");
        refund.AddLine(AccountTypes.LiabilityTaxPayable, 8m, "MYR", 8m, "MYR");
        refund.ValidateBalanced();

        db.LedgerEntries.AddRange(payment, refund);
        await db.SaveChangesAsync();

        var handler = new LhdnDocumentCancelledIntegrationEventHandler(db, new LedgerRepository(db));
        await handler.HandleAsync(new LhdnDocumentCancelledIntegrationEvent(
            org, "INV-2026-00001", "uuid-cancel", "Buyer refunded"));

        Assert.That(
            await db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.LhdnCancellation),
            Is.EqualTo(0));

        var reloaded = await db.LedgerEntries.IgnoreQueryFilters()
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayPayment);
        Assert.That(reloaded.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.Cancelled));
        Assert.That(reloaded.LhdnDocumentUuid, Is.EqualTo("uuid-cancel"));
    }

    [Test]
    public async Task Cancel_WithoutRefund_PostsLhdnCancellationContra()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var payment = new LedgerEntry(org, LedgerReferenceTypes.GatewayPayment, "pi_only", "sale", "B2B");
        payment.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        payment.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        payment.AssignB2bInvoice("INV-2026-00002");
        payment.ValidateBalanced();
        db.LedgerEntries.Add(payment);
        await db.SaveChangesAsync();

        var handler = new LhdnDocumentCancelledIntegrationEventHandler(db, new LedgerRepository(db));
        await handler.HandleAsync(new LhdnDocumentCancelledIntegrationEvent(
            org, "INV-2026-00002", "uuid-2", "Wrong invoice"));

        var cancel = await db.LedgerEntries.IgnoreQueryFilters()
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.LhdnCancellation);
        Assert.That(cancel.Lines.Sum(l => l.Amount), Is.EqualTo(0m));
        Assert.That(
            cancel.Lines.Single(l => l.AccountType == AccountTypes.AssetCash).Amount,
            Is.EqualTo(-100m));
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
