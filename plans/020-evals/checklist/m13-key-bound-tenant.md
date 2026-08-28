# M13 — Key bound to path org, tenant active

**Track:** M · **Depends:** M12  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.1 steps 2–3  
**Goal:** A key is one-shop. Mismatch or suspend is 403.

**Why:** One binds a key to **one** tenant. Pay `org_id` is that UUID. If we skip `authz/check` (M12) and only read `/me`, we must still refuse path `/orgs/t2` when the key’s tenant is `t1`, and refuse `status != active` (pause still works without `authz/check`).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | JWT: path org + `authz/check`; writer also `/me` status |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs` | `/me` projection `tenants[]` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs` | Path vs `X-Lazuar-Tenant-Id` hint |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs` | Other-org 403 |

**Current (`6d730d15`):** Cross-org is enforced by One `authz/check` for JWTs. Keys never get that far.

---

## M13.1 Bound tenant

- [x] From key `/me`, find tenant `id == path orgId`
- [x] Zero tenants → 403
- [x] Path org not in `tenants` → 403 (not 404)
- [x] Multiple tenants: still require exact path match (keys should be one-tenant; extra ids 403 unless `id == orgId`)

## M13.2 Active

- [x] `status` must be `active` (same fail-closed as writer overlay)
- [x] Suspend copy: if One detail contains suspend, pass that sentence (existing `SuspendedDetail`)

## M13.3 Tests

- [x] Key bound `t1` on `/v1/orgs/t1/ready` → 200 (member door)
- [x] Key bound `t1` on `/v1/orgs/t2/ready` → 403
- [x] Key `/me` tenant `status: suspended` → 403, body may contain suspend

## M13.4 Must not

- [x] Header `X-Lazuar-Tenant-Id` cannot authorize a different org (path SoT)

## M13.5 Exit

- [x] Unblocked for M14
