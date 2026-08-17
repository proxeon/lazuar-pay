---
number: "103"
id: B06-D17
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 103 — B06-D17 — Submit without credits: deduct-after-persist is fail-open

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D17 — Submit without credits: deduct-after-persist is fail-open (P1)

**Status:** open as a money/compliance pair.

Pre-check `HasSufficientCreditsAsync` (`SubmitTaxDocumentCommand.cs:77–83`) then persist `TaxDocument` then deduct (`152–169`). If deduct throws, the document is already PENDING and the worker will submit. Comment says this is intentional. A tenant at 0 credits who races two submits, or whose deduct fails, **files MyInvois for free**.

`LhdnDocumentSubmittedIntegrationEventHandler` correctly does **not** deduct again (that double-charge was fixed). `LhdnSingleCreditPathTests` asserts the deduct call happens; it does **not** assert behaviour when deduct fails.

Test mode (`IExecutionContextAccessor.IsTestMode`) skips metering entirely (`74–76`). Sandbox scripts that hit the API with test-mode on do not prove the credit gate.

