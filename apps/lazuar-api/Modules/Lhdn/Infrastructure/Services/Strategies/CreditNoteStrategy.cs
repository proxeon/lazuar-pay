using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

/// <summary>
/// Implements the UBL 2.1 schema for LHDN Credit Notes (02).
/// Enforces the mandatory InvoiceDocumentReference to link the adjustment to the original UUID.
/// </summary>
public class CreditNoteStrategy : IUblDocumentStrategy
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

        var invoiceTypeCode = UblNodeBuilder.CreateCbcElement(doc, "InvoiceTypeCode", "02");
        invoiceTypeCode.SetAttribute("listVersionID", "1.1");
        root.AppendChild(invoiceTypeCode);

        if (!string.IsNullOrWhiteSpace(request.Adjustment_reason))
        {
            root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "InstructionNote", request.Adjustment_reason));
        }

        root.AppendChild(UblNodeBuilder.CreateCbcElement(doc, "DocumentCurrencyCode", "MYR"));

        if (!string.IsNullOrWhiteSpace(request.Original_lhdn_uuid))
        {
            var originalInternalId = request.Internal_id.Replace("CN-", "");
            root.AppendChild(UblNodeBuilder.BuildInvoiceDocumentReference(doc, originalInternalId, request.Original_lhdn_uuid));
        }

        root.AppendChild(UblNodeBuilder.BuildEmptySignatureNode(doc));
        root.AppendChild(UblNodeBuilder.BuildSupplierParty(doc, config));
        
        bool isB2c = string.IsNullOrWhiteSpace(request.Buyer_tin) || request.Buyer_tin == UblNodeBuilder.GeneralPublicTin;
        root.AppendChild(UblNodeBuilder.BuildCustomerParty(doc, request, isB2c));
        
        root.AppendChild(UblNodeBuilder.BuildTaxTotal(doc, request.Total_excluding_tax, request.Total_tax, "06"));
        root.AppendChild(UblNodeBuilder.BuildLegalMonetaryTotal(doc, request));

        for (int i = 0; i < request.Items.Count; i++)
        {
            root.AppendChild(UblNodeBuilder.BuildInvoiceLine(doc, request.Items[i], i + 1, isB2c));
        }

        return doc;
    }
}
