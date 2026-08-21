# B00 — Align and freeze

**Track:** Program  
**Analysis:** [`../README.md`](../README.md), [`../01-production-ready-bar.md`](../01-production-ready-bar.md)  
**Goal:** Lock Bar B so PRs cannot grow a second IdP, a Hub clone, or nine schemas.  
**No product code.**

---

## B00.1 Scope

- [ ] Confirm the bar is **Bar B** (011 dogfood sentence on 8081/5178/5179), not 012 C99, not Hub feature-parity, not paper 02 Hub dark
- [ ] Confirm 012 C99 stays closed: whoami, org ready, fixture checkout, CORS 5178/5179
- [ ] Confirm apps: `lazuar-pay`, `lazuar-pay-merchant`, `lazuar-pay-checkout` only for product code
- [ ] Confirm One repo product diffs are **rare** (M10 seed / M25 allowlist / O10 invites use existing One APIs)
- [ ] Confirm ops/portal are **not** retargeted (P60)

## B00.2 Anti-goals (must stay refused)

- [ ] No Pay password / `POST /one/auth/login` / Hub cookie on 8081
- [ ] No Pay `organizations` / `users` table
- [ ] No buyers as Zitadel humans; no OIDC on `:5179`
- [ ] No Zitadel PAT / OpenFGA admin / Hub `Jwt:Secret` in Pay
- [ ] No MediatR, `Modules.*`, BuildingBlocks, `apps/lazuar-api` ProjectReference
- [ ] No FGA types `payment` / `document`; no `authz/write`
- [ ] No homemade LHDN / Tax Invoice / VALID / UUID as receipt number
- [ ] No Stripe Billing `subscription.updated` as SoT
- [ ] No five PSP adapters on day one
- [ ] No `VITE_API_URL` of ops/portal → 8081; no CORS `:3003`/`:3004`
- [ ] No Go rewrite of the host in this program
- [ ] No Hub ETL as a Bar B gate (paper 09: greenfield)

## B00.3 Fill [`decisions.md`](./decisions.md)

- [ ] Confirm every lock row matches the team
- [ ] Write **First rail** = Stripe XOR CHIP (not “both”, not Billplz-first unless you accept reminder-only)
- [ ] Confirm public pay path `GET/POST /v1/pay/{token}` (do not ungated merchant GET)
- [ ] Write **Migrator** = SQL files or one EF `PayDbContext`
- [ ] Confirm local Postgres publish **5435**, database `lazuar_pay`

## B00.4 Ports (live dogfood)

- [ ] One API **8080** + login **5175** + Zitadel **8085**
- [ ] Pay **8081** + merchant **5178** + checkout **5179**
- [ ] Hub `task dev` / compose `lazuar-api` / `task fe` / `task proxy` / turbo `pnpm dev` **off** while One owns 8080
- [ ] Fingerprint One via `GET /api/v1/` `name=lazuar-one-api`

## B00.5 Tests vs live

- [ ] CI / `task pay:test` = hermetic (fake One, fake PSP)
- [ ] Live Ada OIDC = M26; live pay = after G+F+O, not a merge gate for M13 if tests exist
- [ ] No Hub compose in Pay CI

## B00.6 Tracker honesty

- [ ] Do **not** flip 011/12 steps 8–12 to `done` from B00
- [ ] Do **not** flip Hub-parity rows
- [ ] After each later phase, only the NP-* IDs in that phase Exit may move, and only when the **job** ran on the new stack

## B00.7 Exit

- [ ] [`decisions.md`](./decisions.md) First rail + migrator filled
- [ ] This checklist complete or amended in-place
- [ ] Unblocked for M10 and D10
