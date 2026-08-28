---
number: "063"
id: PAY-ONE-005
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 063 — Invalid JSON on Plane A after HMAC success is an unhandled 500

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B6
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`JsonDocument.Parse` is not in try/catch. Empty body is coerced to `"{}"` only **after** verify. Empty body **with** a valid HMAC of empty bytes would 200 `{ ok: true }` and apply nothing. Checklist O15.2 claimed empty body → 4xx. Missing signature is 401. Empty **signed** body is 200. Parse of garbage is 500.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` **22–36**.

## Reproduction

Sign body `not-json` with Pay’s dialect. 500. Sign empty bytes. 200 ok, no pause.

## Blast radius

Ops noise; One retry of a truncated body. Not a forge (HMAC still required).

## Suggested fix

try/catch parse → 400. Empty body after successful verify → 400, not 200 `{}`. Prefer envelope `id` **or** `X-Lazuar-Event-Id` as the unique key so a truncated body does not mint `Guid.NewGuid()` (`delivery` fallback today).

## Tests

- Missing: garbage body + valid HMAC is 400. Empty signed body is 400.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B6
