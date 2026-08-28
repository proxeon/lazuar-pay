---
number: "072"
id: PAY-SPEC-006
severity: P2
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 072 — Spec says 200; host returns 201 on mint doors

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

OpenAPI / TypeSpec declare `POST /v1/checkouts` and `POST .../products` as 200. Host returns **201**. Payment-links also 201 and are missing from spec. Strict generated clients that only accept 200 fail on success. Tests lock 201.

Replay of checkout idempotency also 201 (020) — a second lie if replay should be 200.

## Related files

- `packages/pay-spec/main.tsp` / `dist/openapi.yaml`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **99**.
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` **62**.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` **102**.
- Tests: `CheckoutTests`, `CatalogTests`, `PaymentLinkTests` Created.

## Reproduction

POST product as owner. 201. tsp 200.

## Blast radius

Strict generated clients.

## Suggested fix

`@statusCode 201` on create doors. Replay 200 vs 409 is 020. Do not change host to 200 to match tsp.

## Tests

- Host already 201.
- Spec honesty scrape.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 7
