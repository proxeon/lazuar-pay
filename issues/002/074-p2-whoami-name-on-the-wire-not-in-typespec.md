---
number: "074"
id: PAY-SPEC-008
severity: P2
status: resolved
source: plans/019-evals/08-contracts-spec-honesty.md
head: "9f04ad58"
---

# 074 — Whoami `name` is on the wire, not in TypeSpec

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/08-contracts-spec-honesty.md` bugs 9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`OneMeMapper` maps One `name` onto Pay whoami. Merchant sidebar `staffDisplay` uses it (not Zitadel sub). TypeSpec `WhoamiResponse` has user_id, email, is_platform_admin, active_org_id, tenants — **no name**. Not a runtime break (optional JSON). It is a contract bug if anyone generates `WhoamiResponse` from tsp and assumes name comes only from OIDC.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `packages/pay-spec/main.tsp` `WhoamiResponse`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`

## Reproduction

GET `/v1/whoami`. JSON has `name`. tsp model does not.

## Blast radius

Generated types drop the field; sidebar would fall back to email/sub if it used generated types.

## Suggested fix

TypeSpec add `name?: string`. Do not drop mapping.

## Tests

- Whoami tests may already serialize name from Fake One.
- Spec field.

## Source reports

- `plans/019-evals/08-contracts-spec-honesty.md` §Bugs 9
- `plans/019-evals/07-identity-authz-cors.md` §G9
