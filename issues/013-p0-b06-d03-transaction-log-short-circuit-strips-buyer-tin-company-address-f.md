---
number: "013"
id: B06-D03
severity: P0
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 013 — B06-D03 — Transaction-log short-circuit strips buyer TIN / company / address from the PDF

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D03 — Transaction-log short-circuit strips buyer TIN / company / address from the PDF (P0)

**Status:** open. Same as 008 §4.4.

```92:102:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
    public async Task<CommerceCustomerDisplay?> GetCustomerForDocumentAsync(...)
    {
        var fromLog = await FindCustomerOnTransactionLogAsync(organizationId, referenceId, ct);
        if (fromLog != null && !string.IsNullOrWhiteSpace(fromLog.Email))
        {
            return fromLog;
        }
```

```157:161:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        return new CommerceCustomerDisplay(
            string.IsNullOrWhiteSpace(log.CustomerName) ? "Customer" : log.CustomerName,
            log.CustomerEmail ?? "",
            null,
            null);
```

The interface comment even documents the preference (`ICommerceDocumentLookup.cs:31–33`: “Prefers an existing transaction log email”). After a real pay, the log almost always exists. Therefore the Tax Invoice PDF’s “Billed To” is typically the **person name from checkout**, with **no buyer TIN**.

Lhdn submit does a second CRM-by-email lookup (`B2bTaxInvoiceRequestedIntegrationEventHandler.cs:42–46`). MyInvois can have a real buyer while the customer PDF does not. That is worse than “we have no e-invoice.”

`FromCrmAsync` (`201–213`) **would** deliver TIN, company, address, id type/value. The short-circuit never reaches it on the common path.

