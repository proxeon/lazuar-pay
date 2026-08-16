using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;
using QuestPDF.Infrastructure;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class GenerateAndStoreDocumentCommandHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Test]
    public async Task GenerateAndStore_PublishesDocumentPublished_WithCustomerEmail()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var entry = SeedReceipt(db, orgId, "gw_txn_1");
        await db.SaveChangesAsync();

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetCustomerForDocumentAsync(orgId, entry.ReferenceId, "session-or-sub", Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Aisha Merchant", "aisha@example.com"));

        var eventBus = Substitute.For<IEventBus>();
        var r2 = Substitute.For<IR2StorageService>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));

        var handler = CreateHandler(db, lookup, eventBus, r2, one);

        await handler.Handle(new GenerateAndStoreDocumentCommand(
            orgId, entry.Id, "Official Receipt", CorrelationId: "session-or-sub"), CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<DocumentPublishedIntegrationEvent>(e =>
            e.OrganizationId == orgId
            && e.LedgerEntryId == entry.Id
            && e.DocumentType == "Official Receipt"
            && e.CustomerEmail == "aisha@example.com"
            && e.CustomerName == "Aisha Merchant"
            && e.TenantSlug == "acme"
            && !string.IsNullOrWhiteSpace(e.StoragePath)));

        await r2.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Is<string>(key => key.Contains(entry.Id.ToString())),
            "application/pdf",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateAndStore_StillPublishes_WhenEmailEmpty()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var entry = SeedReceipt(db, orgId, "gw_txn_2");
        await db.SaveChangesAsync();

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetCustomerForDocumentAsync(orgId, entry.ReferenceId, null, Arg.Any<CancellationToken>())
            .Returns((CommerceCustomerDisplay?)null);

        var eventBus = Substitute.For<IEventBus>();
        var r2 = Substitute.For<IR2StorageService>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Acme", "acme", true, DateTime.UtcNow));

        var handler = CreateHandler(db, lookup, eventBus, r2, one);

        await handler.Handle(new GenerateAndStoreDocumentCommand(orgId, entry.Id, "Official Receipt"), CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<DocumentPublishedIntegrationEvent>(e =>
            e.CustomerEmail == ""
            && e.CustomerName == "Customer"
            && !string.IsNullOrWhiteSpace(e.StoragePath)));
        await r2.Received(1).UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), "application/pdf", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateAndStore_UsesWorkspaceName_WhenBillingProfileMissing()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var entry = SeedReceipt(db, orgId, "gw_txn_3");
        await db.SaveChangesAsync();

        var lookup = Substitute.For<ICommerceDocumentLookup>();
        lookup.GetCustomerForDocumentAsync(orgId, entry.ReferenceId, null, Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer", "buyer@example.com"));

        var eventBus = Substitute.For<IEventBus>();
        var r2 = Substitute.For<IR2StorageService>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Studio Nine", "studio", true, DateTime.UtcNow));

        var handler = CreateHandler(db, lookup, eventBus, r2, one);
        await handler.Handle(new GenerateAndStoreDocumentCommand(orgId, entry.Id, "Official Receipt"), CancellationToken.None);

        await r2.Received(1).UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), "application/pdf", Arg.Any<CancellationToken>());
        await eventBus.Received(1).PublishAsync(Arg.Is<DocumentPublishedIntegrationEvent>(e =>
            e.BusinessName == "Studio Nine"));
    }

    private static GenerateAndStoreDocumentCommandHandler CreateHandler(
        BillingDbContext db,
        ICommerceDocumentLookup lookup,
        IEventBus eventBus,
        IR2StorageService r2,
        IOneQueryService one)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["R2_BUCKET_NAME"] = "test-bucket" })
            .Build();

        return new GenerateAndStoreDocumentCommandHandler(
            db,
            r2,
            lookup,
            one,
            eventBus,
            Substitute.For<IHttpClientFactory>(),
            config);
    }

    private static BillingDbContext CreateDb(Guid orgId) =>
        new(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

    private static LedgerEntry SeedReceipt(BillingDbContext db, Guid orgId, string referenceId)
    {
        var entry = new LedgerEntry(orgId, LedgerReferenceTypes.GatewayPayment, referenceId, "Test pay", "B2C");
        entry.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.AssignB2cReceipt("RCPT-TEST");
        db.LedgerEntries.Add(entry);
        return entry;
    }
}
