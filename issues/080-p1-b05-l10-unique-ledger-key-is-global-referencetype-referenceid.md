---
number: "080"
id: B05-L10
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 080 — B05-L10 — Unique ledger key is global `(ReferenceType, ReferenceId)`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L10 — P1 — Unique ledger key is global `(ReferenceType, ReferenceId)`

`BillingDbContext.cs:66`. No `OrganizationId`. Fine for Stripe PaymentIntent ids (globally unique per account). Wrong grain for anything we mint (`MANUAL_ENROLLMENT` uses a Guid so it is safe; a reused Billplz bill id across two tenants on the same Billplz collection is the theoretical collision). Second org fails the insert; inbox dead-letters. Not a silent steal, but a stuck journal.

---

