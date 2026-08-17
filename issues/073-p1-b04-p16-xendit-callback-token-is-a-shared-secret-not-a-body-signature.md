---
number: "073"
id: B04-P16
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/073-xendit-callback-constant-time
---

# 073 — B04-P16 — Xendit callback token is a shared secret, not a body signature

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/073-xendit-callback-constant-time`

Callback token compare is SHA-256 then constant-time, including length mismatch. Xendit still has no body HMAC — that is the processor.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P16 — P1 — Xendit callback token is a shared secret, not a body signature

**Where.** `VerifyCallbackToken` (`240-256`). No HMAC of `rawBody`. No timestamp.

**What.** Stolen token + any JSON with `status=PAID` and a new `id` is `PAYMENT_COMPLETED`. Length-mismatch compare is not constant-time. Same class of integration as many Xendit docs; still a Payments-layer fact.

