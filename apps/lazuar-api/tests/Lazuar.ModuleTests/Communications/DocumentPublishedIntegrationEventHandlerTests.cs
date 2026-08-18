using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts.Events;
using Modules.Communications.Domain;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.Messaging.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class DocumentPublishedIntegrationEventHandlerTests
{
    [Test]
    public async Task HandleAsync_EnrichedEvent_DispatchesWithSubstitutedTemplateAndDocumentLink()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var ledgerEntryId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Official Receipt")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-for-document-link-signing",
                ["App:ApiBaseUrl"] = "https://api.test/api/v1"
            })
            .Build();

        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            ledgerEntryId,
            DocumentType: "Official Receipt",
            StoragePath: $"vault/{orgId}/documents/{ledgerEntryId}.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme Studio",
            CustomerName: "Aisha Merchant",
            CustomerEmail: "aisha@example.com"));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.OrganizationId == orgId
            && e.ToEmail == "aisha@example.com"
            && e.Subject.Contains("Acme Studio")
            && e.Subject.Contains("{{business_name}}") == false
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Aisha Merchant")
            && e.HtmlEmailBody.Contains("{{customer_name}}") == false
            && e.HtmlEmailBody.Contains("Acme Studio")
            && e.HtmlEmailBody.Contains($"https://api.test/api/v1/public/billing/acme/documents/{ledgerEntryId}")
            && e.HtmlEmailBody.Contains("sig=")
            && e.HtmlEmailBody.Contains("{{document_link}}") == false
            && e.PlainTextPhoneBody != null
            && e.PlainTextPhoneBody.Contains("Aisha Merchant")
            && e.Channel == "ALL"));
    }

    [Test]
    public async Task HandleAsync_DraftQuotation_UsesQuotationReadyTemplate()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var ledgerEntryId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Quotation Ready")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-for-document-link-signing",
                ["App:ApiBaseUrl"] = "https://api.test/api/v1"
            })
            .Build();

        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            ledgerEntryId,
            DocumentType: "Draft Quotation",
            StoragePath: "vault/x.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme Studio",
            CustomerName: "Buyer",
            CustomerEmail: "buyer@example.com"));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "buyer@example.com"
            && e.Subject.Contains("quotation", StringComparison.OrdinalIgnoreCase)
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("Buyer")));
    }

    [Test]
    public async Task HandleAsync_MissingCustomerEmail_DoesNotDispatch()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Official Receipt")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder().Build();
        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            DocumentType: "Official Receipt",
            StoragePath: "vault/x.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme",
            CustomerName: "No Email",
            CustomerEmail: ""));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_MissingTenantSlug_DoesNotDispatch()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Official Receipt")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder().Build();
        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            DocumentType: "Official Receipt",
            StoragePath: "vault/x.pdf",
            TenantSlug: "",
            BusinessName: "Acme",
            CustomerName: "Buyer",
            CustomerEmail: "buyer@example.com"));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task DocumentPublished_TaxInvoice_DoesNotFallBackToOfficialReceipt()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Official Receipt")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-for-document-link-signing",
                ["App:ApiBaseUrl"] = "https://api.test/api/v1"
            })
            .Build();

        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            DocumentType: "Tax Invoice",
            StoragePath: "vault/x.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme",
            CustomerName: "Buyer",
            CustomerEmail: "buyer@example.com"));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task DocumentPublished_TaxInvoice_UsesTaxInvoiceTemplate()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Tax Invoice")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-for-document-link-signing",
                ["App:ApiBaseUrl"] = "https://api.test/api/v1"
            })
            .Build();

        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            DocumentType: "Tax Invoice",
            StoragePath: "vault/x.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme",
            CustomerName: "Buyer",
            CustomerEmail: "buyer@example.com"));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.ToEmail == "buyer@example.com"
            && e.Subject.Contains("tax invoice", StringComparison.OrdinalIgnoreCase)
            && e.HtmlEmailBody != null
            && e.HtmlEmailBody.Contains("tax invoice", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task DocumentPublished_ProformaInvoice_UsesQuotationReadyTemplate()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var def = DefaultMessageTemplates.GetByName("Quotation Ready")!;
        db.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(orgId, def));
        await db.SaveChangesAsync();

        var eventBus = Substitute.For<IEventBus>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-for-document-link-signing",
                ["App:ApiBaseUrl"] = "https://api.test/api/v1"
            })
            .Build();

        var handler = new DocumentPublishedIntegrationEventHandler(db, config, eventBus);

        await handler.HandleAsync(new DocumentPublishedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            DocumentType: "Proforma Invoice",
            StoragePath: "vault/x.pdf",
            TenantSlug: "acme",
            BusinessName: "Acme Studio",
            CustomerName: "Buyer",
            CustomerEmail: "buyer@example.com"));

        await eventBus.Received(1).PublishAsync(Arg.Is<DispatchMessageIntegrationEvent>(e =>
            e.Subject.Contains("quotation", StringComparison.OrdinalIgnoreCase)));
    }

    private static CommunicationsDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
