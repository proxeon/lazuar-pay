---
number: "214"
id: B03-C26
severity: P2
status: resolved
resolved_branch: fix/214-infer-online-gateway-from-product
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 214 — B03-C26 — `InferPaymentMethod` is “has vault id”

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/214-infer-online-gateway-from-product`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C26 — P2 — `InferPaymentMethod` is “has vault id”

No token → `MANUAL`. Unvaulted Stripe PAST_DUE does not match an ONLINE_GATEWAY-only campaign and gets **no** emails (`PastDue_EmailStep_NoMatchingCampaign_DoesNotPublish`). Default empty targets still match.

---

