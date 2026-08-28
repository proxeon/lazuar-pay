---
number: "068"
id: PAY-SPEC-002
severity: P1
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 068 — `GET /gateway` without `?provider=` returns the list envelope

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 2
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Same path, two shapes. Empty-query singular `GET /v1/orgs/{orgId}/gateway` aliases `List` (`{ org_id, processors }`). TypeSpec says `GatewayView`. A generated client would parse the list as a single gateway and lose every rail. Host chose aliasing after adding `/gateways`. Spec was not updated. SPA uses `/gateways`.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` **16–18**, **158–160**.
- `packages/pay-spec/main.tsp` Gateways interface — singular only.
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **46** — `/gateways`.

## Reproduction

`GET /v1/orgs/{id}/gateway` (no query) with member Bearer. JSON is the processors envelope, not `GatewayView`.

## Blast radius

Spec-generated staff clients. Live SPA is fine.

## Suggested fix

Prefer **host** stop aliasing (singular GET without provider → 400/404) **or** **spec** documents “no provider ⇒ list envelope.” Lean: `/gateways` is list; singular requires `provider`. Do not make SPA call singular.

## Tests

- Existing: GET list / GET ?provider=
- Missing: GET singular without provider is 400 **or** documented list (lock the chosen shape).

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 2
- `plans/019-evals/04-processors-vault-test.md` GET `/gateway` vs `/gateways`
