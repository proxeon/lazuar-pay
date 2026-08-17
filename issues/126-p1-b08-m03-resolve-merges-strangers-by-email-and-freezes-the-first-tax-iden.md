---
number: "126"
id: B08-M03
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 126 — B08-M03 — Resolve merges strangers by email and freezes the first tax identity

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M03 — P1 — Resolve merges strangers by email and freezes the first tax identity

**Where:** `ResolveClientProfileCommandHandler.cs` 26–79; unique index `ClientProfileConfiguration.cs` 15; `LhdnBuyerMapper.cs` 44–61.

**What:** Identity key is normalized email only. Enrichment is blank-fill-only. FullName never moves. Two people, one shared inbox (`accounts@`, a family Gmail, a typo) share TIN, NRIC/BRN, address, and every dunning/receipt mail greeting.

**Why it matters:** PDPA / LHDN. The first buyer’s NRIC on the second buyer’s invoice is not “CRM convenience.”

**Not the same as:** Stripe-style customer-by-email when the product is a single-buyer SaaS seat. This product sells B2B tax invoices off the same row.

---

