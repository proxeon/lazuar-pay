# H14 — Production empty/laptop One URL fails boot

**Track:** H · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.2  
**Goal:** Production does not silently call `localhost:8080`.

**Why:** `OneOptions.BaseUrl` defaults to `http://localhost:8080/api/v1`. Production whoami looks healthy against the operator’s One or a random 8080.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs` | Default laptop URL |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | Bind `One` section |
| `apps/lazuar-pay/.env.example` | `One__BaseUrl=http://localhost:8080/api/v1` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | Overrides to `http://one.test/api/v1` |

**Current (`6d730d15`):** Default is laptop in the **class**, not only Development json.

---

## H14.1

- [x] Production/Staging: `One:BaseUrl` missing or `localhost` → fail boot
- [x] Move laptop default out of base `appsettings.json` into `appsettings.Development.json` if that is where it lives today
- [x] Testing factory still sets `http://one.test/api/v1`

## H14.2 Tests

- [x] Production factory with WrapKey + CS + CORS but `One:BaseUrl=http://localhost:8080/api/v1` fails boot
- [x] HTTPS example URL allowed

## H14.3 Exit

- [x] Unblocked for H16
