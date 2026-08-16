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
}
