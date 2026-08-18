/** Same rules as PortalDocumentQueryService.Classify. */
export function classifySalesDocument(entry: {
  customer_type?: string | null;
  customer_document_number?: string | null;
  lhdn_validation_status?: string | null;
}): "Credit Note" | "Tax Invoice" | "Invoice" | "Official Receipt" {
  const number = entry.customer_document_number ?? "";
  if (number.toUpperCase().startsWith("CN-")) return "Credit Note";
  const isInvoice =
    entry.customer_type === "B2B" || number.toUpperCase().startsWith("INV-");
  if (isInvoice) {
    return entry.lhdn_validation_status?.toUpperCase() === "VALID"
      ? "Tax Invoice"
      : "Invoice";
  }
  return "Official Receipt";
}
