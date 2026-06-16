
## References


https://github.com/allaboutevemirolive/lhdn-info
https://github.com/ERPGulf/myinvois
https://github.com/zahidaramai/MyInvoice-SDK-Middleware
https://github.com/ryzncodes/lhdn-e-invoice-guide


## 1. Fully Implemented (Standard Flow)

*   ✅ **`invoice-v1-1` (01):** Fully supported via `StandardInvoiceStrategy.cs` and `ConsolidatedInvoiceStrategy.cs`.
*   ✅ **`credit-v1-1` (02):** Supported via `CreditNoteStrategy.cs`.
*   ✅ **`debit-v1-1` (03):** Supported.
*   ✅ **`refund-v1-1` (04):** Supported.

*Architecture Note:* In our `DocumentStrategyFactory.cs`, we grouped `02`, `03`, and `04` to use the same `CreditNoteStrategy` and `CreditNote.xml` template. Because the UBL structural layout for Credit, Debit, and Refund notes is identical, the template dynamically injects `{{ doc_type_code }}` to satisfy LHDN. This was a highly efficient architectural decision.

