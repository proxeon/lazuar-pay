using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using Modules.Commerce.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class CollectBuyerTinForLargeB2cCommandHandlerTests
{
    [Test]
    public async Task Handle_ConvertsParkedB2c_AndPublishesType01()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var entry = new LedgerEntry(orgId, LedgerReferenceTypes.GatewayPayment, "pi_large", "sale", "B2C");
        entry.AddLine(AccountTypes.AssetCash, 10800m, "MYR", 10800m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -10000m, "MYR", -10000m, "MYR");
        entry.AddLine(AccountTypes.LiabilityTaxPayable, -800m, "MYR", -800m, "MYR");
        entry.ValidateBalanced();
        entry.AssignB2cReceipt("RCPT-2026-09999");
        entry.MarkConsolidationNotRequired();
        entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var buyers = Substitute.For<ICommerceBuyerIdentity>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new CollectBuyerTinForLargeB2cCommandHandler(db, mediator, eventBus, buyers);

        await handler.Handle(new CollectBuyerTinForLargeB2cCommand(
            orgId,
            entry.Id,
            "C123456789012",
            "BRN",
            "202001012345",
            "Buyer Sdn Bhd",
            "Aisha",
            "aisha@buyer.com"), CancellationToken.None);

        var reloaded = await db.LedgerEntries.IgnoreQueryFilters().SingleAsync();
        reloaded.CustomerType.Should().Be("B2B");
        reloaded.LhdnValidationStatus.Should().BeNull();
        reloaded.CustomerDocumentNumber.Should().Be("RCPT-2026-09999");
        reloaded.ConsolidationStatus.Should().Be(ConsolidationStatuses.NotRequired);

        await buyers.Received(1).AttachTinAsync(
            orgId, "Aisha", "aisha@buyer.com", "C123456789012", "BRN", "202001012345", "Buyer Sdn Bhd", Arg.Any<CancellationToken>());
        await eventBus.Received(1).PublishAsync(Arg.Is<B2bTaxInvoiceRequestedIntegrationEvent>(e =>
            e.LedgerEntryId == entry.Id
            && e.InvoiceNumber == "RCPT-2026-09999"
            && e.AmountExcludingTax == 10000m
            && e.TaxAmount == 800m));
    }

    [Test]
    public async Task Handle_UnknownEntry_Throws()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var handler = new CollectBuyerTinForLargeB2cCommandHandler(
            db, Substitute.For<IMediator>(), Substitute.For<IEventBus>(), Substitute.For<ICommerceBuyerIdentity>());

        var act = () => handler.Handle(new CollectBuyerTinForLargeB2cCommand(
            orgId, Guid.CreateVersion7(), "C1", "BRN", "1", "Co", "A", "a@b.com"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
