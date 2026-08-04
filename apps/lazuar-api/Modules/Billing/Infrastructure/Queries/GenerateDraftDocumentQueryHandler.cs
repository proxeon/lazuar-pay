using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application.Queries;
using Modules.Billing.Infrastructure.Documents;
using Modules.Commerce.Contracts;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Queries;

public class GenerateDraftDocumentQueryHandler : IQueryHandler<GenerateDraftDocumentQuery, byte[]>
{
    private readonly BillingDbContext _dbContext;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;

    public GenerateDraftDocumentQueryHandler(
        BillingDbContext dbContext,
        ICommerceDocumentLookup commerceDocumentLookup)
    {
        _dbContext = dbContext;
        _commerceDocumentLookup = commerceDocumentLookup;
    }

    private class AdHocLineItemStub
    {
        public string Description { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public async Task<byte[]> Handle(GenerateDraftDocumentQuery request, CancellationToken ct)
    {
        var profile = await _dbContext.TenantBillingProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId, ct);

        var sessionData = await _commerceDocumentLookup.GetDraftCheckoutSessionAsync(
            request.OrganizationId, request.SessionId, ct);

        if (sessionData == null) throw new InvalidOperationException("Custom checkout session not found.");

        var lineItemsJson = sessionData.AdHocLineItemsJson;
        var lineItems = string.IsNullOrWhiteSpace(lineItemsJson)
            ? new List<AdHocLineItemStub>()
            : JsonSerializer.Deserialize<List<AdHocLineItemStub>>(lineItemsJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new List<AdHocLineItemStub>();

        var model = new InvoiceDocumentModel
        {
            DocumentType = "Proforma Invoice",
            InvoiceNumber = $"QUOTE-{request.SessionId.ToString()[..8].ToUpperInvariant()}",
            IssueDate = DateTime.UtcNow,
            CompanyName = profile?.LegalName ?? "Lazuar Merchant",
            CompanyTin = profile?.Tin ?? "N/A",
            CompanyAddress = profile?.Address?.Line1 ?? "",
            CustomerName = sessionData.CustomerName ?? "Customer",
            CustomerEmail = sessionData.CustomerEmail ?? "",
            Currency = "MYR",
            LineItems = lineItems.Select(li => new InvoiceLineItemModel { Description = li.Description, Amount = li.UnitPrice * li.Quantity }).ToList()
        };

        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        model.Total = model.Subtotal;

        var pdfDocument = new BaseInvoiceDocument(model);
        return pdfDocument.GeneratePdf();
    }
}
