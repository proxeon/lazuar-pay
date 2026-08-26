---
number: "060"
id: B04-P03
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/060-chip-recurring-token
---

# 060 — B04-P03 — CHIP off-session: `tokenId` used as a purchase id; `recurring_token` may not be one

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/060-chip-recurring-token`

CHIP off-session no longer treats a 404 GET /purchases/{token} as fatal. It falls back to the client record. Recurring fallback uses the nested purchase id.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P03 — P1 — CHIP off-session: `tokenId` used as a purchase id; `recurring_token` may not be one

**Where.** `ExtractVaultIds` prefers `recurring_token` (`392-396`). `ChargeOffSessionAsync` `GET /purchases/{tokenId}/` (`242`). Test `ExtractVaultIds_PurchaseNodeTokenAndClient_FallsBackCustomerToToken` sets token `tok_from_purchase`.

**What.** If CHIP’s `recurring_token` is a distinct token string, GET 404s, charge returns false, Billing publishes `charge_declined` (via the off-session handler) even though a valid token exists. The charge API itself wants `{ recurring_token }` — that part is right. The GET-to-clone-brand step is the broken assumption.

`ExtractVaultIds` also uses **root** `id` for the is-recurring fallback, not `ReadStablePurchaseId`. Nested-vs-root disagreement splits `GatewayTransactionId` and `GatewayTokenId`.

