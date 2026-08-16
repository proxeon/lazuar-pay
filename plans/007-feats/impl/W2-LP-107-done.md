# W2-LP-107 — done

QuestPDF stationery prints the **legal** seller: logo (best-effort, including drafts), legal name, TIN, SSM, SST (omit if empty), and full registered address. Buyer TIN / company / address print when CRM has them. Missing billing profile uses the workspace name + “TIN not on file” — never **Lazuar Merchant**. Accent stays platform blue. UBL templates unchanged.

## Files

- `InvoiceDocumentModel` / `BaseInvoiceDocument` / `InvoiceDocumentFactory`
- `GenerateAndStoreDocumentCommandHandler` + `GenerateDraftDocumentQueryHandler`

## Tests run

- `InvoiceDocumentFactoryTests`, `GenerateAndStoreDocumentCommandHandlerTests`, `GenerateDraftDocumentQueryHandlerTests` — **passed**

Not committed. Not pushed.

Tracker `LP-107` can move **P → Y**.
