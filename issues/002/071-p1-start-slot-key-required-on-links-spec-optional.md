---
number: "071"
id: PAY-SPEC-005
severity: P1
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 071 — Start `slot_key` required on links; spec body optional / dist none

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 5
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Live payment-link start requires `slot_key` (8–128). TypeSpec `StartPayRequest` is optional `{ name, email }` — **no slot_key**. Dist OpenAPI historically had **no request body**. Checkout SPA is the honest client. Any spec-generated buyer client 400s on links.

Standalone checkout tokens do not need slot_key (032). Spec cannot say “optional” for both doors without a note.

## Related files

- `packages/pay-spec/main.tsp` `StartPayRequest`, `PublicPayApi.start`.
- `packages/pay-spec/dist/openapi.yaml`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **219–223**, **335–341**.
- `apps/lazuar-pay-checkout/src/App.tsx` **129–132**.

## Reproduction

POST `/v1/pay/{linkToken}/start` `{"name":"Ada"}` → 400 `slot_key`. tsp allows it.

## Blast radius

Generated buyer clients. Occupancy (001) depends on slot.

## Suggested fix

TypeSpec: document `slot_key` on body **and** query (GET already reads `slot_key`). Required for payment-link tokens. Do not remove slot_key from host or checkout SPA.

## Tests

- Host: `Start_link_without_slot_key_is_400`.
- Spec must include the field.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 5
- `plans/019-evals/03-checkout-frontend.md` G5
