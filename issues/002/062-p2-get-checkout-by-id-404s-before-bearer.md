---
number: "062"
id: PAY-ONE-004
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 062 — `GET /v1/checkouts/{id}` 404s before Bearer (existence oracle)

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Unknown id → 404 and **skips One** even with a Bearer (`Get_unknown_is_404`). Missing Bearer on a **known** id → 401 after the row lookup. Missing Bearer on unknown → 404. That is an existence oracle on checkout ids.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **102–122**.
- Whoami/checkout tests: unknown 404.

## Reproduction

`GET /v1/checkouts/dead` no Bearer → 404. `GET` a real id no Bearer → 401.

## Blast radius

Enumerate whether a GUID exists. Checkout ids are 32-hex, not guess-friendly. Still an oracle.

## Suggested fix

Require Bearer first (401 without looking up), then 404 if missing **or** not a member (do not distinguish). Same pattern as org-scoped lists.

## Tests

- Existing: unknown is 404 (locks the oracle).
- After fix: missing Bearer always 401; unknown with Bearer 404; other-org 403/404 one spelling.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B4
