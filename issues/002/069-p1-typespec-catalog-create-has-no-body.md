---
number: "069"
id: PAY-SPEC-003
severity: P1
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 069 — TypeSpec catalog create has no body; host requires name+amount

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 3
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

TypeSpec `Catalog.createProduct(@path orgId)` has no body. A generated client would POST empty. Host returns 400 `"name is required"` / `"amount must be greater than 0"`. Merchant happens to send the right JSON because it is hand-written. Spec bug (invalid contract), not a host bug.

## Related files

- `packages/pay-spec/main.tsp` Catalog interface.
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` **26–41**, **88–95**.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **161–166**.

## Reproduction

POST `/v1/orgs/{id}/products` `{}` → 400. tsp says no body.

## Blast radius

Generated clients. 023 still applies to money vs label.

## Suggested fix

TypeSpec: add `CreateProductRequest` and 201 body with `price_id`. Do not make host accept empty POST.

## Tests

- Host already 400s empty. Missing: spec compile + honesty scrape.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 3
