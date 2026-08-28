# H10 — `/ready` uses CanConnect bool

**Track:** H · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.1  
**Goal:** Dead Postgres is not 200.

**Why:** `CanConnectAsync` is awaited and **discarded**. A false result still 200 `{ status: ready }`. Only exceptions become 503. InMemory always connects, so HealthTests stay green.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs` | Lines 11–22: await, ignore bool |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs` | **Different** door `/v1/orgs/{orgId}/ready` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs` | `Unversioned_ready_returns_200_on_inmemory` |
| `scripts/check-pay-openapi-honesty.mjs` | `GET /ready` host-only |

**Current (`6d730d15`):** Catch-only 503. Bool unused.

---

## H10.1

- [ ] `GET /ready` awaits `Database.CanConnectAsync`
- [ ] `false` → **503** JSON `{ status: "not_ready" }` (or problem object — **pick `{ status: not_ready }`** to stay a probe)
- [ ] `true` → 200 `{ status: "ok" }` (or `ready`)
- [ ] Testing InMemory: CanConnect true → 200 still
- [ ] `/health` and `/v1/health` unchanged liveness
- [ ] Not org ready

## H10.2 Must not

- [ ] Do not add `/ready` to pay-spec (honesty IMPL_ONLY)

## H10.3 Exit

- [ ] Unblocked for H11
