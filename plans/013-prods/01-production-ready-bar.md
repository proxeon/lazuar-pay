# 01 — What “production-ready” means for the new stack (and why Hub parity is the failure mode)

**Date:** 21 August 2026  
**Slice:** program 013 — production-ready bar for the three new processes, then replace the old tree  
**Kind:** analysis only. No C# implementation. No Vite product-code change. No flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. No cutover runbook (that is [02](./02-replace-old-cutover.md)).  
**Branch at analysis:** `feat/012-connect-one`

**Repos / HEAD**

| Repo | Path | Short SHA | Full SHA | Tip |
|------|------|-----------|----------|-----|
| Focused Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6f866ff0` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| Lazuar One (sibling, HTTP SoT) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

`git rev-parse HEAD`, `git rev-parse --short HEAD`, `git log -1`, and `git branch --show-current` were run in both working copies on 21 Aug 2026. Pay’s tip is on `feat/012-connect-one`. One’s tip is still the same WIP commit 012 pinned (`main`). If either tree moves, re-pin the SHAs before treating path lists, route counts, or 011 Status cells as frozen.

**What “Pay” means in this paper**

- The **new focused host** is `apps/lazuar-pay` (`Lazuar.Pay`, TFM `net10.0`), listening on **http://localhost:8081**. It is not `apps/lazuar-api`. It is not a Go tree. 011/05 still says Go for a *hypothetical kernel rewrite*; **this program ships the existing focused C# host + two Vite apps.** Recommending a language swap is **out of this program** (see §8 and §10).
- The **new merchant shell** is `apps/lazuar-pay-merchant`, Vite **http://localhost:5178**. It is not `apps/lazuar-ops` (`:3003`).
- The **new hosted pay page** is `apps/lazuar-pay-checkout`, Vite **http://localhost:5179**. It is not `apps/lazuar-portal` (`:3004`). Buyers have **no** One account.
- The **old modular Pay** (`apps/lazuar-api` on **8080**, plus `apps/lazuar-ops` / `lazuar-portal` / `lazuar-admin`) is the **museum this rewrite leaves**. It is reference for judgment (SST unit × seats, wrap-rails, receipt ≠ tax invoice). It is not the production-ready target. Do not grow it. Do not retarget its `VITE_API_URL` at 8081. Do not implement issues 261–334 on it as a way to “make Hub prod-ready enough to keep.”
- The **HTTP server the new host already calls** is One’s `lazuar-api` at **http://localhost:8080**, product surface **`/api/v1`**. One tenant id **is** Pay `org_id`.

**What “production-ready” is allowed to mean here**

Not “feature-for-feature with Hub.” Not “LHDN VALID.” Not “ops chat.” Not “cookie JWT.” Not “One staging PASSED + Okta + SCIM.” Not “connected” (that bar already closed in 012 C99).

The only sentence that may be called production-ready for this program is the 011 dogfood test, lived on the **new** three processes:

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

If a feature is not on that path, it is not the production-ready **gate**. If a feature *is* Hub chrome (tax invoices, WhatsApp, credits wallet, ops chat, Hub SaaS plan page), it is **refuse** or **later**, never a gate.

**What this paper is not**

- Not [02-replace-old-cutover.md](./02-replace-old-cutover.md) (kill criteria, dual-run, DNS).
- Not [03-host-production-seams.md](./03-host-production-seams.md) (Postgres, secrets, Dockerfile, health for k8s).
- Not [04-merchant-frontend.md](./04-merchant-frontend.md) (OIDC wiring, page inventory).
- Not [05-checkout-frontend.md](./05-checkout-frontend.md) (hosted pay UX).
- Not [06-money-rails.md](./06-money-rails.md) (Stripe/CHIP adapters).
- Not [07-fulfillment-ledger-docs.md](./07-fulfillment-ledger-docs.md) (journal + `RCPT-`).
- Not [08-one-identity-production.md](./08-one-identity-production.md) (SPA registration, `lzr_sk_`, HMAC).
- Not [09-data-migration.md](./09-data-migration.md).
- Not [10-ci-observability-decommission.md](./10-ci-observability-decommission.md).
- Not a flip of 011/11 or 011/12.
- Not a Go rewrite plan.
- Not an order to mega-merge One+Pay or to five-deploy Notify/Audit/Media.

Parent index: [README.md](./README.md). Binding from [011](../011-new-lazuar-pay/README.md) and [012](../012-one-to-pay/README.md). Freeze table for the already-shipped connect work: [012/checklists/decisions.md](../012-one-to-pay/checklists/decisions.md).

---

## 0. Locked decisions this bar must not reverse

Copied so a later 013 paper cannot “clarify” them into Hub parity.

| Lock | Source | Meaning for *this* bar |
|------|--------|------------------------|
| Do not grow the C# cathedral | 011 README binding #1; 00; 09 | Steal **judgment**, not `Modules/*` folders, not MediatR, not per-module DbContexts. IsolationTests must stay red if anyone `ProjectReference`s `apps/lazuar-api`. |
| Do not rebuild `Modules/One` | 011 binding #2; 02-one-integration | Merchants are One humans + One tenants. One tenant id **is** Pay `org_id`. No second org table (NP-XX-014). |
| Homemade MyInvois / UBL out of v1 | 011 binding #3; NP-XX-001–003 | Tax later = a **provider**. Receipt is `RCPT-…`, never titled Tax Invoice, never prints VALID. |
| Buyers are not Zitadel humans | 01-product; NP-XX-013; NP-CHK-007 | No Pay password/IdP. Never `id_token` as Bearer. Checkout `:5179` must fail if it asks for Zitadel login. |
| Listen **8081**, never 8080 | 012 decisions; launchSettings | One and old Hub both want 8080. Focused Pay exists so those can keep 8080. Do not retarget `lazuar-ops` / `lazuar-portal` `VITE_API_URL` to 8081 (P60). |
| Never ship merchants to One admin `:5173` or Hub admin `:3005` | NP-ONE-005; NP-XX-018 | Product login is One `:5175`. Merchant homepage is Pay `:5178`. |
| Bezos door: public `/v1` from day one | 08-bezos-door; NP-API | One Pay process. HTTP **to** One. Function calls **inside** Pay (ledger + receipt + audit in one handler). |
| Notify/audit for Pay writes stay in Pay | 011 binding #7; NP-XX-019 | No `lazuar-notify` service in v1. Audit row in the **same DB transaction** as the write. |
| Do not mega-merge One+Pay; do not five-deploy | 011 binding #10; 13 | Ship existing One + one Pay process. Media later. |
| Language of the current host is C# net10 | 011/05 vs 012/03 vs this SHA | 011 said Go for a hypothetical kernel. **Out of this program:** abandoning `apps/lazuar-pay` for Go. |
| VIEWER is not a One tenant role | 012/07; C24; NP-ONE-021 | One membership is `owner` \| `admin` \| `member` only. OpenFGA `viewer` is on type **`app`**, not staff read-only. NP-ONE-021 is **Pay-enforced using One role**, not `check(member)` alone. Dummy `/ready` checking `member` is “has the tenant,” not “cannot charge.” |
| First-slice dogfood sentence | 011/01, 03, 12 | Merchant via One → keys → buyer pays without One account → `RCPT-` + balanced journal → webhook retry no-ops → MEMBER sees ops, VIEWER cannot charge. |

012 C99 is **closed** on this SHA (`GET /v1/whoami`, `GET /v1/orgs/{orgId}/ready`, hermetic tests). C99 is **connected**, not production-ready. P10–P60 remain parked relative to 012; 013 is the program that is allowed to unpark money and UIs **without** unparking P60 (old ops/portal on 8081 stays refused).

---

## 1. Method — what was opened

Nothing was implemented. The following were read in full or in the cited ranges. Counts were taken from the trees at the SHAs above (Pay `6f866ff0`, One `0f79fe4`).

### 1.1 Pay plans (consumer intent and the living tracker)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/013-prods/README.md` — this program’s index; three new apps vs four old apps; binding; paper 01–10 assignment.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/README.md` — binding decisions 1–10; second cut (One already exists); language note (Go for a *new kernel*, not an order to rewrite this host this program).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/00-why-leave.md` — modular-monolith tax; useful inheritance is judgment not folders; homemade LHDN was the wrong extract.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/01-product.md` — must / should / later / never; the dogfood sentence this paper treats as the production-ready **gate**.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/02-one-integration.md` — HTTP Pay calls; secrets Pay must not hold; One staging **NOT PASSED**; do not call `POST /platform/tenants`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/03-first-slice.md` — One-side stop-after-this; Pay-side money; pass/fail locks.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/05-language.md` — Go verdict for a *new* Pay; C# gravity; **this paper does not reopen that as an implement order**.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/06-platforms.md` — Pay / One / Notify / Audit / Media as a **map**, not four deploys; Notify in Pay until a second sending domain.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/07-separate-vs-one-binary.md` — cost of Pay↔Notify and Pay↔Audit as processes.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/08-bezos-door.md` — Bezos is the door (`/v1`); Linux is the room (one Pay process).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/09-old-pay.md` — score as year-two core: poor; 74 P2s still filed (issues 261–334); money math after the harvest is judgment to steal.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/10-tracker-schema.md` — waves S0 / S1 / V1 / soon / later / refuse; `Dogfood = Y` meaning.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md` — living matrix. Counts on this SHA: **115** rows; **10 done**; **81 todo**; **24 refuse**. S0 5 done / 17 todo; S1 5 done / 37 todo.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/12-first-slice-tracker.md` — ordered steps 1–12 still **todo** (C99 forbade flipping them for whoami alone).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/13-monolith-vs-services.md` — do not mega-merge; do not five-deploy; ship existing One + one Pay binary.

### 1.2 012 connect (what is already true, and what was parked)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/README.md` — connected ≠ S0 ≠ S1.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/01-one-http-surface.md` — voice and method this paper copies; One `/api/v1` vs Pay `/v1`; port map.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/03-pay-host-seams.md` — whoami as endpoint not middleware; no Dockerfile in that slice (still true).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/04-pay-spec-contract.md` — three TypeSpec trees; do not copy `/one/*` into `pay-spec`; do not hook `task gen`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/05-local-topology.md` — 8080 footgun; Hub compose still points at old API.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/06-tenant-org.md` — One tenant UUID is Pay `org_id`; no Pay `organizations` table.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/07-authz-roles.md` — VIEWER honesty gap; `authz/check` allow-list `{tenant, app}`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/10-dogfood-and-tests.md` — three bars (connected / S0 / S1); fail table F1–F20; anti-goals.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/checklists/decisions.md` — freeze.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/checklists/c00-align-freeze.md`, `c24-viewer-honesty.md`, `c99-connected-done.md`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/checklists/p10-spa-oidc.md` — P10.1 checked that `:5178` / `:5179` **origins exist**; OIDC still unwired.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/checklists/p20-machine-key.md`, `p30-one-webhooks.md`, `p50-money.md`, `p60-old-frontends.md`.

### 1.3 Old-tree evidence (the failure mode, not the target)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/008-evals/README.md` and `07-ops-portal-admin-frontend.md` — live ops/portal/admin routes after Waves 1–4; Hub cookie split; ops chat unrouted; TIN-at-checkout; Hub admin is a single gateways page.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/*/routes.tsp` — **152** HTTP operation attributes (`@get`/`@post`/`@patch`/`@put`/`@delete`) across billing (13), commerce admin (39), commerce public (16), commerce integration (3), communications (12), lhdn (13), one (39), ops (9), payments (3), platform (5). CRM and messaging TypeSpec are **models only** (no `routes.tsp`).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx` — current route table (re-counted; 008’s line numbers are stale).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/lib/api-client.ts` — `VITE_API_URL` default `http://localhost:8080/api/v1`; `credentials: "include"`; header **`X-Tenant-Id`** (old name).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/App.tsx` — `/platform/gateways` only.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/**/page.tsx` — 12 page files.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/*` — nine modules; **784** `.cs` files excluding `bin/`/`obj/` (Commerce 217, One 124, Billing 102, Lhdn 101, Payments 91, Communications 52, Ops 37, Messaging 35, CRM 25) plus **37** host `.cs` under `apps/lazuar-api/src`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Properties/launchSettings.json` — `http://localhost:8080`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` — dual cookie `lazuar_auth` vs `lazuar_admin_auth`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.yml` — `api` still `apps/lazuar-api/Dockerfile` on **8080**; ops **3003**, portal **3004**, admin **3005**; `VITE_API_URL` → Hub.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml` — old frontend set only (developers 3002, ops 3003, portal 3004, admin 3005). No `lazuar-pay` / merchant / checkout procs.

