---
number: "065"
id: PAY-ONE-007
severity: P2
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 065 — Suspended tenant copy says “not a member”

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B8
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

One 403 on suspend is mapped to `"Not a member of this org"`. The human still **is** a member; the tenant is suspended. Overview still renders if `/me` lists `status: "suspended"` because `OrgLayout` only checks `tenants.find`, not `status`. Subsequent money GETs 403 with the wrong sentence.

Buyer pause is 011 (`ChargesPaused`). This is staff copy.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` **38–40**.
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx` **44–46**.

## Reproduction

Suspend tenant in One. Staff opens overview (whoami still lists it). Click Pay links. 403 “Not a member.”

## Blast radius

Support confusion. Not a money leak if 011 works; if 011 does not, buyers still pay (worse).

## Suggested fix

Pass through One’s `"Tenant is suspended."` when present. OrgLayout: if `tenant.status === 'suspended'`, banner before money pages. Do not treat suspend as “not a member.”

## Tests

- Missing: One 403 body “suspended” → Pay 403 same meaning. OrgLayout reads `status`.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B8
