# H11 — Ready down test

**Track:** H · **Depends:** H10  
**Goal:** A test that would have caught discarded bool.

**Why:** H10 is untestable if we only hit InMemory. Need a seam (`CanConnect` false) or Testcontainers stop.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs` | Happy path only |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayPostgres.cs` | Testcontainers helper (077) |
| `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs` | Probe |

**Current (`6d730d15`):** No down test.

---

## H11.1

- [x] Prefer: fake `CanConnect` false without live Postgres
- [x] Or Testcontainers: stop DB → 503
- [x] Existing HealthTests 200 path still green in Testing

## H11.2 Exit

- [x] Unblocked for H12
