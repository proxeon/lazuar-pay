---
number: "030"
id: PAY-ONE-003
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 030 — Writer is `/me` role overlay, not `authz/check admin`

- **Severity:** P1 if One drifts; P2 if they stay aligned
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B12 (also `07-identity-authz-cors.md` G1)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Member = OpenFGA-style `authz/check` `relation=member`. Writer = a **second** `GET /me` and `tenants[].role` in `{owner, admin}`. `MemberGate` does not read `WhoamiTenant.Status`. `is_platform_admin` is mapped and unused. Merchant chrome `canWriteMoney` is the same string test.

A user who is `member` in authz and `owner` in `/me` (or the reverse) gets a different answer than One’s graph. Suspended tenant with stale `/me` can still PUT keys if authz still allows member. Tests cover owner vs member; **admin-as-writer is untested**. **Member cannot create payment-link** has no dedicated test (create checkout/gateway/product do).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` **8–71**.
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` — `CheckMemberAsync` vs `GetWhoamiAsync`.
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- Tests: `Member_cannot_create_checkout`, `Member_cannot_put_gateway`, `Member_cannot_create_product`. Missing payment-link member 403; missing admin 201.

## Reproduction

One `/me` role `member`, FGA `admin` (or the reverse). PUT gateway vs POST payment-links disagree with One’s graph.

## Blast radius

Split-brain authorization. Platform admin without a tenant row cannot mint (probably intended).

## Suggested fix

One writer relation (`admin` / `owner`) on the same `authz/check` hop, **or** treat `/me` as the only source and drop the extra call. Check `status == active`. Do not invent a Pay admin. Add `Member_cannot_create_payment_link`. Do not copy OpenFGA into Pay.

## Tests

- Missing: member 403 on POST `/v1/payment-links`. Admin 201. Suspended `status` 403 on writer.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B12
- `plans/019-evals/07-identity-authz-cors.md` §G1 §G13 §G14
