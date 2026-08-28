---
number: "070"
id: PAY-SPEC-004
severity: P1
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 070 — TypeSpec `CreateCheckoutRequest` omits required `provider`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Live `POST /v1/checkouts` requires `provider` (unknown → 400). README curl includes it (honest). TypeSpec `CreateCheckoutRequest` has org_id, amount, currency, urls, idempotency_key — **no provider**. A client generated from tsp would 400 `"unknown provider"`. Payment-links (the UI door) are missing from tsp entirely.

## Related files

- `packages/pay-spec/main.tsp` `CreateCheckoutRequest`.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **53–55**.
- `apps/lazuar-pay/README.md` curl example.

## Reproduction

POST checkout without provider → 400. tsp allows it.

## Blast radius

Kernel curl-from-spec. Merchant uses payment-links (also unspecified).

## Suggested fix

TypeSpec: `provider` required (or default documented — host has **no** default). `@statusCode 201`. Grow payment-links. Do not stop returning 201; tests lock it (072).

## Tests

- Host: unknown provider 400 already.
- Spec must list provider.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 4
- `plans/019-evals/01-pay-host-seams.md` G2 README curl is the old door
