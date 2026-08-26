---
number: "011"
id: B06-D01
severity: P0
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/011-quote-b2b-crm-arity
---

# 011 — B06-D01 — Quote B2B CRM arity: company name written into `IdValue`

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/011-quote-b2b-crm-arity`

Quote/session checkout now resolves CRM with named args (`Tin`, `IdType`, `IdValue`, `CompanyName`). Company name no longer lands in `IdValue`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D01 — Quote B2B CRM arity: company name written into `IdValue` (P0)

**Status at `297ba98`:** open. Same defect 008 §11 named. Not fixed.

Evidence: `InitiateCheckoutCommandHandler.cs:134–142` (quoted in §3.3). Command shape: `ResolveClientProfileCommand.cs:7–18`. Product path next to it uses named args and is correct (`198–208`).

`CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` only asserts gateway metadata `is_b2b_required=true`. It does **not** assert `ResolveClientProfileCommand` arguments. The session branch is unguarded.

Effect: type `01` either never created, or created with BRN = company name and then INVALID / retry-loop.

