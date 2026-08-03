using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Infrastructure.Documents;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Commands;

public class GenerateAndStoreDocumentCommandHandler : ICommandHandler<GenerateAndStoreDocumentCommand>
{
    private readonly BillingDbContext _dbContext;
    private readonly IR2StorageService _r2Service;
    private readonly ISqlConnectionFactory _sqlFactory;
    private readonly IEventBus _eventBus;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public GenerateAndStoreDocumentCommandHandler(
        BillingDbContext dbContext,
        IR2StorageService r2Service,
        [FromKeyedServices("BillingSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _dbContext = dbContext;
        _r2Service = r2Service;
        _sqlFactory = sqlFactory;
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

        var (customerName, customerEmail) = await GetCustomerDetailsAsync(request.OrganizationId, entry.ReferenceId);

        byte[]? logoBytes = null;
        if (!string.IsNullOrEmpty(profile?.LogoUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                logoBytes = await client.GetByteArrayAsync(profile.LogoUrl, ct);
            }
            catch { /* Ignore image fetch failures to prevent blocking receipt generation */ }
        }

        var model = new InvoiceDocumentModel
        {
            DocumentType = request.DocumentType,
            InvoiceNumber = entry.TaxInvoiceId ?? entry.Id.ToString()[..8].ToUpperInvariant(),
            IssueDate = entry.Timestamp,
            CompanyName = profile?.LegalName ?? "Lazuar Merchant",
            CompanyTin = profile?.Tin ?? "N/A",
            CompanyAddress = profile?.Address?.Line1 ?? "",
            CompanyLogo = logoBytes,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            LhdnUuid = entry.LhdnValidationStatus == "VALID" ? entry.TaxInvoiceId : null, // Uses actual UUID stored in TaxInvoiceId when validated
            LhdnQrLink = request.LhdnQrLink
        };

        var revenueLines = entry.Lines.Where(l => l.AccountType == "REVENUE_GROSS" || l.AccountType == "REVENUE_RECOGNIZED").ToList();
        foreach (var line in revenueLines)
        {
            model.LineItems.Add(new InvoiceLineItemModel
            {
                Description = entry.Description ?? "Payment",
                Amount = Math.Abs(line.Amount)
            });
            model.Currency = line.Currency;
        }

        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        model.Discount = entry.Lines.Where(l => l.AccountType == "EXPENSE_DISCOUNT").Sum(l => Math.Abs(l.Amount));
        model.Tax = entry.Lines.Where(l => l.AccountType == "LIABILITY_TAX_PAYABLE").Sum(l => Math.Abs(l.Amount));
        model.Total = model.Subtotal - model.Discount + model.Tax;

        var pdfDocument = new BaseInvoiceDocument(model);
        var pdfBytes = pdfDocument.GeneratePdf();

        var storageKey = $"vault/{request.OrganizationId}/documents/{request.LedgerEntryId}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        await _r2Service.UploadAsync(stream, _bucketName, storageKey, "application/pdf", ct);

        await _eventBus.PublishAsync(new DocumentPublishedIntegrationEvent(
            request.OrganizationId,
            request.LedgerEntryId,
            request.DocumentType,
            storageKey
        ));
    }

    private async Task<(string Name, string Email)> GetCustomerDetailsAsync(Guid orgId, string referenceId)
    {
        using var connection = _sqlFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""CustomerName"", ""CustomerEmail"" 
            FROM commerce.""TransactionLogs"" 
            WHERE ""OrganizationId"" = @OrgId AND (""ExternalReference"" = @RefId OR ""Id""::text = @RefId) 
            LIMIT 1";

        var result = await connection.QuerySingleOrDefaultAsync(sql, new { OrgId = orgId, RefId = referenceId });
        
        return result != null 
            ? ((string)result.CustomerName, (string)result.CustomerEmail) 
            : ("Customer", "");
    }
}