### 1.4 New stack (what is actually running)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json` (`One:BaseUrl = http://localhost:8080/api/v1`, timeout 5s)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` — Sdk.Web, **no PackageReference**, `InternalsVisibleTo` tests, TFM `net10.0`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/global.json` — SDK `10.0.100`, `rollForward: latestFeature`
- Entire `src/Lazuar.Pay/One/` and `src/Lazuar.Pay/Checkouts/` (17 `.cs` files excluding `bin/`/`obj/`)
- Entire `tests/Lazuar.Pay.Tests/` (8 `.cs` files; **31** `[Test]` methods: Whoami 6, OrgReady 6, Checkout 9, Health 3, Isolation 4, Cors 3)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp` and `README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/{README.md,src/App.tsx,vite.config.ts,package.json,.env.example}`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/{README.md,src/App.tsx,vite.config.ts,package.json}`
- Root `Taskfile.yml` `pay:*` tasks (lines 90–129)

### 1.5 One (staging honesty, not a Pay ticket)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/017-evals/08-dogfood-then-serve.md` header and §1 (staging **NOT PASSED**; unpublished packages; no hosted SKU; Pay is Consumer-0).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/008-next/STAGING-PROOF-STATUS.md` — binary **NOT PASSED** (dated 2026-08-10; still not flipped on `0f79fe4`). Human steps 1–15 open. Exit criteria “Two distinct humans… isolation 403… STATUS → PASSED” all **NOT DONE**.

### 1.6 Commands run (not a live stack proof)

| Command | Result |
|---------|--------|
| `git rev-parse HEAD` (Pay) | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` |
| `git log -1` (Pay) | `feat(pay): scaffold merchant and checkout Vite apps` |
| `git branch --show-current` (Pay) | `feat/012-connect-one` |
| `git -C …/lazuar-one rev-parse HEAD` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` |
| `git log --oneline -15` (Pay) | C10–C99 + fixture checkouts + Vite scaffold (see §2.12) |
| `rg '@(get\|post\|…)' packages/api-spec` | 152 operations (breakdown §4.3) |
| `find apps/lazuar-api/Modules -name '*.cs'` | 784 module sources |
| `find apps/lazuar-pay/src -name '*.cs'` | **17** host sources |

This paper did **not** boot One, Pay, or Hub. It did **not** run `task pay:test`. Runtime claims below are from source + tests + READMEs, not from a live curl on 21 Aug.

### 1.7 What was not opened (so we cannot pretend)

- Live One staging VM / evidence pack (does not exist; STATUS file says so).
- Hub issues 261–334 bodies (cited as a set from 09-old-pay, not re-triaged).
- A production hostname, TLS cert, or Pay Dockerfile (there is **no** `apps/lazuar-pay/Dockerfile` on this SHA).
- CHIP / Stripe dashboard configuration.
- SST registration data source for a real merchant.
- 013 papers 02–10 (they do not exist yet; this paper only names them).

---

## 2. Current honesty of the new stack (what is actually running vs demo)

The new stack is **real enough to be connected** and **not real enough to take money**. Calling it production-ready today would be the same class of lie 008 caught in Hub READMEs.

### 2.1 Three processes, three ports, one Bezos prefix

| Process | Path | Listen | Role on `6f866ff0` | Role at the production-ready gate |
|---------|------|--------|--------------------|-----------------------------------|
| Focused host | `apps/lazuar-pay` | **8081** | Health, whoami, dummy org-ready, **in-memory** checkout fixture | Money kernel: BYOK, webhook, journal, `RCPT-`, `/v1` door |
| Merchant Vite | `apps/lazuar-pay-merchant` | **5178** (`strictPort`) | Health probe of 8081 only. **No OIDC.** | Staff shell: One login `:5175`, products, keys, receipts. Client of `/v1`. |
| Checkout Vite | `apps/lazuar-pay-checkout` | **5179** (`strictPort`) | Health probe of 8081 only. Copy says buyers have no One account. **Does not take a card.** | Hosted pay page. Fail if Zitadel login appears. |

CORS on the host is already honest about the split (`Program.cs`): allow **5178** and **5179** (and 127.0.0.1 twins); **do not** allow Hub ops `:3003`. `CorsTests.Health_does_not_allow_ops_origin` asserts that. That is a production-ready *lock*, not a nice-to-have: if someone “just” adds `http://localhost:3003` to CORS so old ops can talk to 8081, P60 has been reversed.

Env name on the new UIs is `VITE_PAY_API_URL` (default `http://localhost:8081`), **not** `VITE_API_URL`. Old ops still uses `VITE_API_URL` → Hub `http://localhost:8080/api/v1`. Do not unify the names as a “cleanup.”

### 2.2 Host HTTP that actually exists

