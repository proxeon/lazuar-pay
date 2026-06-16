using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

/// <summary>
/// Implements the UBL 2.1 schema explicitly for Standard B2B Invoices.
/// </summary>
public class StandardInvoiceStrategy : IUblDocumentStrategy
{
    public XmlDocument Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        
        var root = doc.CreateElement("Invoice", UblNodeBuilder.InvoiceNamespace);
        root.SetAttribute("xmlns:cac", UblNodeBuilder.CacNamespace);
        root.SetAttribute("xmlns:cbc", UblNodeBuilder.CbcNamespace);
        doc.AppendChild(root);

        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "ID", request.Internal_id));
        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "IssueDate", request.Issue_date.ToString("yyyy-MM-dd")));
        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "IssueTime", request.Issue_date.ToString("HH:mm:ssZ")));

        var invoiceTypeCode = UblNodeBuilder.CreateCbcElement(doc, "InvoiceTypeCode", "01");
        invoiceTypeCode.SetAttribute("listVersionID", "1.1");
        root.AppendChild(invoiceTypeCode);

        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "DocumentCurrencyCode", "MYR"));
        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "TaxCurrencyCode", "MYR"));

        // Add Invoice Period (Mandatory for robust XSD passing)
        var invoicePeriod = doc.CreateElement("cac", "InvoicePeriod", UblNodeBuilder.CacNamespace);
        var startDate = request.Billing_period_start?.ToString("yyyy-MM-dd") ?? request.Issue_date.ToString("yyyy-MM-dd");
        var endDate = request.Billing_period_end?.ToString("yyyy-MM-dd") ?? request.Issue_date.ToString("yyyy-MM-dd");
        
        invoicePeriod.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "StartDate", startDate));
        invoicePeriod.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "EndDate", endDate));
        invoicePeriod.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "Description", "Monthly"));
        root.AppendChild(invoicePeriod);

        root.AppendChild(UblNodeBuilder.BuildEmptySignatureNode(doc));
        root.AppendChild(UblNodeBuilder.BuildSupplierParty(doc, config));
        root.AppendChild(UblNodeBuilder.BuildCustomerParty(doc, request, isB2c: false));
        root.AppendChild(UblNodeBuilder.BuildTaxTotal(doc, request.Total_excluding_tax, request.Total_tax, "06"));
        root.AppendChild(UblNodeBuilder.BuildLegalMonetaryTotal(doc, request));

        for (int i = 0; i < request.Items.Count; i++)
        {
            root.AppendChild(UblNodeBuilder.BuildInvoiceLine(doc, request.Items[i], i + 1, isB2c: false));
        }

        return doc;
    }
}
