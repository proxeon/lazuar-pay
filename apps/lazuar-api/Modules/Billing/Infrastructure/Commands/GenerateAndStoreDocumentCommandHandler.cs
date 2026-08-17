using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Infrastructure.Documents;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Commands;

public class GenerateAndStoreDocumentCommandHandler : ICommandHandler<GenerateAndStoreDocumentCommand>
{
    private readonly BillingDbContext _dbContext;
    private readonly IR2StorageService _r2Service;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;
    private readonly IOneQueryService _oneQueryService;
    private readonly IEventBus _eventBus;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public GenerateAndStoreDocumentCommandHandler(
        BillingDbContext dbContext,
        IR2StorageService r2Service,
        ICommerceDocumentLookup commerceDocumentLookup,
        IOneQueryService oneQueryService,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _dbContext = dbContext;
        _r2Service = r2Service;
        _commerceDocumentLookup = commerceDocumentLookup;
        _oneQueryService = oneQueryService;
        _eventBus = eventBus;
        _httpClientFactory = httpClientFactory;
        _bucketName = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
    }

    public async Task Handle(GenerateAndStoreDocumentCommand request, CancellationToken ct)
    {
        var entry = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.LedgerEntryId && e.OrganizationId == request.OrganizationId, ct);

        if (entry == null) throw new InvalidOperationException("Ledger entry not found.");

        var profile = await _dbContext.TenantBillingProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId, ct);

        var customer = await _commerceDocumentLookup.GetCustomerForDocumentAsync(
            request.OrganizationId, entry.ReferenceId, request.CorrelationId, ct);
        var customerName = customer?.Name ?? "Customer";
        var customerEmail = customer?.Email ?? "";

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.OrganizationId);
        var tenantSlug = workspace?.Slug ?? "";
        var businessName = workspace?.Name ?? profile?.LegalName ?? "Business";

        var logoBytes = await BillingDocumentLogo.TryFetchAsync(_httpClientFactory, profile?.LogoUrl, ct);

        var model = InvoiceDocumentFactory.CreateHeader(
            request.DocumentType,
            DocumentSeries.CustomerFacingNumber(entry.CustomerDocumentNumber, entry.TaxInvoiceId),
            entry.Timestamp,
            profile,
            workspace,
            customer,
            logoBytes,
            entry.LhdnValidationStatus == LhdnValidationStatuses.Valid
                ? (entry.LhdnDocumentUuid ?? entry.TaxInvoiceId)
                : null,
            request.LhdnQrLink);

        var isCreditNote = string.Equals(request.DocumentType, "Credit Note", StringComparison.OrdinalIgnoreCase);
        var sourceLines = entry.Lines.Where(l =>
            l.AccountType == AccountTypes.RevenueGross
            || l.AccountType == AccountTypes.RevenueRecognized
            || (isCreditNote && l.AccountType == AccountTypes.ContraRevenueRefunds)).ToList();
        foreach (var line in sourceLines)
        {
            model.LineItems.Add(new InvoiceLineItemModel
            {
                Description = isCreditNote ? "Refund" : entry.Description ?? "Payment",
                Amount = Math.Abs(line.Amount)
            });
            model.Currency = line.Currency;
        }

        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        model.Discount = entry.Lines.Where(l => l.AccountType == AccountTypes.ExpenseDiscount).Sum(l => Math.Abs(l.Amount));
        model.Tax = entry.Lines.Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable).Sum(l => Math.Abs(l.Amount));
        model.Total = model.Subtotal - model.Discount + model.Tax;
        if (entry.Lines.Any(l => l.TaxTypeCode == "02") || !string.IsNullOrWhiteSpace(profile?.SstRegistrationNumber) && model.Tax > 0)
        {
            model.TaxLabel = "SST:";
        }

        var pdfDocument = new BaseInvoiceDocument(model);
        var pdfBytes = pdfDocument.GeneratePdf();

        var storageKey = $"vault/{request.OrganizationId}/documents/{request.LedgerEntryId}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        await _r2Service.UploadAsync(stream, _bucketName, storageKey, "application/pdf", ct);

        await _eventBus.PublishAsync(new DocumentPublishedIntegrationEvent(
            request.OrganizationId,
            request.LedgerEntryId,
            request.DocumentType,
            storageKey,
            tenantSlug,
            businessName,
            customerName,
            customerEmail
        ));
    }
}
