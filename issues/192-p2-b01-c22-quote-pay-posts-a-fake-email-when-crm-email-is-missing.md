---
number: "192"
id: B01-C22
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 192 — B01-C22 — Quote pay posts a fake email when CRM email is missing

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C22 — Quote pay posts a fake email when CRM email is missing

**Severity:** P2  
**One-sentence fault:** `QuoteView` sends `email: checkout.client_email || "customer@example.com"` and `name: checkout.client_name || "Customer"`.

**Evidence.** `QuoteView.tsx` 50–51. `GatewayCommon.PlaceholderEmail` is the same string on the adapter side. Custom initiate then mints hop-2 for `customer@example.com` if create-custom was given a blank that still passed CRM.

**Reproduction in words.** If the quote DTO email is empty, hop-2 is minted for the placeholder. Create-custom requires an email today, so this is a belt-and-suspenders hole. Custom initiate tests pass `buyer@example.com`. Refuse hop-2 without a real email.

---

