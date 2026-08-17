---
number: "241"
id: B05-L37
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 241 — B05-L37 — Platform invoice fallback can print a Guid slice

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L37 — P2 — Platform invoice fallback can print a Guid slice

`GenerateAndStorePlatformSaasInvoiceCommandHandler:63-65`: `CustomerDocumentNumber ?? TaxInvoiceId ?? entry.Id.ToString()[..8]`. Sequence usually ran first. If sequence returned whitespace, `AssignPlatformDocumentNumber` is skipped (`string.IsNullOrWhiteSpace` check on the refund path; SaaS always assigns if the mediator returns). Low likelihood.

---

