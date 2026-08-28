---
number: "075"
id: PAY-SPEC-009
severity: P2
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 075 — `GET .../receipts/{id}` mapped, untested, unused

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 10
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`MapPaymentQueries` includes `GET /v1/orgs/{orgId}/receipts/{id}`. Narrower payload than list. Merchant Receipts page lists only. No NUnit method. Easy to ship a broken detail later. Honesty: a live door with no client and no test is a bug-shaped gap. pay-spec omits receipts entirely (067).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs` receipt-by-id.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs` — list only (2 tests).
- `apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`

## Reproduction

Grep tests for receipts/{id}. None.

## Blast radius

Future drill-down. G10 in 06: GET detail thinner than list.

## Suggested fix

Add member 200/404/403 tests, **or** unmap the door until a client exists. Grow pay-spec either way. Do not add PDF in this issue.

## Tests

- Missing: GET existing receipt 200; other org 403; unknown 404.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 10
- `plans/019-evals/06-rails-webhooks-fulfillment.md` §G10
- `plans/019-evals/09-tests-inventory.md` PaymentQueryTests
