# D12 — CORS CSV for a browser second app

**Track:** D · **Depends:** K00  
**Analysis:** [`../05-identity-authz-tenancy.md`](../05-identity-authz-tenancy.md)  
**Goal:** Server-side M2M does not need CORS; a browser app does.

**Why:** `Pay:CorsOrigins` is a **replace** CSV. Setting only `http://localhost:3021` **drops** 5178/5179. Sample Node on the server does not need CORS; a browser SPA does.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs` | CSV, Production throw |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs` | Public pay OPTIONS |
| `apps/lazuar-pay/.env.example` | Commented CSV |
| E11 | Sample port 3021 |

**Current (`6d730d15`):** Development laptop list; Production empty fails boot.

---

## D12.1

- [x] README: set `Pay__CorsOrigins` to a **replace** CSV including merchant, checkout, **and** the second-app origin
- [x] Warning: setting the CSV replaces Development laptop defaults
- [x] Never AllowAnyOrigin; never ops :3003 / portal :3004

## D12.2 Must not

- [x] Do not add a wildcard in code to “help” samples

## D12.3 Exit

- [x] Track D complete
