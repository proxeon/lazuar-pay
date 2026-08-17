using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Lhdn.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class LhdnDocumentValidatedIntegrationEventHandlerTests
{
    [Test]
    public async Task Valid_MatchesCustomerDocumentNumber_UpdatesStatus_AndGeneratesPdf()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var entry = new LedgerEntry(org, LedgerReferenceTypes.GatewayPayment, "gw-tx", "sale", "B2B");
        entry.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.AssignB2bInvoice("INV-2026-00001");
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var handler = new LhdnDocumentValidatedIntegrationEventHandler(db, mediator);
        await handler.HandleAsync(new LhdnDocumentValidatedIntegrationEvent(
            org, "INV-2026-00001", "uuid-1", "VALID", "https://preprod.myinvois.hasil.gov.my/uuid-1/share/long"));

        var reloaded = await db.LedgerEntries.IgnoreQueryFilters().SingleAsync();
        Assert.That(reloaded.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.Valid));
        Assert.That(reloaded.LhdnDocumentUuid, Is.EqualTo("uuid-1"));
        await mediator.Received(1).Send(Arg.Is<GenerateAndStoreDocumentCommand>(c =>
            c.LhdnQrLink != null && c.LhdnQrLink.Contains("/share/")), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Valid_ConsolidationRef_UpdatesAllRows_DoesNotGenerateReceiptPdfs()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var a = SeedB2c(org, "a");
        var b = SeedB2c(org, "b");
        a.MarkConsolidatedPending("B2C-CONS-202607-x");
        b.MarkConsolidatedPending("B2C-CONS-202607-x");
        db.LedgerEntries.AddRange(a, b);
        await db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var handler = new LhdnDocumentValidatedIntegrationEventHandler(db, mediator);
        await handler.HandleAsync(new LhdnDocumentValidatedIntegrationEvent(
            org, "B2C-CONS-202607-x", "uuid-cons", "VALID", "https://x/share/y"));

        foreach (var row in await db.LedgerEntries.IgnoreQueryFilters().ToListAsync())
        {
            Assert.That(row.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.ConsolidatedPending));
        }

        await mediator.DidNotReceive().Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Invalid_UpdatesStatus_DoesNotGenerateDocument()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var entry = SeedB2c(org, "c");
        entry.AssignB2cReceipt("RCPT-2026-1");
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var handler = new LhdnDocumentValidatedIntegrationEventHandler(db, mediator);
        await handler.HandleAsync(new LhdnDocumentValidatedIntegrationEvent(org, "RCPT-2026-1", "", "INVALID"));

        var reloaded = await db.LedgerEntries.IgnoreQueryFilters().SingleAsync();
        Assert.That(reloaded.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.Invalid));
        await mediator.DidNotReceive().Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UnknownInternalId_DoesNotThrow()
    {
        await using var db = CreateDb();
        var handler = new LhdnDocumentValidatedIntegrationEventHandler(db, Substitute.For<IMediator>());
        await handler.HandleAsync(new LhdnDocumentValidatedIntegrationEvent(
            Guid.CreateVersion7(), "INV-MISSING", "u", "VALID"));
    }

    private static LedgerEntry SeedB2c(Guid org, string id)
    {
        var e = new LedgerEntry(org, LedgerReferenceTypes.GatewayPayment, id, "sale", "B2C");
        e.AddLine(AccountTypes.AssetCash, 50m, "MYR", 50m, "MYR");
        e.AddLine(AccountTypes.RevenueGross, -50m, "MYR", -50m, "MYR");
        return e;
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
