using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using Modules.Billing.Infrastructure.Documents;
using Modules.Billing.Infrastructure.Services;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;
using QuestPDF.Infrastructure;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class PlatformSaasInvoiceTests
{
    [SetUp]
    public void SetUp()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Test]
    public void Factory_SellerIsLazuar_SstZeroWithReason()
    {
        var saas = new SaasOptions
        {
            Plan = new SaasPlanOptions { Name = "Hub Starter", Interval = "mo" },
            Seller = new SaasSellerOptions
            {
                LegalName = "Lazuar",
                Tin = "",
                SstRate = 0,
                SstReason = "Supplier not SST-registered"
            }
        };

        var model = PlatformSaasInvoiceFactory.Create(
            saas,
            "SAAS-2026-00001",
            DateTime.UtcNow,
            "Acme Studio",
            "ada@example.com",
            99m,
            "MYR");

        Assert.That(model.CompanyName, Is.EqualTo("Lazuar"));
        Assert.That(model.CompanyName, Is.Not.EqualTo("Acme Studio"));
        Assert.That(model.CustomerName, Is.EqualTo("Acme Studio"));
        Assert.That(model.Tax, Is.EqualTo(0m));
        Assert.That(model.ShowZeroTax, Is.True);
        Assert.That(model.Notes, Is.EqualTo("Supplier not SST-registered"));
        Assert.That(model.DocumentType, Is.EqualTo("Invoice / payment receipt"));
        Assert.That(model.LineItems[0].Description, Does.Contain("Hub Starter"));
    }

    [Test]
    public void Factory_WithTin_UsesTaxInvoiceHeading()
    {
        var saas = new SaasOptions
        {
            Plan = new SaasPlanOptions { Name = "Hub Starter", Interval = "yr" },
            Seller = new SaasSellerOptions { LegalName = "Lazuar Sdn Bhd", Tin = "C123" }
        };

        var model = PlatformSaasInvoiceFactory.Create(saas, "SAAS-2026-00002", DateTime.UtcNow, "Buyer", "b@x.com", 10m, "MYR");
        Assert.That(model.DocumentType, Is.EqualTo("Tax invoice"));
        Assert.That(model.LineItems[0].Description, Does.Contain("year"));
    }

    [Test]
    public async Task StoreHandler_UploadsPdf_DoesNotPublishInvoiceIssued()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var entry = new LedgerEntry(orgId, LedgerReferenceTypes.SystemSaasFee, "tx_inv", "Hub Starter", "B2B");
        entry.AddLine(AccountTypes.ExpenseSoftwareSubscription, 99m, "MYR", 99m, "MYR");
        entry.AddLine(AccountTypes.AssetCash, -99m, "MYR", -99m, "MYR");
        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        entry.AssignPlatformDocumentNumber("SAAS-2026-00003");
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var r2 = Substitute.For<IR2StorageService>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        one.GetWorkspaceMembersAsync(orgId).Returns(new[]
        {
            new WorkspaceMemberSnapshotDto(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ada", "ada@acme.com", "ADMIN", DateTime.UtcNow)
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["R2_BUCKET_NAME"] = "test-bucket" })
            .Build();

        var handler = new GenerateAndStorePlatformSaasInvoiceCommandHandler(
            db,
            r2,
            one,
            eventBus,
            Microsoft.Extensions.Options.Options.Create(new SaasOptions
            {
                Plan = new SaasPlanOptions { Name = "Hub Starter", Interval = "mo" },
                Seller = new SaasSellerOptions { LegalName = "Lazuar", SstReason = "Supplier not SST-registered" }
            }),
            config);

        await handler.Handle(new GenerateAndStorePlatformSaasInvoiceCommand(orgId, entry.Id), CancellationToken.None);

        await r2.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            "test-bucket",
            Arg.Is<string>(key => key == $"vault/{orgId}/documents/{entry.Id}.pdf"),
            "application/pdf",
            Arg.Any<CancellationToken>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<InvoiceIssuedIntegrationEvent>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DocumentPublishedIntegrationEvent>());
    }

    [Test]
    public async Task StoreHandler_NullDocumentNumbers_PrintsPendingNotGuidSlice()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var entry = new LedgerEntry(orgId, LedgerReferenceTypes.SystemSaasFee, "tx_bare", "Hub Starter", "B2B");
        entry.AddLine(AccountTypes.ExpenseSoftwareSubscription, 99m, "MYR", 99m, "MYR");
        entry.AddLine(AccountTypes.AssetCash, -99m, "MYR", -99m, "MYR");
        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();

        Assert.That(entry.CustomerDocumentNumber, Is.Null);
        Assert.That(entry.TaxInvoiceId, Is.Null);

        var printed = GenerateAndStorePlatformSaasInvoiceCommandHandler.ResolvePrintedInvoiceNumber(
            entry.CustomerDocumentNumber,
            entry.TaxInvoiceId);
        Assert.That(printed, Is.EqualTo("PENDING"));
        Assert.That(printed, Is.Not.EqualTo(entry.Id.ToString()[..8].ToUpperInvariant()));

        var r2 = Substitute.For<IR2StorageService>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(orgId).Returns(new WorkspaceSnapshotDto(orgId, "Acme Studio", "acme", true, DateTime.UtcNow));
        one.GetWorkspaceMembersAsync(orgId).Returns(Array.Empty<WorkspaceMemberSnapshotDto>());

        var handler = new GenerateAndStorePlatformSaasInvoiceCommandHandler(
            db,
            r2,
            one,
            Substitute.For<IEventBus>(),
            Microsoft.Extensions.Options.Options.Create(new SaasOptions
            {
                Plan = new SaasPlanOptions { Name = "Hub Starter", Interval = "mo" },
                Seller = new SaasSellerOptions { LegalName = "Lazuar" }
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["R2_BUCKET_NAME"] = "test-bucket" })
                .Build());

        await handler.Handle(new GenerateAndStorePlatformSaasInvoiceCommand(orgId, entry.Id), CancellationToken.None);

        await r2.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            "test-bucket",
            Arg.Is<string>(key => key == $"vault/{orgId}/documents/{entry.Id}.pdf"),
            "application/pdf",
            Arg.Any<CancellationToken>());
    }
}
