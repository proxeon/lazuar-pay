---
number: "078"
id: PAY-ONE-008
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 078 — `/v1/orgs/{id}/ready` is still dummy `ready: true`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` G2 (also `10-honesty-bugs-gaps.md` P1-11)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

After `RequireMemberAsync`, the door always returns `{ org_id, ready: true }`. It does not read vault, catalog, `charges_paused`, or Test. Merchant SPA **never calls this route**. Org chrome uses whoami + `tenants.find`. TypeSpec documents “Dummy admin.” Honest as a member ping; a lie if anyone treats it as “this shop can take money.”

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `packages/pay-spec/main.tsp` Orgs.ready

## Reproduction

Member GET ready on an org with no keys. `ready: true`.

## Blast radius

Kernel probes. Merchant unaffected.

## Suggested fix

Keep dummy and **say so** in spec/README, **or** compute ready from “at least one vault or Test allowed.” Do not invent a cathedral health of five rails. Merchant should not grow a dependency on this until it means something.

## Tests

- Existing: member 200 ready true; non-member 403.
- If you change meaning: lock no-keys → ready false (except Test in Dev).

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §G2
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-11