`Program.cs` on this SHA:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
```

| Method | Path | Auth | Calls One? | Persistence | Honesty |
|--------|------|------|------------|-------------|---------|
| GET | `/health` | none | **never** (C15 / HealthTests) | none | Liveness of Pay process only |
| GET | `/v1/health` | none | **never** | none | Same, versioned alias |
| GET | `/v1/whoami` | Bearer required | Yes: `GET {One:BaseUrl}/me` once | none | Projection, not clone of One `MeResponse`, not Hub `AuthUser` |
| GET | `/v1/orgs/{orgId}/ready` | Bearer + `authz/check member` on `type=tenant` | Yes | none | Dummy admin. `ready: true` means “One said member,” **not** “keys pasted / SST known / can charge” |
| POST | `/v1/checkouts` | Bearer + member of `body.org_id` | Yes (`authz/check`) | **in-memory** `CheckoutStore` | Fixture. `status: "open"`. Not a charge. Currency defaults **MYR**. Idempotency-Key per org. |
| GET | `/v1/checkouts/{id}` | Bearer + member of **session** org | Yes | in-memory | Other org → 403. Unknown id → 404 **without** calling One |

There is no `POST /one/auth/login`. There is no cookie. There is no Pay password. There is no webhook URL. There is no `RCPT-`. There is no journal. There is no Postgres. There is no Dockerfile.

`packages/pay-spec/main.tsp` matches those doors (health, whoami, org ready, checkout create/get) under `@server("http://localhost:8081")`, namespace `LazuarPay`, prefix **`/v1`** (not `/api/v1`). Five TypeSpec operations vs Hub’s **152**. Unversioned `/health` is host-only (not in the spec), same pattern as One.

`pay-spec/README.md` still says “Grow `main.tsp` when `POST /v1/checkouts` exists.” That sentence is **stale** on this SHA (checkout is in the spec). Stale README is a Hub disease (00-why-leave). Fixing it is hygiene, not a production-ready feature — but do not let 013 papers claim the spec is still health-only.

### 2.3 Whoami is a projection; org_id is the tenant id

`WhoamiResponse` fields actually serialized (snake_case): `user_id`, `email`, `is_platform_admin`, `active_org_id`, `tenants[]` of `{ id, slug, name, role, status }`. `OneMeMapper` copies One `active_tenant_id` → `active_org_id` and One `tenants[].id` → `tenants[].id`. That id **is** `org_id` on checkout create.

012/10 drafted a whoami that emitted `orgs[]` and forbade a parallel `tenants` array. **012 C00 / `decisions.md` locked `tenants[]` instead.** The host matches the freeze, not the earlier 012/10 draft. Production-ready does not require renaming `tenants` to `orgs`. It requires that nobody adds a second UUID.

Whoami errors (from `WhoamiEndpoints.Map`): missing Bearer → 401 **without** calling One; One 401 → 401; One 403 → 403; timeout / transport / other → **503**. Never 200 empty user. That mapping is a production-ready identity lock. It is **not** sufficient identity: SPA still does not mint the Bearer (P10).

### 2.4 `/ready` is not NP-ONE-021

`MemberGate.RequireMemberAsync` always posts `relation=member`, `object.type=tenant`, `object.id={orgId}`. Path is SoT (`OrgReadyTests.Ready_checks_path_org_not_header`: header `header-org` does not replace path `path-org`). `{allowed:false}` and One 403 both become Pay 403.

C24 and the host README are explicit:

> Staff **VIEWER** is not a One tenant role (`owner` / `admin` / `member` only); `/v1/orgs/{orgId}/ready` checks `member`, not “cannot charge”.

011/11 still has **NP-ONE-021 = todo**. A PR that flips 021 because `/ready` returned 200 has lied. Production-ready **requires** Pay-side enforcement on money routes (change keys, refund, charge) using the One role from `/me` plus a stricter `authz/check` (`admin` / `owner` for writes). Mapping Hub `VIEWER` onto One `member` is how you fail closed for the wrong people (every member becomes “cannot charge”) or fail open (every member can charge). Either is a museum bug.

### 2.5 Fixture checkout is not S1 money

`CheckoutStore` is a `ConcurrentDictionary`. Comment on the type: “In-memory fixture store. Not a ledger. Replace when money is real.” Session `Id` is `Guid.NewGuid().ToString("N")` — a UUID without dashes. That is acceptable as an **opaque checkout id**. It is **forbidden** as a document number (NP-DOC-002, NP-XX-003). Do not print this id as `RCPT-`. Do not title a JSON field `tax_invoice`.

P50 on this SHA:

- [x] `POST /v1/checkouts` and GET on Pay `/v1` (fixture, `status: open`)
- [x] Tenant/org is One tenant id from whoami/authz
- [ ] Buyer pays **without** a One account
- Hosted page, rails, journal, `RCPT-` still out

Checkout create is a **merchant** call (Bearer + member). It is the opposite of “buyer pays on the hosted page.” A demo that curls POST `/v1/checkouts` with Ada’s access_token and calls that “we take payments” is a demo. Production-ready forbids counting it.

### 2.6 Vite apps are origin pins, not products

Merchant `App.tsx` (`apps/lazuar-pay-merchant/src/App.tsx`): one `useEffect` `fetch(${payApi}/health)`. Copy tells the human that sign-in is One `:5175` and this origin is not ops `:3003`. Dependencies: `react`, `react-dom`. **No** `oidc-client`, **no** `@repo/api-types-ts`, **no** whoami fetch, **no** router.

Checkout `App.tsx`: the same health probe. Copy: “Buyers have no One account and no Pay password form.” There is no amount, no card element, no session id in the URL.

`vite.config.ts` on both: `strictPort: true` so 5178 cannot silently steal 5175/5179 and 5179 cannot steal 5178. That pin is part of the bar (port honesty). It is not a checkout.

P10.1 is checked: origins exist. P10.2 (register SPA, PKCE, redirects, login `:5175`) is unchecked. Production-ready of **merchant** starts at P10.2, not at “the SPA builds.”

### 2.7 Tests vs live vs Hub CI

| Gate | What it proves today | What it must not be asked to prove |
|------|----------------------|------------------------------------|
| `task pay:test` | Hermetic `WebApplicationFactory` + fake `HttpMessageHandler`. Whoami 200/401/503. Authz allow/deny/header-hint. Checkout open/idempotent/403. Health never calls One. Isolation bans `lazuar-api` / `Modules.` / `BuildingBlocks` / `MediatR` / `Lazuar.Api`. CORS allows 5178/5179, denies 3003. | Live One, Zitadel, CHIP, journal balance, SPA login |
| `task pay:spec` | TypeSpec compile of `packages/pay-spec` | Hub honesty allowlist, `task gen` |
| Live curl (README) | Human with One up, Hub **off**, access_token (not id_token) → whoami; optional fixture checkout | Production-ready money |
| Hub `task contracts:honesty` / `task gen` | Old `packages/api-spec` | Must stay **unhooked** from pay-spec |

`Taskfile.yml` still describes `pay:test` as “health + isolation.” That description is stale (31 tests). Same class of drift as Hub READMEs. Not a blocker; do not treat the Taskfile blurb as the test inventory.

There is still **no** Testcontainers, no Pay Postgres, no Playwright on 5178/5179.

### 2.8 011/11 Status on this SHA (whoami/authz/fixture done; money/OIDC/catalog still todo)

Copied from `11-checklist.md` counts so this paper cannot drift by paraphrase.

| Wave | Rows | todo | doing | done | blocked | refuse | n/a |
|------|------|------|-------|------|---------|--------|-----|
| S0 | 22 | 17 | 0 | 5 | 0 | 0 | 0 |
| S1 | 42 | 37 | 0 | 5 | 0 | 0 | 0 |
| V1 | 12 | 12 | 0 | 0 | 0 | 0 | 0 |
| soon | 9 | 9 | 0 | 0 | 0 | 0 | 0 |
| later | 6 | 6 | 0 | 0 | 0 | 0 | 0 |
| refuse | 24 | 0 | 0 | 0 | 0 | 24 | 0 |
| **Total** | **115** | **81** | **0** | **10** | **0** | **24** | **0** |

**Done (do not re-do; do not over-claim):**

| ID | What actually shipped | Still missing for the dogfood sentence |
|----|----------------------|----------------------------------------|
| NP-ONE-003 | Whoami forwards Bearer; never id_token as Bearer **on this route** | SPA must send access_token (P10) |
| NP-ONE-006 | `GET /v1/whoami` → One `/me` once; not middleware; not on `/health` | Create-workspace / empty-tenants UX |
| NP-ONE-007 | Path `{orgId}` SoT on `/ready` and checkouts | Every future money route must keep this |
| NP-ONE-008 | Projection copies One `role`; no Zitadel claim parse | — |
| NP-ONE-015 | Dummy `/ready` + checkout gate `check(member)` | `admin`/`owner` checks for keys/refund; NP-ONE-021 |
| NP-CHK-001 | Fixture session amount/currency/tenant | `open → paid` (NP-CHK-004); real rail |
| NP-CHK-002 | success/cancel URLs stored | Not fulfillment |
| NP-CHK-003 | Idempotency-Key header or body, per org, in-memory | Persist with money POSTs (NP-API-006) |
| NP-API-001 | `POST /v1/checkouts` exists on Pay `/v1` | Real checkout, not fixture |
| NP-API-003 | `GET /v1/checkouts/{id}`; other org 403 | Payment status after paid |

**Still todo, and the dogfood sentence fails without them (`Dogfood = Y`):**

S0: NP-ONE-001, 002, 004, 005, 009, 011, 012, 014, 017, 018, 021, 022.

S1: NP-CAT-001, 002, 003, 005; NP-CHK-004, 005, 006, 007; NP-GW-001, 002 **or** 003, 004, 006, 009; NP-FUL-001, 002, 003; NP-MON-001; NP-DOC-001, 002, 003, 005; NP-BUY-001; NP-API-002, 004.

That Y-set **is** the production-ready gate mapped to IDs. Section 3 lists must/should/later/refuse for the rest.

### 2.9 011/12 ordered loop is still todo

C99.5: “Do not mark 011/12 step 2 complete just because whoami exists (step 2 includes `:5175` login UX).”

On this SHA every 011/12 step 1–6 and 8–12 is still `todo`. Step 7 is `refuse (keep)` (no SCIM, no custom FGA types, no npm publish, no hosted SKU). That is correct. Production-ready **does** require steps 1–6 and 8–12 to have actually **run** (a human loop, not only unit tests). It does **not** require One staging PASSED to flip them.

### 2.10 Cathedral isolation is real — and easy to reverse

`Lazuar.Pay.csproj` has zero `PackageReference` and zero `ProjectReference`. `IsolationTests` grep host + test csproj + every `src/**/*.cs` for `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`. The focused host is **17** C# files. Old Modules are **784**. The failure mode of 013 is not “we forgot a Hub page.” It is “we added MediatR because checkout got a second handler.”

No `apps/lazuar-pay/Dockerfile`. Root `docker-compose.yml` still builds **Hub** `apps/lazuar-api` as `container_name: lazuar-api` on 8080. `apps/lazuar-pay/README.md`: “Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real.” Production-ready of the **host** includes a compose/image story (paper 03). Production-ready of the **bar** forbids swapping compose **before** S1 dogfood, and forbids swapping by pointing ops at 8081.

### 2.11 Git history that landed on this SHA (so “connected” is not a feeling)

```text
6f866ff0 feat(pay): scaffold merchant and checkout Vite apps
1bd9f338 feat(pay): fixture POST/GET /v1/checkouts with One member gate
18e10d6f docs(012): C99 connected — whoami and org ready shipped
e466a2fe feat(pay): C20-C24 org ready via One authz/check member
811be438 docs(pay): C19 whoami runbook — One on 8080, Pay on 8081
a35a0334 feat(pay): C18 add whoami and org ready to pay-spec
e47ed381 test(pay): C17 widen isolation scans to src and test csproj
9b8a935b test(pay): C15-C16 hermetic whoami and health isolation
83a36dac feat(pay): C13-C14 GET /v1/whoami forwards Bearer to One /me
c30a11fa feat(pay): C12 map One /me JSON to Pay whoami DTO
47589733 feat(pay): C11 register typed HttpClient for One
e938e4a7 feat(pay): C10 bind One BaseUrl and timeout options
56f45080 docs(012): freeze C00 One-to-Pay connect checklists
```

012 is closed as **connected**. 013 starts from a host that already speaks HTTP to One and already refuses Hub CORS. It does not start from a blank `Program.cs`. It also does not start from a product.

### 2.12 Demo vs production — a table that must stay ugly until §6 is true

| Claim someone will make | True on `6f866ff0`? | When it becomes true |
|-------------------------|---------------------|----------------------|
| “Pay is on 8081” | Yes | Now |
| “Pay trusts One over HTTP” | Yes, for whoami/ready/checkout **gate** | Connected (C99) |
| “Merchants sign in through One” | Only if a human pastes an access_token into curl | P10 + NP-ONE-001–005 |
| “There is a merchant UI” | There is an origin and a health probe | Paper 04 |
| “There is a checkout page” | There is an origin and a health probe | Paper 05 |
| “We create checkouts” | In-memory, status open, merchant Bearer | Paper 06/07 |
| “Buyers pay without One” | **No** | NP-CHK-007 |
| “We write a receipt” | **No** | NP-DOC-001 |
| “The journal balances” | **No** | NP-MON-001 |
| “Webhook retry no-ops” | **No** | NP-GW-006 |
| “VIEWER cannot charge” | **No** (and One has no VIEWER) | NP-ONE-021 |
| “This replaces Hub” | **No** | Paper 02 after §6 |

---

## 3. Production-ready definition of done, mapped to 011 IDs

### 3.0 Three bars (do not collapse them)

012/10 already named three bars for **connect**. 013 needs the same honesty one level up:

| Bar | Name | Pass sentence | Status on this SHA | May we call it production-ready? |
|-----|------|---------------|--------------------|----------------------------------|
| **A** | Connected | Pay 8081 consumes One `/me` + `authz/check member` with a Bearer. Health never calls One. No password form. | **Pass** (C99) | **No.** Identity relay + dummy ready + fixture. |
| **B** | First-slice live dogfood | The 011/01 sentence, on the **new** three processes, with fail locks still true. | **Fail** (011/12 steps 1–12 mostly todo) | **Yes — this is the gate this paper defines.** |
| **C** | Product v1 (rest of 011/01 must-have) | Renew, refund-once, SST fail-closed, buyer magic-link portal, remaining mail/audit. After Bar B is boring. | **Fail** | Not required to *start* calling Bar B production-ready. Required before selling “Pay v1 is complete.” Still not Hub. |

**Replace old** ([02](./02-replace-old-cutover.md)) may begin in dual-run **after Bar B**, not after Hub feature-parity, not after Bar C, not after One Okta. Killing Hub `:3003` before MEMBER can see a receipt on `:5178` is vandalism. Waiting to kill Hub until tax invoices and ops chat exist on 8081 is how the cathedral teleports.

Wave rule from 011/10 (still binding):

- Do not start **soon** until S1 `Dogfood = Y` is `done`.
- Do not un-refuse an `NP-XX` row without editing 01-product.md and the schema.
- Old C# tree does not count as `done`.

### 3.1 MUST — Bar B gate (production-ready may be claimed)

A row is **must** for this paper’s gate if the 011 dogfood sentence fails without it, or a 03 fail lock fails without it, or the three apps cannot be pointed at by a human without reversing a lock (ports, Bearer, no Hub cookie).

#### 3.1.1 One façade still required (`NP-ONE`, wave S0)

| ID | Job | Why the gate dies without it |
|----|-----|------------------------------|
| NP-ONE-001 | Register Pay SPA via One `POST /tenants/{id}/apps` (or seed like `lazuar-app`) | Merchant `:5178` cannot start OIDC. Console-only `client_id` is a fail (P10.3). |
| NP-ONE-002 | OIDC code + PKCE; Pay `client_id`; Zitadel authority | No access_token in the browser. Curl-only is a demo. |
| NP-ONE-004 | Redirects on that app + login `REDIRECT_ALLOWLIST` | Login bounces. Not a Console-only allowlist. |
| NP-ONE-005 | Product login via `:5175`; never ship `:3005` or `:5173` | Wrong door. Merchant homepage is `:5178`, not login. |
| NP-ONE-009 | Create workspace = `POST /tenants`; One tenant id **is** `org_id` | Empty `tenants[]` has no Pay-side escape hatch (no second org table). |
| NP-ONE-011 | Copy-link invite + pending + revoke + resend | Second engineer (MEMBER) cannot exist. |
| NP-ONE-012 | Accept-invite; **non-email** accept path | Do not paper over One SMTP with homemade email. |
| NP-ONE-014 | Mint / list / revoke `lzr_sk_` with **explicit** scopes | Workers and later M2M. Empty/`*` is a footgun. |
| NP-ONE-017 | HMAC webhooks: at least `tenant.suspended` / `reactivated` (and `member.*` as cache) | Stop charges if suspended. Pull events if no push. Do not tail Zitadel. |
| NP-ONE-018 | Stop charges (and staff access) on `tenant.suspended` | Money in Pay stays true if webhook is late; **new** charges must fail closed. |
| NP-ONE-021 | VIEWER cannot charge, change keys, or refund | **Pay-enforced** using One role + `authz`. Not `check(member)` alone. |
| NP-ONE-022 | Invited MEMBER can see merchant ops | The last clause of the dogfood sentence. |

Already **done** and must **stay** true: NP-ONE-003, 006, 007, 008, 015 (as “member gate exists,” not as 021).

NP-ONE-010 (PATCH tenant profile), NP-ONE-013 (full roster chrome), NP-ONE-016 (`batch-check` chrome), NP-ONE-019 (provision catalog rows on `tenant.created`), NP-ONE-020 (secrets inventory) are **not** all equal:

| ID | Gate? | Note |
|----|-------|------|
| NP-ONE-010 | **should** (Bar C / soon) | Name/logo can wait if create/pick workspace works. |
| NP-ONE-013 | **should** for chrome; **must** have *enough* roster to see MEMBER vs owner | Dogfood needs two humans with different powers; not a full admin console clone. |
| NP-ONE-016 | **should** | Permission chrome. Do not block charges on batch-check. |
| NP-ONE-019 | **should** | Lazy upsert on first Pay write is allowed (P30.3). |
| NP-ONE-020 | **must** as a **refuse of secrets**, not as a feature | Pay holds OIDC `client_id`, `lzr_sk_`, webhook HMAC. Never Zitadel PAT / FGA admin / masterkey. Already partially true (C-phases: BaseUrl + Timeout only). |

#### 3.1.2 Catalog + checkout + buyer (`NP-CAT`, `NP-CHK`, `NP-BUY`)

| ID | Gate? | Note |
|----|-------|------|
| NP-CAT-001 name | **must** | Dogfood “create a product” |
| NP-CAT-002 prices monthly/yearly | **must** | At least one price. Both intervals not required for the first product. |
| NP-CAT-003 currency MYR | **must** | Fixture already defaults MYR. Keep it. |
| NP-CAT-004 seats | **should** (Bar C / SST) | SST unit × seats is V1 math; first dogfood can be qty=1. |
| NP-CAT-005 merchant ops list/create/edit | **must** | Client of `/v1`, on `:5178`, not ops `:3003`. |
| NP-CHK-004 open → paid / expired | **must** | Fixture is stuck on `open`. |
| NP-CHK-005 hosted buyer page | **must** | `:5179`. |
| NP-CHK-006 shareable pay link | **must** | How the buyer arrives. |
| NP-CHK-007 buyer pays **without** One account | **must** + fail lock | Fail the slice if Zitadel login appears. |
| NP-BUY-001 payer email/name on session | **must** | Receipt mailbox. |
| NP-BUY-002 small payer profile | **should** (Bar C) | Strip of old CRM. Not Zitadel. |
| NP-BUY-003–005 magic link / portal | **should** (Bar C / V1) | After first paid receipt exists. |

#### 3.1.3 Gateways (`NP-GW`)

| ID | Gate? | Note |
|----|-------|------|
| NP-GW-001 encrypted BYOK keys per workspace | **must** | Stripe **or** CHIP/Billplz for dogfood. |
| NP-GW-002 Stripe **or** NP-GW-003 one MY rail | **must (one of)** | Not five adapters. 011/01: one Malaysian rail you will **actually** dogfood. |
| NP-GW-004 webhook verify signature | **must** | |
| NP-GW-005 empty body → 400 | **should** (honesty; cheap; do it with 004) | Dogfood=— but 03 lists empty body = 400 next to the must path. Treat as **must** for the webhook handler you ship. |
| NP-GW-006 idempotent `(tenant, provider, event_id)`; retry no-ops | **must** | Double-journal is a fail lock. |
| NP-GW-007 honest matrix (reminder-only rails never silent debit) | **must** as a **label + code path**, even if only one rail ships | Otherwise Billplz gets treated like Stripe. |
| NP-GW-008 never treat setup / setup-intent as paid | **must** (fail lock) | |
| NP-GW-009 paste/rotate keys in merchant UI; VIEWER cannot | **must** | Ties 021. |

#### 3.1.4 Fulfillment, money, documents, mail, audit, door

| ID | Gate? | Note |
|----|-------|------|
| NP-FUL-001 same handler: subscription/one-off **and** ledger | **must** | Do not wait on One to “hear an event.” |
| NP-FUL-002 buyer access = Pay row, not One grant | **must** | |
| NP-FUL-003 merchant sees payments + subscribers | **must** | `:5178` client of `/v1`. |
| NP-FUL-004 renew job | **should** (Bar C / V1) | After first pay is boring. |
| NP-FUL-005 do not invent PAST_DUE without a failed charge | **should** (Bar C / V1) | |
| NP-MON-001 double-entry journal balanced on first pay | **must** | cash / revenue / tax / fee. |
| NP-MON-002 fee only when PSP sent it (`unknown` ≠ 0) | **should** (do it in the same journal code) | Cheap honesty. |
| NP-MON-003 SST exclusive on unit then × seats | **should** (Bar C / V1) | Steal `SstTaxMath` **judgment**. Qty=1 dogfood can still record tax=0 **only if** merchant SST-registered is known false, not unknown. |
| NP-MON-004 fail closed if SST registration unknown | **should** (Bar C) but **must not undercharge** even in Bar B | If you cannot know, do not ship a price that pretends SST is 0. |
| NP-MON-005–006 refund once / no double-reverse | **should** (Bar C / V1) | |
| NP-DOC-001 `RCPT-…` | **must** | Commercial, not tax. |
| NP-DOC-002 number never UUID; missing = `PENDING` | **must** | Checkout session Guid is not a receipt number. |
| NP-DOC-003 do not title Tax Invoice | **must** + refuse NP-XX-003 | |
| NP-DOC-004 do not print MyInvois VALID | **must** as honesty (even though Dogfood=—) | |
| NP-DOC-005 merchant can open the receipt | **must** | |
| NP-MAIL-001 receipt email | **should** (same process; not a Notify service) | Dogfood=—. Human can open receipt in ops without email for Bar B. Do not stand up SMTP as a Hub clone. |
| NP-AUD-001 audit row on charge, same transaction | **should** (do it in the same handler as FUL-001) | Cheap if the table exists. Not a service. |
| NP-AUD-003 audit on gateway-key change | **should** | |
| NP-API-002 provider webhook URL | **must** | |
| NP-API-004 merchant ops is a client of `/v1` | **must** | No back-door table reads. `:5178` uses Pay `/v1` + One HTTP for roster/invite. |
| NP-API-005 tenant isolation on every route | **must** | Already started (checkout other-org 403). |
| NP-API-006 idempotency on money POSTs | **must** | Persist the fixture idea. |

### 3.2 SHOULD — Bar C (product v1, after Bar B is boring)

011/01 “Must have (v1)” is **broader** than the dogfood sentence. That is intentional. This paper refuses to pretend Bar B is “Pay v1 complete.” It also refuses to hold Bar B hostage to renewals.

| ID | Job | Why it waits |
|----|-----|--------------|
| NP-FUL-004, 005 | Renew / honest PAST_DUE | Needs a second cycle. |
| NP-MON-003, 004, 005, 006 | SST × seats, fail-closed, refund, disputes | Steal judgment from old `SstTaxMath`; do not steal Lhdn module. |
| NP-BUY-002–005 | Payer profile, magic link, update-payment, download receipt | Buyer portal can share `:5179` later (checkout README already says so). |
| NP-MAIL-002, 003 | Failed-pay + magic-link email | In Pay process. |
| NP-AUD-002 | Audit on refund | With refunds. |
| NP-ONE-010, 013, 016, 019 | Profile, roster chrome, batch-check, provision-on-created | Identity chrome, not the charge. |
| NP-CAT-004 | Seats | With SST. |
| NP-SOON-* | **Not Bar C.** After v1 dogfood. | 011/10: do not start soon until S1 Y is done. Quotes, PAST_DUE sequence, partial refunds, second gateway, M2M. |

Bar C still ships **on the same three processes**. It does not add `lazuar-notify`. It does not add Hub invoicing pages.

### 3.3 LATER — not v1, not the gate

| ID | Job | Owner |
|----|-----|-------|
| NP-LAT-001 | Tax **provider**: amount + buyer in; VALID + QR out | vendor |
| NP-LAT-002 | More rails (Razorpay, Xendit) reminder-only, labelled | Pay |
| NP-LAT-003 | Entitlement grant for a **second** Lazuar app via HTTP | Pay |
| NP-LAT-004 | Extract Notify when a second product shares a sending domain | Pay |
| NP-LAT-005 | Audit **feed** API if someone buys a feed | Pay |
| NP-LAT-006 | Enterprise SSO / SCIM / HRD via **One** when a named merchant asks | **One**, not Pay |

NP-SOON-001–008 stay **soon**, not later: custom amount/quote, proforma PDF (not tax invoice), SST on quote matches hop-2, PAST_DUE dunning, one completion does not skip a cycle, partial refunds, M2M checkout, second gateway after the first two are boring.

### 3.4 REFUSE — keep the rows so the museum cannot come back

All **24** `NP-XX-*` rows remain refuse. Production-ready **fails** if any of them ship, even if the dogfood sentence is otherwise green.

| ID | Refuse | Why it shows up as “prod parity” pressure |
|----|--------|---------------------------------------------|
| NP-XX-001 | Homemade LHDN / XML / UBL / consolidation | Hub sold “e-invoice at pay.” Sandbox VALID was **never captured** (00, 09). |
| NP-XX-002 | TIN-at-checkout as a legal feature | Portal still validates TIN (008/07 §16.5). Do not port it. |
| NP-XX-003 | Title receipt Tax Invoice / print VALID without a provider | Dual-use document model. |
| NP-XX-004 | WhatsApp dunning | Vitamin. Hub templates still have WhatsApp fields. |
| NP-XX-005 | Xero | Vitamin. |
| NP-XX-006 | Web3, escrow, CMS, 15-app super-app | |
| NP-XX-007 | Zitadel / OpenFGA / SCIM / password store **inside Pay** | That is One. Pay password form is a 03 fail lock. |
| NP-XX-008 | Dual JWT vs membership roles | Hub cookie `CLIENT` vs JSON `ADMIN` (00). |
| NP-XX-009 | Per-module schemas / inbox as Pay talking to itself | The tax in 00. |
| NP-XX-010 | Debit notes, self-billed 11–14 | Strategy-only lies. |
| NP-XX-011 | Homemade FPX e-mandate | Wrap-rails only. |
| NP-XX-012 | Stripe Billing `subscription.updated` as SoT | |
| NP-XX-013 | Zitadel human per cardholder | Buyer plane is Pay. |
| NP-XX-014 | Second `organizations` table plus One members | |
| NP-XX-015 | FGA types `payment` / `document` with no written check | AUTHZ-05 wants a named consumer. |
| NP-XX-016 | Pay calls One `authz/write` | |
| NP-XX-017 | Pay holds Zitadel PAT / login PAT / OpenFGA admin | |
| NP-XX-018 | Ship merchants to `lazuar-admin` (`:5173`) | Also refuse Hub admin `:3005` as a merchant door. |
| NP-XX-019 | Notify or Audit as a **process** in v1 | |
| NP-XX-020 | Lazuar Media in v1 | |
| NP-XX-021 | Block Pay on npm publish of `@lazuar/one-client` | Workspace import / raw HTTP is enough. |
| NP-XX-022 | Hosted One SKU / Okta / SCIM as the next **Pay** ticket | One staging NOT PASSED; still integrate HTTP. |
| NP-XX-023 | Pay calls `POST /platform/tenants` | Staff directory. |
| NP-XX-024 | Parse Zitadel `urn:zitadel:iam:org:project:roles` | |

### 3.5 Per-app definition of done (the three processes this paper owns)

This is the same Bar B, sliced by deployable.

#### `apps/lazuar-pay` (C# host :8081)

**Must, to call the host production-ready for Bar B:**

1. Still listen **8081** only. Fingerprint remains Pay `/v1/health`, not Hub `/health` (both can return `{status:ok}` — 012/05).
2. Public `/v1` is the only money door. No second app reading Pay tables (Bezos). Merchant UI and checkout UI are HTTP clients.
3. Whoami + member gate remain; money routes add **role** enforcement (NP-ONE-021).
4. Persistent store for checkout, keys (encrypted), journal, receipts, webhook idempotency, audit — **one** Postgres, **one** schema, **no** per-module DbContext (paper 03 owns the seam; this paper owns the *refusal* of nine migration trains).
5. `POST /v1/checkouts` becomes a real session (not only `ConcurrentDictionary`). `status` can become `paid` from a **verified** webhook, never from setup-intent.
6. Provider webhook URL verifies signature; empty body 400; retry no-ops (NP-GW-004–006).
7. Same handler writes access + balanced journal + `RCPT-…` (NP-FUL-001, NP-MON-001, NP-DOC-001).
8. Notify/audit **functions** in this process. No `lazuar-notify` binary.
9. IsolationTests still ban the cathedral. No MediatR.
10. `task pay:test` stays hermetic (fake One). Live One is a runbook, not CI.
11. TypeSpec grows in `packages/pay-spec` only. Do not import LHDN or `/public/commerce` or `/one/auth/*`.
12. CORS stays 5178/5179 (plus the deployed origins of those two apps). Never Hub 3003/3004/3005 as a shortcut.

**Must not:** bind 8080; implement `POST /one/auth/login`; accept `id_token` as Bearer; hold Zitadel PAT; print UUID as receipt number; title Tax Invoice.

**Out of this paper:** exact connection string, k8s probe shape, secret manager (03).

#### `apps/lazuar-pay-merchant` (Vite :5178)

**Must:**

1. Origin stays 5178 locally (`strictPort`). Production origin is **this app**, not `lazuar-ops`.
2. OIDC code+PKCE against Zitadel via One-registered SPA. Login host `:5175`. Send **access_token** as Bearer to Pay `/v1/whoami` and money routes. Never `id_token`. Never password form.
3. Create-or-pick workspace = One `POST /tenants` or membership from whoami. No Pay org table.
4. Screens required by the dogfood sentence only: paste/rotate gateway keys, create product + pay link, list payments, open `RCPT-`, invite/accept is One copy-link (deep-link or One API). MEMBER can see; Pay-enforced VIEWER/read-only cannot change keys or refund.
5. Types from `@repo/pay-types-ts` **when generated** — **not** `@repo/api-types-ts` (Hub). P60: generate pay-types only when this UI calls `/v1` for real.
6. No Hub cookie `lazuar_auth`. No `credentials: "include"` against 8081 as a session mechanism.

**Must not:** clone Sidebar `MODULES` (Commerce / Invoicing / Developer / Workspace); mount `/invoicing/tax-invoices`; mount `/ops/chat`; call `/admin/commerce/*`; set `VITE_API_URL` to 8081 on **ops**.

Steal **judgment** from ops, not routes: BYOK key paste is a real job; SST fail-closed is a real job; “don’t call it Tax Invoice” is a real job. Dashboard-of-five-queries-blocking-paint is not.

#### `apps/lazuar-pay-checkout` (Vite :5179)

**Must:**

1. Origin stays 5179 locally. Production origin is **this app**, not `lazuar-portal`.
2. Buyer completes pay **without** One login, without Pay password, without Zitadel human.
3. Session identified by Pay checkout id (or pay-link token), not Hub `/{tenantSlug}/checkout/{productSlug}` as a compatibility URL you must keep forever (cutover URLs are paper 02).
4. Payer email/name captured (NP-BUY-001).
5. Success/cancel URLs honored once `paid` is real.
6. Receipts / update-payment **may** share this origin later (magic link to payer mailbox). Not the merchant shell.

**Must not:** `GET /v1/whoami` as a required step; TIN-as-legal-feature; MyInvois validate call; cookie session to “see the portal.”

### 3.6 Fail locks (if any fail, Bar B fails even if IDs look green)

From 011/03 and 011/12. Status on this SHA in parentheses.

| Lock | IDs | Now |
|------|-----|-----|
| No Pay password form | NP-XX-007 | hold (no login route on 8081) |
| No second org table | NP-XX-014 | hold (no DB) |
| Buyer is not a Zitadel human | NP-XX-013, NP-CHK-007 | **unproven** (checkout is a health probe) |
| Setup session is not counted as paid | NP-GW-008 | **unproven** (nothing is paid) |
| Receipt not titled Tax Invoice; number not UUID | NP-DOC-002, 003, NP-XX-003 | **unproven** (no receipt); fixture checkout **id** is a Guid — must not become the number |
| Webhook retry does not double-journal | NP-GW-006 | **unproven** |
| Merchant is not sent to `lazuar-admin` | NP-ONE-005, NP-XX-018 | hold in copy; **unproven** in SPA (no redirects yet) |

If a lock fails, do not mark 011/12 steps 1–12 `done`. Do not call Bar B production-ready.

### 3.7 How Status may be flipped (this paper still does not flip)

011/10: flip in **11-checklist.md** (and 12 for ordered steps) when the **job** is proven in new Pay. Connected rows already flipped are the only `done` cells that are legal. 013 implementation (a later program) flips Bar B Y-rows when a human can run the sentence, not when a fixture test is green.

Do not flip NP-ONE-021 when `/ready` is green.  
Do not flip NP-CHK-007 when checkout Vite fetches `/health`.  
Do not flip NP-DOC-001 when session id is a Guid.  
Do not flip NP-API-004 because merchant `package.json` exists.

---

## 4. Why cloning Hub ops / portal / api is the failure mode

The instinct that will kill 013: “production-ready means merchants can do everything they can do on `:3003`.” That instinct is how you rebuild the cathedral with Vite 8 and a prettier kicker.

### 4.1 Why we left (00) — seams, not missing pay features

`00-why-leave.md` is not a mood. It lists bugs that were **module walls**:

- `TaxInvoiceId` dumping ground because Billing, LHDN, and consolidation could not share one document model.
- `InvoiceIssued` subscribed in two modules, constructed in none.
- `ManualPaymentRecorded` looked like cash settlement.
- Hub SaaS PDF sliced a Guid because the handler did not use the numbering helper one folder over.
- Portal tokens were subscription-shaped, so a paid quote with no `Subscriptions` row could not open documents.
- Register said `ADMIN` in JSON and stamped `CLIENT` on the cookie.
- Workers needed `IgnoreQueryFilters` because the “module” ran with an empty tenant.

Those are not product ideas. They are the tax of pretending each folder is a service while sharing one process. Once a module has a schema, DbContext, migration set, outbox, README, architecture test, and parked event, **deleting it feels like deleting a product**. Honesty files become a second product.

Cloning Hub ops into `:5178` **reimports that tax** even if the backend is the small C# host: you will need `/admin/commerce`, `/lhdn`, `/ops/chat`, credits, Hub SaaS plan, TIN, tax invoices — and the host will grow routes until IsolationTests are “too strict” and someone deletes them.

Useful inheritance, restated so 013 cannot “improve” it:

- Exclusive SST on the **unit**, then × seats.
- Fail closed when you cannot decide SST.
- Document number is never a UUID; missing is `PENDING`.
- VALID means a tax system said VALID.
- One role story (on **One** for merchants).
- One write path for cash.
- One database you can migrate without a module README.
- Wrap-rails only (no Stripe Billing `subscription.updated` as SoT).

Leave LHDN, WhatsApp, Xero, homemade e-mandate out until a provider or a later extract has a reason.

### 4.2 How problematic the old tree is (09) — you can charge a card; you must not spend a year inside the shape

`09-old-pay.md` (HEAD considered `main` @ `e7bb07b0`, waves 001–260, issues 261–334 still open):

| Layer | Severity | Meaning for 013 |
|-------|----------|-----------------|
| Shape | High | Nine modules, nine migration trains. A one-line product change is a cross-module case. |
| Scope | High | Homemade MyInvois, homemade identity, messaging, credits, TypeSpec honesty allowlists. Selling “e-invoice at pay” is a lie. |
| Remaining defects | Medium–high **if you ship that binary** | 74 P2s: reset-password email oracle, API key hash vs prefix, cookie vs JWT, TOS checkbox, forwarded-for rate limit. |
| Money math | Medium, contained | SST unit × seats, fail-closed, wrap-rails, Guid not printed as invoice number — **earned judgment**. |
| Tests | Mixed | Green module tests; honesty locks and `[Ignore]` sandbox. Green CI ≠ MyInvois VALID ≠ “no 261.” |

Score: learning artifact **high**; year-two product core **poor**; something to sell as “platforms + Malaysia tax” **unsafe**. The 260 fixes made it *honest enough to leave*. They did not make it a kernel to grow.

If 013’s bar is “close 261–334 on Hub, then retarget ops at 8081,” you have reversed 011 binding #1.

### 4.3 Counts — Hub is a product surface; the new stack is not a mini-Hub

**Old API TypeSpec operations (this SHA):** **152** HTTP ops in `packages/api-spec` route files.

| Spec file | Ops | Prefix the new host must not grow |
|-----------|-----|-----------------------------------|
| `modules/one/routes.tsp` | 39 | `/one/auth/login`, `/one/auth/me`, workspaces, Hub api-keys, Hub webhooks |
| `modules/commerce/admin-routes.tsp` | 39 | `/admin/commerce` products, dunning, subscribers, refunds, coupons, mark-paid |
| `modules/commerce/public-routes.tsp` | 16 | `/public/commerce` checkout, portal, TIN validate, magic-link |
| `modules/billing/routes.tsp` | 13 | `/admin/billing` credits, SaaS checkout, ledger |
| `modules/lhdn/routes.tsp` | 13 | `/lhdn` documents, cancel, taxpayer validate |
| `modules/communications/admin-routes.tsp` | 12 | `/admin/communications` templates, broadcasts |
| `modules/ops/routes.tsp` | 9 | `/ops/chat`, stream, execute-action |
| `modules/platform/routes.tsp` | 5 | `/platform/auth/*`, platform payment-config |
| `modules/commerce/integration-routes.tsp` | 3 | `/integrations/commerce` |
| `modules/payments/routes.tsp` | 3 | `/integrations/payments` |

CRM + messaging: models only. The Hub still has `Modules/CRM` (25 cs) and `Modules/Messaging` (35 cs) **without** a TypeSpec door. That is the museum: code that cannot be deleted because it looks like a product.

**Old ops live routes** (`apps/lazuar-ops/src/App.tsx` 267–313), counted on this SHA — not the 16 Aug 008 line numbers:

Public (8): `/`, `/pricing`, `/signup`, `/login`, `/accept-invite`, `/forgot-password`, `/reset-password`, `/verify-email`.

Authenticated under `OpsLayout` (25):

- Commerce (10): dashboard, products, subscribers, transactions, disputes, coupons, dunning-campaigns, dunning new, dunning `:id`, templates.
- Developer (3): api-keys, webhooks, logs.
- Workspace (9): general, team, audit, billing-profile, payment-gateways, email, billing, ledger.
- Invoicing (3): quotes, tax-invoices, credit-notes.

Catch-all `*` → `NotFoundPage`. **Ops chat is still commented** (`[MVP-HIDE] ADR 023`, lines 306–308). 008/07 said the same. A “parity” bar would eventually unhide it; this bar **refuses** it (vitamin, and `/ops/*` is not Consumer-0).

Sidebar in 008/07: four accordions — Commerce, Invoicing, Developer, Workspace. Brand string **“Lazuar Console.”** No role chip (008: the layout fetched `entitlement.role` and ignored it; current `App.tsx` now threads `workspaceRole` into `OpsOutletContext` — Hub moved; **do not** port Hub ADMIN/MEMBER/VIEWER vocabulary onto One owner/admin/member as if they were the same enum).

Ops talks to Hub with `credentials: "include"` and `X-Tenant-Id` (`apps/lazuar-ops/src/lib/api-client.ts`). One’s hint header is `X-Lazuar-Tenant-Id`. New Pay must not authorize by either header alone (NP-ONE-007). Pointing this client at 8081 means: cookie session against a host that has no cookie JWT, typed calls to `/one/auth/me` that **do not exist**, and a tenant header the new host must not treat as SoT. P60.2 already said this. It is still true.

**Old portal** — 12 `page.tsx` files:

| URL | File |
|-----|------|
| `/` | `src/app/page.tsx` |
| `/legal/{terms,privacy,refund}` | `src/app/legal/**` |
| `/{tenantSlug}` | `[tenantSlug]/page.tsx` |
| `/{tenantSlug}/checkout/{productSlug}` | checkout page |
| `…/success` | product success + custom success |
| `/{tenantSlug}/pay/{sessionId}` | remounted QuoteView (008/07 §16.7) |
| `/{tenantSlug}/portal` | buyer dashboard |
| `/{tenantSlug}/update-payment/{subId}` | |
| `/accept-invite` | |

008/07: TIN unhidden and **blocking** on MyInvois validate; trial days can zero-out amount due today; EN/BM i18n; Hub cookie *or* `?token=` magic link. Cloning portal into `:5179` is how TIN-as-legal-feature (NP-XX-002) returns.

**Old admin** (`apps/lazuar-admin/src/App.tsx`): `/login`, `/` → `/platform/gateways`, `/platform/gateways`, `*`. Cookie `lazuar_admin_auth` via `GET /platform/auth/me`. Brand **“Platform Control.”** One nav item. 008/07: leftover shadcn, unused chat types. **Never** a Pay merchant destination. Collides in folklore with One Login V2 **`:3005`** (break-glass) and Hub admin **`:3005`**. Production-ready merchant path uses **none** of those ports.

**Old frontend ports (mprocs + vite + next):** developers 3002, ops **3003**, portal **3004**, admin **3005**. New: merchant **5178**, checkout **5179**. One: admin 5173, app 5174, login 5175. The production-ready port map is the **new** column, not a Caddy path-prefix that keeps `/` = ops, `/portal` = portal, `/admin/` = Hub admin.

**Source weight:** focused host **17** `.cs` vs Hub modules **784** `.cs`. Merchant+checkout `src/` is **8** files (4+4), vs ops **122** `.tsx`. If 013 ends with 5178 importing 122 tsx files retargeted at 8081, Hub parity has won.

### 4.4 008-evals/07 — Hub UI honesty (live ≠ file exists)

008/07’s honesty rule: a surface is **live** only if a human can reach a mounted route and click a control. Backend behind 403, swallowed empty table, or unrouted file is not shipped UI.

That rule applies **to 013 in reverse**: a health probe on 5178 is not merchant ops. A fixture POST is not checkout. A commented `/ops/chat` is not a missing production feature; it is a correctly unshipped vitamin.

008/07 also recorded Hub diseases not to clone:

- Dual cookie realm in the **API** (`lazuar_auth` vs `lazuar_admin_auth`), not in the UI.
- Ops `GET /one/auth/me` then `GET /one/me/entitlements`. New Pay: `GET /v1/whoami` (One `/me`) and path membership. No entitlements product (Hub SaaS plan).
- Invoicing accordion: quotes, **tax invoices**, credit notes — ADR 021 tax moat. 011/01: ADR 023 “just checkout” is the product; ADR 021 stays **out**.
- Portal TIN + MyInvois validate as checkout UX.
- Admin is not a tenant directory; it is Hub’s own processor keys for SaaS + credits. New Pay has **no** Hub SaaS credits. Platform payment-config is not a merchant job.

### 4.5 Hub cookie IdP vs One Bearer — the retarget footgun

P60.2 / 012/10 F10:

- Ops `POST /one/auth/login` is Hub homemade IdP. Pay must not implement it.
- Ops `GET /one/auth/me` is not One `GET /me`.
- Hundreds of `/admin/commerce`, `/lhdn`, `/ops/chat` routes are not Consumer-0.

`AuthAndCorsExtensions.cs` (Hub): if path starts with `/api/v1/platform`, cookie name is `lazuar_admin_auth`; else `lazuar_auth`. New Pay has **no** cookie authentication. Merchant SPA holds an access_token (memory / sessionStorage pattern like One `lazuar-app`, not a Hub cookie). Checkout holds **no** staff token.

If 013 “production-ready” is defined as “ops still works,” someone will add cookie JWT to 8081 to make `credentials: "include"` succeed. That is NP-XX-007. It is also how `ADMIN` vs `CLIENT` comes back (00).

### 4.6 Compose and mprocs still boot the museum

`docker-compose.yml` `api` service: `dockerfile: apps/lazuar-api/Dockerfile`, port **8080**, image name `lazuar-hub-api`. Frontends profile `full`: ops 3003, portal 3004, admin 3005, all with `VITE_API_URL` / `NEXT_PUBLIC_API_URL` → **8080/api/v1**.

`mprocs-dev.yaml` does not list `task pay:dev`, `pay:merchant`, or `pay:checkout`.

Production-ready does **not** mean “add merchant to mprocs and keep ops.” It means a later compose (03 / 10) runs **One’s** identity stack + **Pay** 8081 + two UIs. Hub `task dev` is **off** when One owns 8080 (already the C19 runbook). Dual-run of Hub+new for cutover is paper 02, and it is painful **because** of the 8080 collision — that pain is a reason to cut over, not a reason to merge hosts.

### 4.7 Chat, LHDN, Hub cookie — three “parity” magnets

| Magnet | Hub evidence | 013 answer |
|--------|--------------|------------|
| Ops chat | TypeSpec 9 `/ops/*` routes; UI still `[MVP-HIDE]` | Refuse. Not on the dogfood sentence. |
| LHDN / tax invoices | 13 `/lhdn` ops + portal TIN + ops Invoicing accordion | Refuse homemade; later a **provider**. Receipt ≠ tax invoice. |
| Hub cookie + password | `/one/auth/login`, dual cookies | Refuse. One `:5175` + Bearer. |

Any production-ready checklist that includes those three as “MVP gaps vs Hub” is the failure mode in writing.

---

## 5. What “replace old” is NOT

Paper 02 owns cutover mechanics. This paper owns the **bar** so 02 cannot define success as Hub parity.

**Replace old is not:**

| Counterfeit success | Why it is counterfeit |
|---------------------|----------------------|
| Feature-for-feature with `lazuar-ops` 25 authenticated routes | Most of those routes are cathedral chrome (credits, Hub SaaS, tax invoices, dunning campaign builder, notification templates with WhatsApp fields). |
| Feature-for-feature with `packages/api-spec` 152 ops | New `pay-spec` should stay a **short** `/v1` list. Growing it to 152 is Modules-by-OpenAPI. |
| LHDN sandbox VALID | Never captured on Hub (00, 09, NP-XX-001). Not a Pay v1 product. |
| TIN-at-checkout | Legal e-invoice theater (NP-XX-002). Portal still does it (008/07). |
| Ops chat / execute-action | ADR 023 hide; vitamin. |
| Hub cookie session on 8081 | Second IdP (NP-XX-007). |
| Retarget `lazuar-ops` `VITE_API_URL` to 8081 | P60 refuse. Types are Hub. Auth is Hub. CORS currently **denies** 3003. |
| Retarget `lazuar-portal` `NEXT_PUBLIC_API_URL` to 8081 | Same. Checkout origin is 5179. |
| Ship merchants to Hub admin `:3005` or One admin `:5173` | NP-XX-018. |
| “Connected” (whoami 200) | Bar A. Already true. Not money. |
| Closing issues 261–334 on `apps/lazuar-api` | 011: not a plan to implement them on the old tree. |
| One staging PASSED / Okta / SCIM / npm publish | NP-XX-021, NP-XX-022. One’s honesty, not Pay’s next ticket. |
| Mega-merge One into Pay so whoami is `foo()` | 011/13. One is the justified extract. |
| Five-deploy Notify/Audit/Media/Pay/One | 011/13. |
| Go rewrite of the host | 011/05 is a kernel opinion. **Out of this program.** |
| Greenfield second org table “just for Pay billing profile” | NP-XX-014. Billing profile fields that are **money** (SST registered yes/no) are Pay columns **keyed by One tenant id**, not a membership directory. |
| Compatibility `/one/*` shim on 8081 so old OpenAPI clients keep working | That shim **is** Modules/One. |

**Replace old is:**

- Bar B lived on 8081 + 5178 + 5179.
- Hub 8080 / 3003 / 3004 / 3005 become **reference + dual-run leftover**, then dark (02, 10).
- Judgment (SST, wrap-rails, receipt numbering) visible in the new money handler, not as copied folders.

---

## 6. Minimum live dogfood that may be called production-ready (the 011 sentence)

Restated as an **executable** loop on the new stack. This is Bar B. If a step is skipped, do not use the word production-ready.

### 6.1 The sentence (011/01, 011/03, 011/12)

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

### 6.2 Ordered loop (011/12) mapped onto the three apps

**One side (stop after this) — still required, still not a Pay-owned staging program:**

| Step | Job | Where the human is | IDs |
|------|-----|--------------------|-----|
| 1 | Register Pay SPA through One (`POST …/apps` or seed like `lazuar-app`) | One API / maybe a One seed script (P40 only if Pay cannot self-register) | NP-ONE-001, 002, 004 |
| 2 | Sign-in via **`:5175`**. Access token as Bearer. `GET /me` (via Pay `GET /v1/whoami`) | Login 5175 → merchant 5178 → Pay 8081 → One 8080 | NP-ONE-003, 005, 006 |
| 3 | Create workspace = `POST /tenants` (or pick membership). One tenant id is `org_id` | Merchant 5178 calling **One** (not Pay org CRUD) | NP-ONE-007, 009 |
| 4 | Invite a second engineer with One **copy-link**; non-email accept | One invite; merchant may deep-link | NP-ONE-011, 012, 022 |
| 5 | Mint scoped `lzr_sk_`; `authz/check` before merchant admin routes | One mint; Pay gate already exists for `member` | NP-ONE-014, 015 |
| 6 | Subscribe to `member.*` and `tenant.suspended`; stop charges if suspended | Pay webhook receiver on **8081** | NP-ONE-017, 018 |
| 7 | **Stop** on the One side | No SCIM, no custom FGA types, no npm publish, no hosted SKU | NP-XX-015, 021, 022 |

**Pay side (money):**

| Step | Job | Where | IDs |
|------|-----|-------|-----|
| 8 | Store BYOK Stripe **or** CHIP/Billplz keys, encrypted; VIEWER cannot change | Merchant 5178 → Pay `/v1` | NP-GW-001, 002 or 003, 009 |
| 9 | Create a product + pay link (MYR) | Merchant 5178 | NP-CAT-001…005, NP-CHK-006 |
| 10 | Buyer (no One account) pays on the hosted page | Checkout **5179** | NP-CHK-005, 007, NP-BUY-001 |
| 11 | Webhook verifies; retry no-ops; subscription + balanced journal + `RCPT-…` in **one** transaction | Pay 8081 only | NP-GW-004, 006, NP-FUL-001, NP-MON-001, NP-DOC-001, 002, NP-API-002 |
| 12 | Merchant sees payment and receipt. VIEWER cannot change keys or refund | Merchant 5178 | NP-FUL-003, NP-DOC-005, NP-ONE-021, 022, NP-API-004 |

Pass = 011/01 dogfood test. Fail = 03 locks (password, second org table, Zitadel buyer, setup as paid, Tax Invoice / UUID number, double-journal, merchant sent to admin).

### 6.3 What “may be called production-ready” does **not** include (even after the sentence is green)

- Renewals, PAST_DUE dunning, partial refunds, quotes, second gateway (soon / V1).
- Buyer magic-link portal (V1) — first receipt can be opened by the **merchant**.
- Tax provider, SCIM, npm, hosted One SKU.
- Hub pages that never appear in the sentence.
- One staging PASSED.

You **may** call Bar B production-ready for **first-party dogfood** (Lazuar charging a real test card on the new stack). You may **not** call it “Hub replaced” until paper 02’s kill criteria (that paper). You may **not** call it “Pay v1 complete” until Bar C.

### 6.4 VIEWER in the sentence — implement without waiting for One to grow a role

The sentence says VIEWER. One will not grow `viewer` on type `tenant` for this program (C24, NP-XX-015). Production-ready implementation:

1. Read One `role` from whoami (`owner` / `admin` / `member`).
2. Pay policy: only `owner` and `admin` may paste keys, refund, or trigger charges from ops. `member` is read-only on money (see ops, cannot charge). That is the **Pay** meaning of VIEWER until One adds a staff read-only role.
3. Still call `authz/check` for the relation you mean (`member` to enter the org; `admin` or `owner` for writes). Do not invent FGA type `payment`.
4. Do not map Hub `VIEWER` string into One. Do not parse Zitadel project roles.

If product later needs a fourth staff power, that is an One conversation with a **written** check site. It is not a Hub `WorkspaceStaffRoles` enum copy.

### 6.5 Language of the loop

The loop runs on **C# net10** `apps/lazuar-pay` + two Vite apps. A parallel Go host is **out of this program**. If a future program starts a Go kernel, it does not redefine Bar B as “rewrite first.”

---

## 7. Staging vs production vs local (One staging NOT PASSED — do not block Pay)

### 7.1 Three environments, three honesty rules

| Env | One | Pay | Allowed to call it |
|-----|-----|-----|--------------------|
| **Local laptop** | Compose/scripts in **lazuar-one**: API **8080**, login **5175**, app **5174**, Zitadel **8085**, OpenFGA **8090**. `prove-local-stack.sh` is **not** staging (012/10). | `task pay:dev` **8081**; `pay:merchant` **5178**; `pay:checkout` **5179`. Hub `task dev` / compose `lazuar-api` **off** (8080 collision). | Connected (already). Bar B when the sentence runs against test rails. |
| **Pay staging** (shared host) | HTTP façade the Pay staging process can reach. **Must not wait** on `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/008-next/STAGING-PROOF-STATUS.md` flipping to PASSED. | Real Postgres, real secrets, real origins, one Pay process, `/v1`. Still first-party. | Bar B if the sentence runs with isolation 403 across two One tenants. |
| **Production** | First-party One that Pay already dogfoods; still not “sell One as WorkOS.” | Same topology as staging; wrap-rails live keys BYOK; no homemade LHDN. | Bar B for first-party; Bar C before “complete product.” |

### 7.2 What One STAGING-PROOF-STATUS.md actually says (`0f79fe4`)

Binary result: **NOT PASSED**. Date on the file: 2026-08-10. Human steps 1–15 open (named owner, secrets in vault, VM/DNS, OpenFGA bootstrap, smoke script against live, two humans on customer script, isolation matrix, scoped API key, OIDC HTTPS redirect, staff script, evidence pack). Exit criteria table: every row **NOT DONE** / **NOT PASSED**.

017-08 header on One: unpublished `@lazuar/one-client` / `one-react` / `one-cli`; **no public hosted SKU**. Error table in 017-08 §1.1: treating WorkOS SSO/SCIM as the next dogfood ticket is refused; treating npm publish as dogfood is refused; treating hosted SKU as the next honesty move is refused; treating “Ada created a workspace once” as dogfood complete is refused.

011 binding #5: *One’s staging proof is NOT PASSED. Integrate the HTTP façade anyway; do not pretend Okta/SCIM is the next Pay ticket.* NP-XX-022.

**Therefore:** Pay production-ready **must not** have a blocker column “waiting on One Okta.” Pay **may** be blocked on One **HTTP** being reachable (login, `/me`, `authz/check`, invites, keys, webhooks). Those already work on a laptop. Staging One is One’s program. Pay staging can sit on the same laptop-grade One **or** a shared One that is still NOT PASSED, as long as the façade is up and isolation 403 is real.

### 7.3 Local topology (production-ready still uses this shape)

From 012/05 and One ports, plus new Vite pins:

| Service | Port | Who in Bar B |
|---------|------|----------------|
| **lazuar-api (One)** | **8080** | Pay backend → `/api/v1/…` |
| **lazuar-pay (focused)** | **8081** | `/v1` money + whoami |
| lazuar-admin (One staff) | 5173 | **Never** merchants |
| lazuar-app | 5174 | Optional accept-invite; not Pay homepage |
| **lazuar-login** | **5175** | Product sign-in. Not Pay homepage. |
| Login BFF loopback | 5176 | Dev only |
| **lazuar-pay-merchant** | **5178** | Staff shell |
| **lazuar-pay-checkout** | **5179** | Buyer pay |
| Zitadel | 8085 | Token issuer. Pay holds **no** PAT. |
| Zitadel Login V2 stock | 3005 | Break-glass. Collides with Hub admin 3005. Do not ship merchants here. |
| OpenFGA | 8090 | One ops. Pay never holds store admin. |
| **Hub** lazuar-api | 8080 | **Off** when One is on 8080 |
| Hub ops / portal / admin | 3003 / 3004 / 3005 | Museum. Not Bar B. |

Fingerprint One: `GET http://localhost:8080/api/v1/` names `lazuar-one-api`. Do not trust `/health` alone.

### 7.4 Staging Pay is not “Hub compose with VITE_API_URL flipped”

Illegal staging:

- `docker-compose.yml` as-is + env `VITE_API_URL=https://pay-staging/api/v1`.
- Caddy path map `/` → ops, `/portal` → portal, `/admin/` → Hub admin, API → 8081.
- One SCIM against Okta as a Pay milestone.

Legal staging:

- Deploy **focused** `lazuar-pay` (paper 03).
- Deploy merchant + checkout static (04, 05) with `VITE_PAY_API_URL` pointing at Pay `/` (host already serves `/v1/...`; do not invent `/api`).
- Point Pay `One:BaseUrl` at whichever One HTTP is actually up.
- Keep Hub dark or on a **different** host/port for dual-run (02).

### 7.5 Production is Bar B with real blast radius, not Bar C and not One-as-SKU

Production adds: secrets not in git, encrypted BYOK, TLS, backups of **one** Pay DB, webhook endpoints reachable by Stripe/CHIP, HMAC for One events, on-call for **Pay money** (not for Hub chat). It does not add: LHDN, WhatsApp, five adapters, Notify as a fleet, Media.

If production is blocked on One evidence pack (D102 two humans, etc.), Pay will wait forever on a sibling’s beta-gate. 011 already forbade that. First-party production-ready means **Lazuar can charge on the new stack**. Stranger-ready One is a different bar (017-08 “serve others”).

---

## 8. Anti-goals

Deleting a row is how the museum comes back. This table is the program.

| Do not | Do instead | Why | Tracker / lock |
|--------|------------|-----|----------------|
| Define production-ready as Hub ops parity (25 routes, 152 TypeSpec ops, 784 module files) | Define it as the 011 sentence on 8081+5178+5179 | 00, 09, 008/07 | this paper §3–§5 |
| Retarget `lazuar-ops` / `lazuar-portal` `VITE_API_URL` to 8081 | New UIs; leave Hub on 8080 until killed | P60; CORS denies 3003 | NP-API-004 later, NP-XX-007 |
| Stub `POST /one/auth/login` (or `/v1/auth/login`) on Pay | No login route. `:5175` + Bearer | 03 fail lock | NP-XX-007 |
| Cookie JWT `lazuar_auth` on 8081 | access_token in the merchant SPA | Dual cookie realm is Hub | NP-XX-007, NP-XX-008 |
| Send `id_token` as Bearer | access_token only | NP-ONE-003 | NP-ONE-003 |
| Bind Pay to **8080** | **8081** | Collision with One and Hub | 012 decisions |
| Ship merchants to `:5173` or `:3005` | `:5178` homepage, `:5175` login | NP-ONE-005 | NP-XX-018 |
| Clone `/invoicing/tax-invoices`, TIN validate, `/lhdn/*` | Receipt `RCPT-`; tax later = provider | 00, 01 | NP-XX-001–003 |
| Unhide ops chat on 5178 | Leave it dead | ADR 023; vitamin | NP-XX-006 family |
| `check(member)` as NP-ONE-021 | Pay policy on One role + stricter check | C24 | NP-ONE-021 |
| Map Hub VIEWER → One `member` | See §6.4 | 012/07 | NP-ONE-021 |
| Add FGA types `payment` / `document` | Written check site later, named consumer | AUTHZ-05 | NP-XX-015 |
| Pay `authz/write` | Never | | NP-XX-016 |
| Hold Zitadel PAT / FGA admin | `client_id` + `lzr_sk_` + webhook HMAC | 011/02 secrets table | NP-XX-017 |
| Second org/users table | One tenant id is `org_id` | | NP-XX-014 |
| Buyer as Zitadel human | Payer profile in Pay | | NP-XX-013, NP-CHK-007 |
| Count fixture `status: open` as paid | Webhook + journal + `RCPT-` | P50 | NP-CHK-004, NP-GW-008 |
| Print checkout Guid as receipt number | `RCPT-…` or `PENDING` | Fixture id is Guid.N | NP-DOC-002 |
| Stripe Billing `subscription.updated` as SoT | Wrap-rails; Pay journal | | NP-XX-012 |
| Homemade FPX e-mandate | Wrap-rails | | NP-XX-011 |
| Five adapters on day one | Stripe **or** one MY rail | 01-product | NP-GW-002/003 |
| `lazuar-notify` / `lazuar-audit` processes | Functions + table in Pay | 06, 07, 13 | NP-XX-019 |
| Mega-merge One into Pay | HTTP to One | 13 | locked |
| Five-deploy | One process + existing One | 13 | locked |
| Go rewrite **in this program** | Ship `apps/lazuar-pay` net10 + two Vite apps | 011/05 is out of band | §0, §10 |
| MediatR / `Modules.*` / `BuildingBlocks` / `Lazuar.slnx` | IsolationTests stay green (no banned strings) | 05-language gravity | IsolationTests |
| Hook `packages/pay-spec` into Hub `task gen` / honesty allowlist | `task pay:spec` only | 012/04 | — |
| Block on npm `@lazuar/one-client` | C# HttpClient; SPA may use workspace pack later | | NP-XX-021 |
| Block on One Okta/SCIM/hosted SKU | Integrate HTTP now | | NP-XX-022 |
| `POST /platform/tenants` | `POST /tenants` | Staff directory | NP-XX-023 |
| Parse Zitadel project-role claims | `/me` + `authz/check` | | NP-XX-024 |
| Hammer `/me` from middleware / health | Whoami endpoint only | GET `/me` can write | NP-ONE-006 |
| Authorize by `X-Lazuar-Tenant-Id` or `X-Tenant-Id` | Path `{orgId}` | | NP-ONE-007 |
| Tail Zitadel for membership | One HMAC webhooks | | NP-ONE-017 |
| Implement 261–334 on `apps/lazuar-api` | Leave; steal judgment | 011 README | — |
| Clerk/Better Auth “until One is ready” | One HTTP now | Staging NOT PASSED is not a third IdP license | NP-XX-007 |
| Start **soon** rows before S1 Y is done | 011/10 wave rule | Quotes are not the first receipt | NP-SOON-* |
| Un-refuse NP-XX without editing 01-product.md | Keep the refuse table | 011/11 how-to | NP-XX-* |
| Call Bar A (C99) production-ready | Use this paper’s Bar B | 012 README | — |
| Wait for papers 02–10 to be implemented before stating the bar | This file **is** the bar | 013 README | — |

---

## 9. Handoff to papers 02–10

Do not treat these names as content. Do not implement them from this file. Analyses stay uncondensed; this README/index must not swallow them.

| File | Owns | What this paper already locked for them |
|------|------|------------------------------------------|
| [02-replace-old-cutover.md](./02-replace-old-cutover.md) | Kill criteria for old API + UIs; dual-run | Success ≠ Hub parity. Earliest kill: after Bar B. 8080 collision is a cutover fact. Do not retarget ops/portal. |
| [03-host-production-seams.md](./03-host-production-seams.md) | DB, config, secrets, health, deploy of `lazuar-pay` | One process, one schema, 8081, no Dockerfile yet, no MediatR, notify/audit in-process, `One:BaseUrl` + timeout, never PAT. |
| [04-merchant-frontend.md](./04-merchant-frontend.md) | `:5178` OIDC, whoami, steal judgment not ops routes | P10.2 is the start. Screens = dogfood sentence only. No `@repo/api-types-ts`. No password form. VIEWER is Pay policy. |
| [05-checkout-frontend.md](./05-checkout-frontend.md) | `:5179` hosted pay; no Zitadel | NP-CHK-005/007. Fail if login appears. Not `lazuar-portal`. TIN out. |
| [06-money-rails.md](./06-money-rails.md) | BYOK, Stripe/CHIP, webhooks, wrap-rails | One rail + Stripe **or** one MY rail. Signature verify. Retry no-ops. Setup ≠ paid. Honest reminder-only matrix. |
| [07-fulfillment-ledger-docs.md](./07-fulfillment-ledger-docs.md) | Same-handler journal + `RCPT-`; SST judgment | NP-FUL-001, NP-MON-001, NP-DOC-001–003. Steal `SstTaxMath` judgment not Lhdn. UUID session id ≠ receipt number. |
| [08-one-identity-production.md](./08-one-identity-production.md) | SPA, `lzr_sk_`, HMAC, `tenant.suspended` | NP-ONE-001–022 minus already-done. Stop on One side at step 7. Do not wait on staging PASSED. |
| [09-data-migration.md](./09-data-migration.md) | What to migrate vs greenfield; no second org table | One tenant id is `org_id`. Greenfield money is allowed; Hub GlobalUser is **not** the merchant SoT. |
| [10-ci-observability-decommission.md](./10-ci-observability-decommission.md) | Tests, staging, compose, when Hub goes dark | Hermetic `pay:test`; compose currently Hub; swap after Bar B; Hub honesty CI stays off pay-spec. |

Implementation of 01–10 is a **later** program (checklists, not this folder). This paper does not become a mega-PR plan.

---

## 10. Open questions this tree did not close

These are **not** invitations to reverse §0. They are questions 02–10 must answer with evidence, or a later checklist must pick.

1. **Production hostnames and TLS** — no Pay Dockerfile, no Caddy stanza for 8081/5178/5179, no `https://api.lazuar.com` that is *this* host (Hub compose still `lazuar-hub-api`). Paper 03/10.
2. **Which Malaysian rail is the first dogfood** — 011/01 says CHIP **or** Billplz, not both, not five. No code on 8081 picks. Paper 06. Until picked, Bar B cannot run step 8–11 for real.
3. **SST-registered as a Pay column** — fail-closed needs a yes/no/unknown on the **org_id**. That is not a second membership table. Where it is collected (merchant 5178) is paper 04+07. Unknown must not undercharge even in Bar B.
4. **VIEWER product copy** — whether the merchant UI says “Viewer” (Hub word) or “Member (read-only on money)” (One word). Policy is locked (§6.4); **label** is not.
5. **Invite UX host** — 011/02 allows deep-link to `lazuar-app` `/invites/accept` **or** post the same API from Pay. Bar B needs one non-email path. Which origin is paper 04/08.
6. **Pay-types package timing** — 012/04 said do not generate `@repo/pay-types-ts` until a UI calls `/v1` for real. Merchant still only calls `/health`. Paper 04 should generate when whoami is called from the browser, not before, and not by hijacking `task gen`.
7. **Fixture store replacement** — `CheckoutStore` is in-memory; process restart loses sessions. Bar B needs persistence (03+06). Whether the fixture route stays as a test double or is replaced in place is an implement detail; **status: open without a rail must not be sold as paid**.
8. **Webhook path name** — P30.1 drafted `POST /v1/one/webhooks` for **One** HMAC events. Provider (Stripe/CHIP) webhooks are a different URL (NP-API-002). Do not collapse them into one handler that cannot tell HMAC schemes apart. Paper 06 vs 08.
9. **Compose swap vs dual-run** — README already says swap later when S1 is real. 8080 cannot hold One and Hub at once on one laptop. Staging topology if someone still needs Hub running is paper 02/10, not a reason to merge listeners.
10. **mprocs / `task fe`** — still Hub-only. Developer UX for Bar B local (One + Pay + two Vites + login) is paper 10, not a new Hub proc.
11. **Receipt PDF vs JSON** — 011/01 wants Official Receipt `RCPT-…`. Old tree used QuestPDF. Whether Bar B’s “shows one `RCPT-`” is a numbered HTML/JSON the merchant can open, or a PDF, is paper 07. Title and numbering rules do not wait on PDF.
12. **Mail in Bar B** — NP-MAIL-001 is Dogfood=—. This paper left it **should**. If staging has no SMTP (One’s MEM-10 problem is **One’s**), Pay must not block Bar B on Resend. Do not clone Hub “Email Provider” as a hard gate for the first card (008/07 said ops treated Resend as a gate for paid checkout — that is Hub gravity).
13. **Migration of Hub merchants** — whether any Hub `Organization` id can equal a One tenant id (almost certainly **no**). Paper 09. Greenfield is allowed. A mapping table that becomes membership SoT is NP-XX-014 in disguise.
14. **Go kernel** — 011/05 still exists. This program **does not** start `cmd/pay`. If someone wants Go, that is a **different program** after Bar B, not a rewrite of 013’s host mid-flight.
15. **One `app.viewer` vs Pay VIEWER** — still a naming trap in docs. Closed as policy (C24); still open as “will One ever add tenant read-only.” Pay must not wait.
16. **Stale docs on this SHA** — `Taskfile` `pay:test` blurb; `pay-spec/README.md` “grow when checkouts exist.” Hygiene, not product. Mentioned so 03/10 do not treat those blurbs as inventory.
17. **012/10 vs decisions.md whoami shape** — implemented `tenants[]` per freeze, not `orgs[]`. Closed for this host. Do not “fix” it in 013 as a breaking rename unless 04’s SPA has not shipped yet **and** C00 is amended. This paper does not amend C00.
18. **Whether Bar C SST/refunds are required before Hub goes fully dark** — this paper says **no** for first-party dogfood, **yes** before claiming product-complete. Paper 02 must pick a kill switch that does not secretly require tax invoices.
19. **CHIP/Stripe account ownership** — BYOK means the **merchant’s** keys, not Lazuar platform processors (Hub admin `/platform/gateways` is Hub’s own processors for SaaS + credits). Confirm in 06 that we are not porting platform-payment-config as a merchant feature.
20. **Live proof on 21 Aug 2026** — this paper did not boot the stack. Open until a human runbook (08/10) records whoami + (later) the full sentence against the SHAs then current.

---

## Appendix A — Port collision cheat sheet (keep)

| Port | New / One | Old Hub | Rule |
|------|-----------|---------|------|
| 8080 | One API | Hub API | Never both. Pay never binds it. |
| 8081 | **Focused Pay** | (unused historically) | Pay only |
| 3003 | — | ops | Do not CORS this to 8081 |
| 3004 | — | portal | Not checkout |
| 3005 | One Login V2 break-glass | Hub admin | Not merchants |
| 5173 | One staff admin | — | Not merchants |
| 5174 | One customer app | — | Not Pay homepage |
| 5175 | One product login | — | Login, not homepage |
| 5178 | **Pay merchant** | — | Staff shell |
| 5179 | **Pay checkout** | — | Buyers, no One account |

## Appendix B — 011/11 done rows (do not re-litigate)

S0 done: NP-ONE-003, 006, 007, 008, 015.  
S1 done: NP-CHK-001, 002, 003; NP-API-001, 003.

Everything else in S0/S1 that is `Dogfood = Y` is the Bar B gap. Counts: 10 done / 81 todo / 24 refuse / 115 total.

## Appendix C — Explicit “not this paper” (one more time)

This file does not: implement OIDC; pick CHIP vs Billplz; design the journal schema; write a Dockerfile; flip tracker cells; cut DNS; migrate Hub rows; start a Go module; merge One; un-refuse LHDN; add CORS for `:3003`; generate `@repo/pay-types-ts`; stand up Notify.

It defines the **bar**. Hub parity is the **failure mode**. The 011 sentence is the **only** production-ready gate the new stack is allowed to use.
