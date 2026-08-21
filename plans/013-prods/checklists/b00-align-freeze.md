# B00 — Align and freeze

**Track:** Program  
**Analysis:** [`../README.md`](../README.md), [`../01-production-ready-bar.md`](../01-production-ready-bar.md)  
**Goal:** Lock Bar B so PRs cannot grow a second IdP, a Hub clone, or nine schemas.  
**No product code.**

---

## B00.1 Scope

- [x] Confirm the bar is **Bar B** (011 dogfood sentence on 8081/5178/5179), not 012 C99, not Hub feature-parity, not paper 02 Hub dark
- [x] Confirm 012 C99 stays closed: whoami, org ready, fixture checkout, CORS 5178/5179
- [x] Confirm apps: `lazuar-pay`, `lazuar-pay-merchant`, `lazuar-pay-checkout` only for product code
- [x] Confirm One repo product diffs are **rare** (M10 seed / M25 allowlist / O10 invites use existing One APIs)
- [x] Confirm ops/portal are **not** retargeted (P60)

## B00.2 Anti-goals (must stay refused)

- [x] No Pay password / `POST /one/auth/login` / Hub cookie on 8081
- [x] No Pay `organizations` / `users` table
- [x] No buyers as Zitadel humans; no OIDC on `:5179`
- [x] No Zitadel PAT / OpenFGA admin / Hub `Jwt:Secret` in Pay
- [x] No MediatR, `Modules.*`, BuildingBlocks, `apps/lazuar-api` ProjectReference
- [x] No FGA types `payment` / `document`; no `authz/write`
- [x] No homemade LHDN / Tax Invoice / VALID / UUID as receipt number
- [x] No Stripe Billing `subscription.updated` as SoT
- [x] No five PSP adapters on day one
- [x] No `VITE_API_URL` of ops/portal → 8081; no CORS `:3003`/`:3004`
- [x] No Go rewrite of the host in this program
- [x] No Hub ETL as a Bar B gate (paper 09: greenfield)

## B00.3 Fill [`decisions.md`](./decisions.md)

- [x] Confirm every lock row matches the team
- [x] Write **First rail** = Stripe XOR CHIP (not “both”, not Billplz-first unless you accept reminder-only)
- [x] Confirm public pay path `GET/POST /v1/pay/{token}` (do not ungated merchant GET)
- [x] Write **Migrator** = SQL files or one EF `PayDbContext`
- [x] Confirm local Postgres publish **5435**, database `lazuar_pay`

## B00.4 Ports (live dogfood)

- [x] One API **8080** + login **5175** + Zitadel **8085**
- [x] Pay **8081** + merchant **5178** + checkout **5179**
- [x] Hub `task dev` / compose `lazuar-api` / `task fe` / `task proxy` / turbo `pnpm dev` **off** while One owns 8080
- [x] Fingerprint One via `GET /api/v1/` `name=lazuar-one-api`

## B00.5 Tests vs live

- [x] CI / `task pay:test` = hermetic (fake One, fake PSP)
- [x] Live Ada OIDC = M26; live pay = after G+F+O, not a merge gate for M13 if tests exist
- [x] No Hub compose in Pay CI

## B00.6 Tracker honesty

- [x] Do **not** flip 011/12 steps 8–12 to `done` from B00
- [x] Do **not** flip Hub-parity rows
- [x] After each later phase, only the NP-* IDs in that phase Exit may move, and only when the **job** ran on the new stack

## B00.7 Exit

- [x] [`decisions.md`](./decisions.md) First rail + migrator filled
- [x] This checklist complete or amended in-place
- [x] Unblocked for M10 and D10
