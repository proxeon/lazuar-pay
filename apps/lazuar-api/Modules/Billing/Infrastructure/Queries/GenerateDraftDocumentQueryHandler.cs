using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Application.Queries;
using Modules.Billing.Infrastructure.Documents;
using QuestPDF.Fluent;

namespace Modules.Billing.Infrastructure.Queries;

public class GenerateDraftDocumentQueryHandler : IQueryHandler<GenerateDraftDocumentQuery, byte[]>
{
    private readonly BillingDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlFactory;

    public GenerateDraftDocumentQueryHandler(
        BillingDbContext dbContext,
        [FromKeyedServices("BillingSqlConnectionFactory")] ISqlConnectionFactory sqlFactory)
    {
        _dbContext = dbContext;
        _sqlFactory = sqlFactory;
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

        using var connection = _sqlFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        
        var sessionSql = @"
            SELECT c.""ClientProfileId"", c.""AdHocLineItems"", cp.""FullName"" AS CustomerName, cp.""Email"" AS CustomerEmail
            FROM commerce.""CheckoutSessions"" c
            LEFT JOIN crm.""ClientProfiles"" cp ON c.""ClientProfileId"" = cp.""Id""
            WHERE c.""Id"" = @SessionId AND c.""OrganizationId"" = @OrgId LIMIT 1";

        var sessionData = await connection.QuerySingleOrDefaultAsync(sessionSql, new { SessionId = request.SessionId, OrgId = request.OrganizationId });

        if (sessionData == null) throw new InvalidOperationException("Custom checkout session not found.");

        var lineItemsJson = (string)sessionData.AdHocLineItems;
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
