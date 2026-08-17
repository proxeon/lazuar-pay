---
number: "251"
id: B06-D32
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 251 — B06-D32 — Large B2C `NEEDS_BUYER_TIN` has no resolution product

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D32 — Large B2C `NEEDS_BUYER_TIN` has no resolution product (P2)

Pay-time (`GatewayPaymentCompletedHandler.cs:94–98`) and the cons job (`B2cConsolidationJob.cs:225–230`) both park above-threshold B2C as `NOT_REQUIRED` / `NEEDS_BUYER_TIN`. There is no flow that then collects a TIN. They sit in ops forever. Honesty of the badge is fine. Completeness of the product is not.

