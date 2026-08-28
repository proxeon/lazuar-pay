---
number: "073"
id: PAY-SPEC-007
severity: P2
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 073 — Webhook spec requires `{ ok }`; live duplicate/ignored omit it

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 8
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

TypeSpec/OpenAPI webhook 200 is `{ ok: boolean }` required. Live duplicate is `{ duplicate: true }`. Ignored is `{ ignored: reason }`. Paid success is `{ ok: true }`. A generated integrator client treating 200 as `{ok:true}` is wrong. PSP adapters that only check HTTP 200 are fine. Host tests depend on duplicate/ignored shapes.

## Related files

- `packages/pay-spec/main.tsp` Webhooks `psp` return `{ ok: boolean }`.
- `packages/pay-spec/dist/openapi.yaml` required `[ok]`.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **90–93**, **96–98**, **172**.

## Reproduction

Replay a Stripe event. 200 `{ duplicate: true }` — no `ok`.

## Blast radius

Generated webhook clients. Live rails only need HTTP 200.

## Suggested fix

TypeSpec union 200 bodies. Do not change host replay to always `{ok:true}` (would hide idempotent PSP semantics).

## Tests

- Host already asserts `{ duplicate: true }` / `{ ignored }`.
- Spec union.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 8
