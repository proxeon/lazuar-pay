# C00 — Align and freeze

**Track:** Program  
**Analysis:** [`../README.md`](../README.md), [`../10-dogfood-and-tests.md`](../10-dogfood-and-tests.md)  
**Goal:** Lock decisions so whoami PRs cannot grow a second IdP.  
**No product code.**

---

## C00.1 Scope of this program

- [x] Confirm the bar is **connected** (Pay trusts One HTTP), not 011 S0 steps 1–7 complete, not S1 money
- [x] Confirm first door is `GET /v1/whoami`, not `GET /v1/me`, not `GET /one/auth/me`
- [x] Confirm dummy admin is `GET /v1/orgs/{orgId}/ready`, not a money route
- [x] Confirm One repo is **not** in the C-phase diff
- [x] Confirm old ops/portal URLs are **not** retargeted to 8081

## C00.2 Anti-goals (must stay refused)

- [x] No Pay password form / `POST /one/auth/login` on 8081
- [x] No Pay `organizations` / `users` table
- [x] No buyers created as Zitadel humans
- [x] No Zitadel PAT / OpenFGA admin in Pay
- [x] No MediatR, `Modules/One` copy, or reference to `apps/lazuar-api`
- [x] No FGA types `payment` / `document`
- [x] No `authz/write` from Pay
- [x] No One webhooks, SPA, or `lzr_sk_` **in C-phases** (captured in P20–P30)

## C00.3 Ports and process set for live dogfood (C19)

- [x] One API **8080** + login **5175** + (optional) `lazuar-app` **5174**
- [x] Focused Pay **8081**
- [x] Hub `task dev` / docker-compose `lazuar-api` / `task fe` **off** while One owns 8080
- [x] Note both Hub `/health` and One `/health` can look like `{status:ok}` — fingerprint One via `/api/v1/` `name=lazuar-one-api` ([05](../05-local-topology.md))

## C00.4 JSON and headers

- [x] Write the whoami field list into [`decisions.md`](./decisions.md) (already drafted — confirm or amend)
- [x] Bearer: `Authorization` forwarded **verbatim** (including `Bearer `)
- [x] Optional forward: `X-Lazuar-Tenant-Id` as hint only
- [x] One JSON is snake_case; Pay matches snake_case on whoami

## C00.5 Tests vs live

- [x] CI / `task pay:test` = hermetic fake One
- [x] Live curl = C19 only, not a gate for C13 merge if tests exist
- [x] No Testcontainers/Zitadel in this program

## C00.6 Tracker honesty

- [x] Do **not** flip 011/12 steps 1–7 to `done`
- [x] After C99, only these 011 rows may move (whoami/authz mapping, not SPA): NP-ONE-003, NP-ONE-006, NP-ONE-008 (negative), NP-ONE-009 (mapping law), NP-ONE-007, NP-ONE-015, NP-ONE-020 (partial: still no PAT)

## C00.7 Exit

- [x] [`decisions.md`](./decisions.md) matches the table the team will implement
- [x] This checklist complete or amended in-place
- [x] Unblocked for C10
