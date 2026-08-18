using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application.Queries;
using Modules.Billing.Contracts;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Documents;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Queries;

public class GenerateDraftDocumentQueryHandler : IQueryHandler<GenerateDraftDocumentQuery, byte[]>
{
    private readonly BillingDbContext _dbContext;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;
    private readonly IOneQueryService _oneQueryService;
    private readonly IHttpClientFactory _httpClientFactory;

    public GenerateDraftDocumentQueryHandler(
        BillingDbContext dbContext,
        ICommerceDocumentLookup commerceDocumentLookup,
        IOneQueryService oneQueryService,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _commerceDocumentLookup = commerceDocumentLookup;
        _oneQueryService = oneQueryService;
        _httpClientFactory = httpClientFactory;
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

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.OrganizationId);
        var logoBytes = await BillingDocumentLogo.TryFetchAsync(_httpClientFactory, profile?.LogoUrl, ct);
        var quoteNumber = !string.IsNullOrWhiteSpace(sessionData.DocumentNumber)
            ? sessionData.DocumentNumber
            : DocumentSeries.CustomerFacingNumber(null, null);

        var customer = sessionData.Customer ?? new CommerceCustomerDisplay(
            sessionData.CustomerName ?? "Customer",
            sessionData.CustomerEmail ?? "");

        var model = InvoiceDocumentFactory.CreateHeader(
            "Proforma Invoice",
            quoteNumber,
            ResolveDraftIssueDate(sessionData.CreatedAt, DateTime.UtcNow),
            profile,
            workspace,
            customer,
            logoBytes);
        // Quotes are MYR-only today; do not invent FX on the draft.
        model.Currency = "MYR";
        model.LineItems = lineItems
            .Select(li => new InvoiceLineItemModel { Description = li.Description, Amount = li.UnitPrice * li.Quantity })
            .ToList();

        ApplyDraftTotals(model, MerchantHasSst(profile));

        var pdfDocument = new BaseInvoiceDocument(model);
        return pdfDocument.GeneratePdf();
    }

    internal static DateTime ResolveDraftIssueDate(DateTime? sessionCreatedAt, DateTime utcNow) =>
        sessionCreatedAt ?? utcNow;

    internal static bool MerchantHasSst(TenantBillingProfile? profile) =>
        !string.IsNullOrWhiteSpace(profile?.SstRegistrationNumber);

    /// <summary>Same exclusive 8% SST as hop-2 <c>CustomQuoteBreakdown</c> (one unit = quote net).</summary>
    internal static void ApplyDraftTotals(InvoiceDocumentModel model, bool merchantHasSst)
    {
        model.Subtotal = model.LineItems.Sum(x => x.Amount);
        if (merchantHasSst && model.Subtotal > 0)
        {
            model.Tax = Math.Round(model.Subtotal * 0.08m, 2, MidpointRounding.AwayFromZero);
            model.TaxLabel = "SST (8%):";
        }

        model.Total = model.Subtotal + model.Tax;
    }
}
