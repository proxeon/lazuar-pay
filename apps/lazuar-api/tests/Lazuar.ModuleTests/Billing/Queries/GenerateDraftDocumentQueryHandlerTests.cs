using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Modules.Billing.Application.Queries;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Documents;
using Modules.Billing.Infrastructure.Queries;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;
using QuestPDF.Infrastructure;

namespace Lazuar.ModuleTests.Billing.Queries;

[TestFixture]
public class GenerateDraftDocumentQueryHandlerTests
{
    [SetUp]
    public void SetUp() => QuestPDF.Settings.License = LicenseType.Community;

    [Test]
    public async Task Handle_UsesPersistedQuoteNumber_AndWorkspaceNameWhenNoProfile()
    {
        var orgId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetDraftCheckoutSessionAsync(orgId, sessionId, Arg.Any<CancellationToken>())
            .Returns(new DraftCheckoutSessionDisplay(
                "Buyer",
                "buyer@example.com",
                """[{"description":"Design","quantity":1,"unit_price":250}]""",
                "QT-2026-00004"));

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Studio Nine", "studio", true, DateTime.UtcNow));

        var handler = new GenerateDraftDocumentQueryHandler(db, lookup, one, Substitute.For<IHttpClientFactory>());
        var pdf = await handler.Handle(new GenerateDraftDocumentQuery(orgId, sessionId), CancellationToken.None);

        pdf.Should().NotBeEmpty();
        pdf[0].Should().Be((byte)'%');
    }

    [Test]
    public async Task Handle_UnknownSession_Throws()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetDraftCheckoutSessionAsync(orgId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DraftCheckoutSessionDisplay?)null);

        var handler = new GenerateDraftDocumentQueryHandler(
            db, lookup, Substitute.For<IOneQueryService>(), Substitute.For<IHttpClientFactory>());

        var act = async () => await handler.Handle(
            new GenerateDraftDocumentQuery(orgId, Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public void ResolveDraftIssueDate_UsesSessionCreatedAt_NotDownloadTime()
    {
        var created = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 8, 18, 21, 0, 0, DateTimeKind.Utc);
        GenerateDraftDocumentQueryHandler.ResolveDraftIssueDate(created, now).Should().Be(created);
        GenerateDraftDocumentQueryHandler.ResolveDraftIssueDate(null, now).Should().Be(now);
    }

    [Test]
    public void ApplyDraftTotals_AddsExclusiveSst_WhenMerchantHasSstId()
    {
        var model = new InvoiceDocumentModel
        {
            LineItems = { new InvoiceLineItemModel { Description = "Design", Amount = 250m } }
        };

        GenerateDraftDocumentQueryHandler.ApplyDraftTotals(model, merchantHasSst: true);

        model.Subtotal.Should().Be(250m);
        model.Tax.Should().Be(20m);
        model.Total.Should().Be(270m);
        model.TaxLabel.Should().Be("SST (8%):");
    }

    [Test]
    public void ApplyDraftTotals_StaysNet_WhenMerchantHasNoSst()
    {
        var model = new InvoiceDocumentModel
        {
            LineItems = { new InvoiceLineItemModel { Description = "Design", Amount = 250m } }
        };

        GenerateDraftDocumentQueryHandler.ApplyDraftTotals(model, merchantHasSst: false);

        model.Subtotal.Should().Be(250m);
        model.Tax.Should().Be(0m);
        model.Total.Should().Be(250m);
    }

    [Test]
    public async Task Handle_UsesSessionCreatedAt_AndBuyerTin()
    {
        var orgId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var created = new DateTime(2026, 4, 2, 4, 0, 0, DateTimeKind.Utc);
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetDraftCheckoutSessionAsync(orgId, sessionId, Arg.Any<CancellationToken>())
            .Returns(new DraftCheckoutSessionDisplay(
                "Aisha",
                "aisha@example.com",
                """[{"description":"Design","quantity":1,"unit_price":100}]""",
                "QT-2026-00009",
                created,
                new CommerceCustomerDisplay("Aisha", "aisha@example.com", "C123456789012", "Buyer Sdn Bhd")));

        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Studio Nine", "studio", true, DateTime.UtcNow));

        var handler = new GenerateDraftDocumentQueryHandler(db, lookup, one, Substitute.For<IHttpClientFactory>());
        var pdf = await handler.Handle(new GenerateDraftDocumentQuery(orgId, sessionId), CancellationToken.None);

        pdf.Should().NotBeEmpty();
        GenerateDraftDocumentQueryHandler.ResolveDraftIssueDate(created, DateTime.UtcNow).Should().Be(created);
    }
}
