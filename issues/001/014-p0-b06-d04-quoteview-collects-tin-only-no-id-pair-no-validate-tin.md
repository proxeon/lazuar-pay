---
number: "014"
id: B06-D04
severity: P0
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/014-quoteview-id-pair-validate-tin
---

# 014 — B06-D04 — QuoteView collects TIN only; no ID pair; no `validate-tin`

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/014-quoteview-id-pair-validate-tin`

Quote checkout collects company name, TIN, ID type/value and calls `validate-tin`. Session hop-1 requires the same pair as product checkout.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D04 — QuoteView collects TIN only; no ID pair; no `validate-tin` (P0)

**Status:** open.

```36:55:apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx
    if (checkout.is_b2b_required && !taxId.trim()) {
      setGlobalError("Company tax ID (TIN) is required for this payment request.");
      return;
    }
    ...
        company_name: checkout.is_b2b_required ? companyName.trim() || undefined : undefined,
        tax_id: checkout.is_b2b_required ? taxId.trim() : undefined,
```

Company name is optional. ID type/value are absent. `CheckoutForm.tsx:96–110` and `227–252` do the opposite on the product path.

Backend session branch does not require `IdType`/`IdValue` the way `EnforceCheckoutConfiguration` does for products (`425–428`).

