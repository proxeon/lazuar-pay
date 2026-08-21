# 10 — CI, observability, staging, and decommissioning Hub DX

**Date:** 21 August 2026  
**Slice:** program 013 — tests, staging, compose, when Hub goes dark  
**Kind:** analysis only. No C# implementation. No Vite product-code change. No flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. No GH workflow edit. No compose swap.  
**Branch at analysis:** `feat/012-connect-one`

**Repos / HEAD**

| Repo | Path | Short SHA | Full SHA | Tip |
|------|------|-----------|----------|-----|
| Focused Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6f866ff0` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21) |
| Lazuar One (sibling, HTTP SoT) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP: Thu Aug 20 21:24:22 +08 2026` |

`git rev-parse HEAD` / `--short` / `git log -1` / `git branch --show-current` were taken from sibling [01](./01-production-ready-bar.md) on this write date. Pay tip is on `feat/012-connect-one`. One tip is still the 012 pin (`main`). If either tree moves, re-pin before treating job names, test counts, or compose service lists as frozen.

**What this paper owns**

- Whether GitHub Actions ever builds `Lazuar.Pay.slnx` or the two Vite shells.
- What `task pay:test` is allowed to mean (hermetic) vs live whoami vs Playwright later.
- Minimum logs / probes / metrics for a **new** host that is not Hub `/health/metrics`.
- Staging topology that does not fight One for **8080**.
- Developer DX **after** replace: `task pay:dev` + merchant + checkout + One — not `task dev` / `task fe` / `pnpm dev`.
- Ordered decommission of Hub CI, GHCR, compose, Taskfile, mprocs, tunnels — with **not before** gates into papers 01–09, especially 01–02.

**What this paper does not own**

- The production-ready **sentence** ([01](./01-production-ready-bar.md)).
- Kill criteria per old artifact and dual-run shapes ([02](./02-replace-old-cutover.md)) — this paper **sequences DX/CI kill**, it does not rewrite 02’s tables.
- Postgres / Dockerfile / ready-probe **implementation** ([03](./03-host-production-seams.md)).
- Merchant OIDC screens ([04](./04-merchant-frontend.md)), checkout UX ([05](./05-checkout-frontend.md)), rails ([06](./06-money-rails.md)), journal + `RCPT-` ([07](./07-fulfillment-ledger-docs.md)), One SPA/`lzr_sk_`/HMAC ([08](./08-one-identity-production.md)), migrate vs greenfield ([09](./09-data-migration.md)).

**Locked (do not bargain in later PRs)**

| Lock | Meaning |
|------|---------|
| `task pay:test` is hermetic | `dotnet test Lazuar.Pay.slnx`. Fake One (`FakeOneHandler`). No Compose. No Zitadel. No `pnpm api:dev` in the One repo. No Hub Postgres `lazuar_mvp`. |
| IsolationTests stay | Banned tokens: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`. Also no `apps/lazuar-api` path in any csproj under `apps/lazuar-pay`. |
| `packages/pay-spec` is not Hub honesty CI | Do not add pay-spec to `task gen`, `contracts:honesty`, or the `contracts` job dirty-check. Grow `task pay:spec` separately. |
| Compose still Hub until S1 dogfood is real | `docker compose up` starts `db` + Hub `api` on **8080**. Swap **after** Bar B ([01](./01-production-ready-bar.md) §3 / §6), not when whoami is green. |
| `pnpm dev` / turbo `dev` starts **all** apps | Including `lazuar-ops` `:3003`, `lazuar-portal` `:3004`, `lazuar-admin` `:3005`, `lazuar-api` `:8080`. Footgun, not “full stack.” |
| Do not mix Hub OpenAPI gen with Pay | `task gen` sources `packages/api-spec/**/*.tsp` only. Pay TypeSpec is `packages/pay-spec`. |
| Do not retarget ops/portal | [012 P60](../012-one-to-pay/checklists/p60-old-frontends.md): `VITE_API_URL` stays Hub `http://localhost:8080/api/v1`. New clients are `:5178` / `:5179`. |
| Focused Pay listens **8081**, never 8080 | One owns 8080 when both products run. Hub compose `api` also wants 8080. |

Parent index: [README.md](./README.md). Binding from [011](../011-new-lazuar-pay/README.md) and [012](../012-one-to-pay/README.md). 012 C99 is **connected**, not production-ready. This paper is the DX/CI/ops companion to 02’s cutover phases A–D.

---

## 0. How to read this paper

Three bars, already split in 01:

| Bar | What it proves | CI / DX implication |
|-----|----------------|---------------------|
| **Connected** (012 C99, already true on this SHA) | 8081 whoami + org-ready + fixture checkout against **fake** One | `task pay:test` must stay green **without** GitHub Postgres or live One |
| **S0 façade** (011/12 steps 1–7) | SPA, login `:5175`, tenant, invite, `lzr_sk_`, One webhooks | Optional **manual** live whoami. Not a GitHub job. Playwright **later**. |
| **S1 money / Bar B** (011 sentence) | BYOK → buyer pays without One account → `RCPT-` + journal → retry no-ops → VIEWER cannot charge | New CI jobs for Pay host + Vite **build**. Compose may swap **after** this bar is lived. Hub GHCR stays until 02 Phase D. |

If you add a GitHub job that `docker compose up`s Hub so Pay tests can “see an API,” you have failed the slice. If you hook `packages/pay-spec` into `task gen` so “one pipeline,” you have mixed Hub honesty with Pay. If you mark Hub decommissioned because `pay:test` is green, you have lied — Hub is still what `ci.yml` `dotnet`, `ghcr.yml`, and default compose **ship**.

Three environments, three honesty rules (from 01 §7, restated so CI cannot collapse them):

| Env | Honesty |
|-----|---------|
| **Laptop** | One **8080** + Pay **8081** + merchant **5178** + checkout **5179**. Hub **off**. |
| **Staging** | New stack only. Zero Hub containers on the Pay staging hostname. One staging is One’s problem. |
| **Production** | Bar B with real blast radius. `hub.lazuar.com` is not the money door after 02 Phase C. |

---

## 1. Method / SHAs

Nothing was implemented. The following were **opened**. Filenames, not dumps. Huge YAML was grepped for job names / `lazuar-pay` / `Lazuar.Pay` / `pay:test` rather than read end-to-end.

### 1.1 Binding coordinates (this write)

| Source | Why it binds this paper |
|--------|-------------------------|
| [013 README](./README.md) | This file is “CI / ops / kill.” Analyses 01–10 stay the evidence. |
| [01](./01-production-ready-bar.md) | Bar B sentence; hermetic `pay:test`; Hub honesty unhooked from pay-spec; turbo `dev` pile-up; handoff row for this file. |
| [02](./02-replace-old-cutover.md) | Phases A–D; compose inventory; GHCR matrix; kill tables; `task` name encouragement; anti-strangler. |
| [03](./03-host-production-seams.md) §3.4–3.5, §6.5, §7 | Logs/metrics/probes gaps; CI honesty; test seams to keep (`FakeOneHandler`, IsolationTests). |
| [04](./04-merchant-frontend.md) | `:5178` scripts; Playwright is not the first lock. |
| [05](./05-checkout-frontend.md) | `:5179`; Playwright “no Zitadel” is **later**. |
| [08](./08-one-identity-production.md) | Live whoami is a human runbook, not CI. Do not wait on One staging PASSED. |
| [09](./09-data-migration.md) §4.4 | Greenfield money DB. Hub `lazuar_mvp` is not Pay’s DSN. |
| [012/10](../012-one-to-pay/10-dogfood-and-tests.md) | Hermetic first-connect tests; 8080 trap; do not hook old TypeSpec gen. |
| [012 P60](../012-one-to-pay/checklists/p60-old-frontends.md) | Do not retarget ops/portal; do not mix Hub OpenAPI honesty CI with Pay. |
| Host README | `apps/lazuar-pay/README.md`: compose still Hub; swap later when S1 is real; live whoami not CI. |

### 1.2 Files opened (Pay repo `6f866ff0`)

| Path | What was taken |
|------|----------------|
| `Taskfile.yml` | Task **names** and one-line `desc` / cmds. Groups: `infra:*`, `dev`, `docs*`, `fe`, `proxy*`, `pay:*`, `api:*`, `tunnel:*`, `gen*`, `contracts:honesty`, `docker:*`. |
| `turbo.json` | Task graph: `build`, `test`, `lint`, `check-types`, `dev` (`cache: false`, `persistent: true`). |
| `pnpm-workspace.yaml` | `apps/*`, `packages/*`, `examples/*`. |
| Root `package.json` | Scripts: `build`, `dev`, `lint`, `test`, `check-types` all `turbo run … --filter=!@examples/*`. `packageManager: pnpm@11.5.2`. |
| `apps/lazuar-pay/package.json` | `build` / `test` / `dev` / `lint` / `check-types` map to `Lazuar.Pay.slnx`. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/*.cs` | Test **class/method names** only. IsolationTests read in full (40 lines). |
| `apps/lazuar-pay-merchant/package.json` | Vite `--port=5178 --strictPort`. `build`: `tsc -b && vite build`. |
| `apps/lazuar-pay-checkout/package.json` | Vite `--port=5179 --strictPort`. Same build/lint/check-types shape. |
| Other `apps/*/package.json` | `name` + `dev` port only (turbo footgun). |
| `.github/workflows/` | Two files: `ci.yml`, `ghcr.yml`. Job names + matrix image names. Grep for `lazuar-pay` / `Lazuar.Pay` / `pay:test`: **no matches**. |
| `docker-compose.yml` | Service names: `db`, `api`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers`. |
| `docker-compose.ghcr.yml` | Same six services. Project `name: lazuar-hub`. |
| `docker-compose.dev-proxy.yml` | Service `caddy` only. |
| `deploy/prod/docker-compose.yml` | Services: `caddy`, `api`, `ops`, `portal`, `superadmin`, `developers`. |
| `docker-bake.hcl` | Group `default` targets: `api`, `lazuar-portal`, `lazuar-ops`, `lazuar-admin`, `lazuar-developers`. Images `lazuar-hub-*`. |
| `mprocs-dev.yaml` | Procs: developers, ops, admin, portal, caddy, ngrok tunnels. **No** merchant/checkout/pay. |
| `deploy/dev/Caddyfile` | Handles `/health`, `/api/*`, `/portal*`, `/docs*`, `/admin`, catch-all ops. |
| `apps/lazuar-pay/README.md` | Compose sentence; live whoami; task names. |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | Endpoints + CORS origins. No OTel/Serilog. |
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json` | MEL console levels + `One:BaseUrl`. |
| `packages/pay-spec/package.json` + README | Separate from `api-spec`. |
| Merchant / checkout READMEs | Origin pins; `VITE_PAY_API_URL`; no password form. |

### 1.3 What was **not** opened (so we cannot pretend)

- Full 500-line `ghcr.yml` deploy SSH script (only job names, matrix, concurrency group `lazuar-hub-cd-*`, remote path `/root/lazuar-hub-prod` as already recorded in 02 §2.4).
- Hub test projects under `apps/lazuar-api/tests/*` (names from `task api:test` / CI steps only).
- One repo compose / CI (ports from 012/05 and 02 §3, already written).
- Live `task pay:test` execution on this agent (inventory is the test **files**; 01 already counted 31 tests).
- Playwright config (none under merchant/checkout; Hub portal lockfile may list `@playwright/test` as a Next transitive — **not** a Pay e2e suite).

### 1.4 Commands used for names (not a live stack proof)

| Command / tool | Result used |
|----------------|-------------|
| `list_dir .github/workflows` | `ci.yml`, `ghcr.yml` only |
| grep `^  [a-zA-Z0-9_-]+:` on compose files | Service names in §5 |
| grep `lazuar-pay\|Lazuar.Pay\|pay:test` on `.github/workflows` | **zero hits** |
| grep `[Test]` under `Lazuar.Pay.Tests` | Method names in §3 |
| grep `OpenTelemetry\|Serilog` under `apps/lazuar-pay/src` | empty (matches 03 §3.4) |

---

## 2. Current CI vs focused Pay

**Verdict:** GitHub does **not** build `Lazuar.Pay.slnx`. It does **not** typecheck or Vite-build `lazuar-pay-merchant` / `lazuar-pay-checkout`. It does **not** run `task pay:test`. A green `main` CI today is a green **Hub cathedral** CI.

### 2.1 Workflow inventory (two files)

| File | Workflow `name:` | Triggers (short) | Jobs |
|------|------------------|------------------|------|
| `.github/workflows/ci.yml` | `CI` | `pull_request` + `push` to `main` | `contracts`, `dotnet` |
| `.github/workflows/ghcr.yml` | `GHCR + deploy` | `push` to `main` (path-filtered) + `workflow_dispatch` | `build-and-push`, `deploy` |

No other workflows. No `pay.yml`. No frontend job. No Playwright job. No turbo job.

### 2.2 Job `contracts` — Hub TypeSpec honesty

Runs on `ubuntu-latest`. Installs Node 22, pnpm **11.5.2**, .NET 10, go-task. Then:

| Step (name) | Command | Touches Pay? |
|-------------|---------|--------------|
| Install JS deps | `pnpm install --frozen-lockfile` | Installs workspace including pay-spec / Vite apps as **deps**, does not build them |
| Restore local .NET tools | `dotnet tool restore` | Kiota / NSwag for **Hub** |
| Generate contracts | `task gen --force` | `packages/api-spec` → api-types-ts, api-types-dotnet, lhdn-sdk-* |
| Fail if generated clients dirty | `git diff --exit-code` on Hub generated paths | **pay-spec not in the list** |
| OpenAPI ↔ Minimal path honesty | `node scripts/check-openapi-minimal-honesty.mjs` | Hub Minimal API + `honesty-allowlist.yaml` |

`task gen` sources (Taskfile): `packages/api-spec/**/*.tsp`. Generates Hub OpenAPI + clients. **Does not** compile `packages/pay-spec`.

Dirty-check paths (exact):

- `packages/api-types-ts/src`
- `packages/api-types-dotnet/Generated`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs`
- `packages/lhdn-sdk-ts/src/generated`
- `packages/lhdn-sdk-dotnet/src/Generated`

**Keep this job as Hub-shaped until 02 Phase D.** Do not “fix” it by adding `packages/pay-spec/dist` (gitignored anyway) or Pay C# DTOs. Pay’s contract gate is `task pay:spec` (`pnpm exec tsp compile .` in `packages/pay-spec`).

### 2.3 Job `dotnet` — Hub `Lazuar.slnx` only

```yaml
defaults.run.working-directory: apps/lazuar-api
```

Service: `postgres:16-alpine`, DB `lazuar_mvp`, host **5432**, env `LAZUAR_TEST_PG=Host=localhost;Port=5432;Database=lazuar_mvp;…`.

| Step | Command |
|------|---------|
| Restore | `dotnet restore Lazuar.slnx` |
| Build | `dotnet build Lazuar.slnx --no-restore` |
| Test (Architecture) | `dotnet test tests/Lazuar.ArchitectureTests/…` |
| Test (Integration) | `dotnet test tests/Lazuar.IntegrationTests/…` |
| Test (Module) | `dotnet test tests/Lazuar.ModuleTests/…` |
| Test (Billing) | `dotnet test tests/Modules.Billing.Tests/…` |
| Test (Ops) | `dotnet test tests/Modules.Ops.Tests/…` |

Same five projects as `task api:test`. **Not** `Lazuar.Pay.slnx`. **Not** `task pay:test`.

Hub CI Postgres on **5432** is also a laptop footgun if an engineer copies the job locally while One wants 5432 (02 §3.3). Pay’s future test DB is **not** `lazuar_mvp` and **not** this service (03 §6.5; 09 §4.4).

### 2.4 Job `build-and-push` — five Hub images, zero Pay images

Concurrency group: `lazuar-hub-cd-${{ github.ref }}`. Registry: `ghcr.io`. Image prefix: `ghcr.io/${{ github.repository_owner }}`.

Matrix `name` / Dockerfile:

| Matrix `name` | Dockerfile | Build-args (prod Hub URLs) |
|---------------|------------|----------------------------|
| `lazuar-hub-api` | `apps/lazuar-api/Dockerfile` | (none listed) |
| `lazuar-hub-portal` | `apps/lazuar-portal/Dockerfile` | `NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1`, `NEXT_BASE_PATH=/portal` |
| `lazuar-hub-ops` | `apps/lazuar-ops/Dockerfile` | `VITE_API_URL=https://hub.lazuar.com/api/v1`, `VITE_PORTAL_URL=https://hub.lazuar.com/portal`, `VITE_BASE_PATH=/` |
| `lazuar-hub-superadmin` | `apps/lazuar-admin/Dockerfile` | `VITE_API_URL=https://hub.lazuar.com/api/v1`, `VITE_BASE_PATH=/admin/` |
| `lazuar-hub-developers` | `apps/lazuar-developers/Dockerfile` | `NEXT_BASE_PATH=/docs` |

Platform: `linux/amd64`. Tags: `latest` (default branch), `sha-<short>`, full SHA.

`docker-bake.hcl` group `default` is the same five targets. OCI labels still say “Lazuar Hub CaaS platform” and source `github.com/proxeon/lazuar-hub`.

**There is no `apps/lazuar-pay/Dockerfile` on this SHA.** No bake target `pay`. No GHCR name `lazuar-pay`. Pushing a health-only image as `lazuar-hub-api` would be a rename lie (03 §6.5; 02 §10.17).

### 2.5 Job `deploy` — Hub VPS

Job name: `Deploy hub VPS`. Needs `build-and-push`. Only on `main`. Remote: rsync `deploy/prod/` → `/root/lazuar-hub-prod/`. Health wait is on Hub container names (`hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy`) as recorded in 02 §2.4. A green deploy is a green **Hub** deploy.

### 2.6 Does GitHub build the Vite apps?

| App | Local `build` script | In `ci.yml`? | In `ghcr.yml`? |
|-----|----------------------|--------------|----------------|
| `lazuar-pay-merchant` | `tsc -b && vite build` | **No** | **No** (no Dockerfile) |
| `lazuar-pay-checkout` | `tsc -b && vite build` | **No** | **No** |
| `lazuar-ops` | `vite build` | **No** as a job | **Yes** (image `lazuar-hub-ops`) |
| `lazuar-portal` | `next build` | **No** as a job | **Yes** (`lazuar-hub-portal`) |
| `lazuar-admin` | `vite build` | **No** as a job | **Yes** (`lazuar-hub-superadmin`) |
| `lazuar-developers` | `next build` | **No** as a job | **Yes** |
| `lazuar-docs` | vitepress | **No** | **No** |

Root `pnpm build` **would** turbo-build every workspace package except `@examples/*`, including merchant/checkout `tsc -b && vite build` **and** `lazuar-pay` `dotnet build Lazuar.Pay.slnx` **and** Hub `lazuar-api` `dotnet build`. **CI does not run turbo.** Do not start running `pnpm build` on GitHub as the Pay gate: it would compile the cathedral and the new stack in one job and hide isolation.

### 2.7 What a focused-Pay CI job must look like (later program — shape only)

| Property | Required shape | Forbidden shape |
|----------|----------------|-----------------|
| Working directory | `apps/lazuar-pay` | `apps/lazuar-api` |
| Solution | `Lazuar.Pay.slnx` | `Lazuar.slnx` |
| Command | `task pay:test` or `dotnet test Lazuar.Pay.slnx` | `task api:test` |
| Postgres | **none** until Pay has a DB; then a **Pay** DB, not `lazuar_mvp` | Hub `LAZUAR_TEST_PG` |
| One | Fake handler only | `services: zitadel` / live 8080 |
| Sibling job | merchant `pnpm --filter lazuar-pay-merchant check-types` + `build`; same for checkout | `pnpm --filter lazuar-ops build` as a Pay gate |
| Spec | optional `task pay:spec` | `task gen` / honesty-allowlist |
| Needs Hub `dotnet`? | **No.** Parallel. Hub job may stay until Phase D | `needs: [dotnet]` |

Paper 03 already said: a production host whose tests never run on the PR is not production-shaped. This paper repeats that as a **CI** fail lock, not a host-code change.

### 2.8 `task` names vs CI today

| Task | What it runs | In GitHub? |
|------|--------------|------------|
| `pay:restore` / `pay:build` / `pay:test` | Focused slnx | **No** |
| `pay:spec` | `packages/pay-spec` tsp compile | **No** |
| `pay:dev` / `pay:merchant` / `pay:checkout` | Local DX | N/A (persistent) |
| `api:restore` / `api:build` / `api:test` | Hub slnx + 5 test projects | **Yes** (`dotnet` job ≈ build+test) |
| `gen` / `contracts:honesty` | Hub TypeSpec | **Yes** (`contracts` job) |
| `docker:build` / `docker:push` | Bake five Hub images | **Yes** (`ghcr.yml` matrix, not Taskfile) |
| `docker:up:full` | Compose `--profile full` | Not CI; local Hub pile-up |

Taskfile `pay:test` **desc** still says “health + isolation.” Stale (01 §2.7). Hygiene for a later PR; not a product gate. Do not treat the blurb as the inventory in §3.

---

## 3. Test policy

Three layers. Do not collapse them into “CI is green.”

### 3.1 Layer A — hermetic host (`task pay:test`) — **required, now and forever**

**Command:** `dotnet test Lazuar.Pay.slnx` from `apps/lazuar-pay` (Taskfile `pay:test`; package.json `test` is the same).

**Process:** `WebApplicationFactory<Program>`. One is `FakeOneHandler` injected via `PayApiFactory.ConfigureTestServices` (replaces `OneClient` with `HttpClient` BaseAddress `http://one.test/api/v1/`). Health/CORS tests may use a raw factory **without** the fake — HealthTests already assert `Health_does_not_call_one`.

**Must not require:** Docker Compose, Postgres, Zitadel, OpenFGA, One repo, Hub `task dev`, network.

**IsolationTests (stay — four tests, banned list frozen):**

| Test | Asserts |
|------|---------|
| `Host_csproj_does_not_reference_the_old_api` | Host csproj text does not contain `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api` |
| `Test_csproj_does_not_reference_the_old_api` | Same tokens in `Lazuar.Pay.Tests.csproj` |
| `Source_does_not_use_mediatr_or_hub_modules` | Every `src/**/*.cs`: no `MediatR`, `Modules.One`, `BuildingBlocks` |
| `No_csproj_references_apps_lazuar_api` | Every `*.csproj` under the Pay root: no `apps/lazuar-api` or `apps\lazuar-api` |

Walks parents from `AppContext.BaseDirectory` until `src/Lazuar.Pay/Lazuar.Pay.csproj` exists. Do not weaken the banned list to “allow BuildingBlocks just for logging.” If production logging needs a package, it is a NuGet — not `ProjectReference` into Hub (03 §8.6).

**Other hermetic files on this SHA (method names — the inventory CI must keep running):**

| File | Tests |
|------|-------|
| `HealthTests.cs` | `Health_returns_ok`, `V1_health_returns_ok`, `Health_does_not_call_one` |
| `CorsTests.cs` | `Health_allows_merchant_origin` (5178), `Health_allows_checkout_origin` (5179), `Health_does_not_allow_ops_origin` (3003) |
| `WhoamiTests.cs` | maps `org_id` from One `/me`; empty tenants; 401 skips One; One 401 mapped; timeout → 503; 500 → 503 |
| `OrgReadyTests.cs` | member allowed; `allowed: false` → forbidden; One 403; One 500 → 503; 401 skips One; path org not header |
| `CheckoutTests.cs` | 401 without bearer; create+get open session; unknown 404; other org 403; get other org 403; idempotent key; default MYR; reject non-positive amount; health still skips One |
| Helpers | `PayApiFactory.cs`, `FakeOneHandler.cs` — **keep** (03 §7.1) |

When money lands (06/07), add hermetic tests **in this project**: webhook signature, retry no-ops, journal balance, `RCPT-` numbering, VIEWER cannot charge. Still against fakes / in-memory (or Testcontainers **Pay** Postgres later). Still no live CHIP in `pay:test`.

When a Pay DB exists: a **second** job or a Testcontainers fixture may start Postgres **`lazuar_pay`**. Do not attach to Hub’s `LAZUAR_TEST_PG` / `lazuar_mvp`. Do not publish it on CI **5432** if that collides with a reusable Hub job still on the same runner (usually fine in GHA services; still do not **share** the database).

### 3.2 Layer B — optional live whoami — **human, not GitHub**

Host README already specifies this. Copy the rules; do not automate them into `ci.yml`.

| Rule | Why |
|------|-----|
| One API on **8080**, Pay on **8081**, Hub `task dev` / compose `api` **off** | 8080 collision |
| Fingerprint: `GET http://localhost:8080/api/v1/` names `lazuar-one-api` | Hub `/health` can also look like `{status:ok}` |
| Login at One `:5175`; copy **access_token**, never `id_token` | NP-ONE token law (012/02, 08) |
| `curl -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami` | Connected proof |
| No header → 401 | Same as hermetic |
| Fixture checkout is **not** Bar B | In-memory `status: open` |

Optional later: a **manual** workflow_dispatch that an engineer runs against staging with secrets. That is still not PR CI. One staging remains **NOT PASSED** on `0f79fe4` (01 §7.2; 08). Do not block Pay PRs on it.

### 3.3 Layer C — frontend `tsc` / `build` — **required before claiming production-shaped UIs**

Merchant and checkout scripts today:

| Script | Merchant | Checkout |
|--------|----------|----------|
| `dev` | `vite --port=5178 --host=0.0.0.0 --strictPort` | `:5179` |
| `build` | `tsc -b && vite build` | same |
| `lint` | `oxlint` | `oxlint` |
| `check-types` | `tsc -b` | `tsc -b` |
| `preview` | `:4178` | `:4179` |
| `test` | **absent** | **absent** |

Policy:

1. PR CI for focused Pay **must** grow a job (or two) that runs `check-types` + `build` for `lazuar-pay-merchant` and `lazuar-pay-checkout`.
2. That job **must not** build `lazuar-ops` / `lazuar-portal` / `lazuar-admin` as a Pay gate.
3. Filter: `pnpm --filter lazuar-pay-merchant…` / `--filter lazuar-pay-checkout…`, **not** `pnpm build` (turbo whole workspace).
4. No `@repo/api-types-ts` dependency (merchant README). A later `@repo/pay-types-ts` is generated from **pay-spec**, not `task gen` (01 open question 6; P60.3).
5. Lint (`oxlint`) is cheap; add it when the job exists. Do not wait on a Hub ESLint cathedral.

Until those jobs exist, a broken `App.tsx` can merge if Hub `dotnet` is green. That is the same class of hole as Hub issue 325 (zero tests in ops/admin) — do not recreate it on 5178/5179.

### 3.4 Layer D — Playwright — **later, not a Bar B gate on this SHA**

There is no Playwright config under `apps/lazuar-pay-merchant` or `apps/lazuar-pay-checkout`. Do not stand up Playwright to lock a health-probe SPA.

When it is allowed (after 04 OIDC + 05 hosted pay exist):

| Spec | Must prove | Must not prove |
|------|------------|----------------|
| Merchant | Redirect to One `:5175`, callback `:5178/callback`, whoami with `access_token` | Hub cookie `lazuar_auth`; password form |
| Checkout | Buyer context **never** hits `:5175` or `/v1/whoami` (05) | Merchant create (different plane) |
| VIEWER | Cannot charge / cannot paste keys | OpenFGA `app.viewer` |
| Paid | Receipt number visible; setup session not “paid” | LHDN VALID |

Playwright against **live** One is an optional nightly / manual staging job. PR Playwright, if any, uses a **stubbed Pay** and a stubbed OIDC — or is skipped until that harness exists. Do not add One’s Zitadel to Pay’s `ci.yml` `services:`.

### 3.5 What Hub tests are **not**

| Hub surface | Role after this paper | Pay policy |
|-------------|----------------------|------------|
| Architecture / Integration / Module / Billing / Ops projects | Cathedral CI until Phase D | Do not import. Steal **cases** as a checklist (02 §7.1), re-implement in `Lazuar.Pay.Tests` |
| Honesty allowlist | Hub OpenAPI ⊆ Minimal | Stay off pay-spec |
| Portal `node:test` i18n / grossBreakdown | Hub portal | Steal SST **judgment** via 07, not the test file as a ProjectReference |
| Issue 325 “add Playwright to ops” | Museum | **Refuse** as a 013 ticket |

### 3.6 Policy table (one screen)

| Layer | When | Where it runs | Pass means | Fail lock |
|-------|------|---------------|-------------|-----------|
| A hermetic host | Every PR that touches Pay **or** as soon as a Pay job exists | GHA + laptop `task pay:test` | Isolation + whoami/authz/checkout/health/CORS against fake One | Live One in this job; Hub slnx in this job |
| B live whoami | Human dogfood / staging | Laptop / staging URL | Ada JWT → 200 whoami; fingerprint One on 8080 | Making this a required PR check while One staging is NOT PASSED |
| C frontend tsc/build | Every PR that touches merchant/checkout; required before Bar B UI claim | GHA filter those two packages | `tsc -b` + `vite build` | `pnpm build` turbo; building ops/portal |
| D Playwright | After 04/05 have screens | Later job | Buyer has no Zitadel; merchant uses One login | First lock on health-probe SPA; Hub cookie e2e |

---

## 4. Observability minimum for the new host

Paper 03 already inventoried the gap (no Serilog, no OTel, no ready, no metrics). This paper states the **minimum** a later 013 implementation may ship without cloning Hub `PlatformMetricsCollector` / `GET /health/metrics`.

### 4.1 What exists on `6f866ff0`

| Surface | Fact |
|---------|------|
| Logging | Generic host MEL. `appsettings.json`: Default `Information`, `Microsoft.AspNetCore` `Warning`. Console. |
| Packages | No Serilog. No OpenTelemetry. No App Insights in the **host** csproj (test `bin/` may contain `Microsoft.ApplicationInsights.dll` from VSTest — not a product choice). |
| Liveness | `GET /health` and `GET /v1/health` → `{ status: "ok" }`. No One. No DB. |
| Readiness | **None** |
| Metrics | **None** |
| Request id | **None** |
| `OneClient` | Forwards Bearer; maps 401/403/timeout/5xx. Does not log status/elapsed (03). |

Hub (negative oracle, do **not** copy fields): `GET /health` liveness; `GET /health/ready` DB + optional outbox lag; `GET /health/metrics` cathedral gauges (`lhdn_stuck_count`, outbox, dead letters). Serilog.AspNetCore + Console. Homegrown collector.

### 4.2 Minimum logs (when money exists — not a reason to add MediatR)

| Must log | Must never log |
|----------|----------------|
| HTTP method + path + status + elapsed ms | `Authorization` header |
| Correlation / request id (generate if absent; accept `X-Request-Id` / `traceparent` if present) | `lzr_sk_` / API key material |
| `user_id` **after** whoami maps (not the raw JWT) | Raw JWT / `id_token` |
| `org_id` on checkout create / webhook apply | BYOK secret, Stripe/CHIP raw secret |
| Upstream One **status code** + elapsed (not body) | One HMAC, Zitadel PAT (Pay must not hold PAT anyway) |
| Webhook provider + event id + idempotent hit/miss | Full webhook JSON if it contains PANs / secrets |
| Journal write success/fail at **error** level | Line-by-line PII dumps |

Format: **JSON console** is enough for a first VPS / container. Serilog.AspNetCore is what this monorepo already knows (03 §3.4). OpenTelemetry collector graphs are **not** in the repo — do not invent a CNCF sidecar farm to look modern. A later cloud agent can scrape.

If a package is added: NuGet. IsolationTests stay red on `BuildingBlocks`.

### 4.3 Minimum probes

| Path | Role | May call One? | May call Pay Postgres (when it exists)? |
|------|------|---------------|----------------------------------------|
| `GET /health` | Liveness — process can return JSON | **Never** | **Never** (a stuck pool must not kill liveness) |
| `GET /v1/health` | Same JSON, Bezos-prefixed alias | **Never** | **Never** |
| `GET /health/ready` (new, later) | Readiness for load balancer | **Never** | **Yes** — Pay DB only |
| Hub `GET /health/metrics` | Cathedral | n/a | **Do not add** under this name |

Rules (03 §3.5, binding here for CI/staging):

1. Keep `HealthTests.Health_does_not_call_one`. Add `Ready_does_not_call_one` when ready exists.
2. If One is down: whoami / org-ready / merchant checkout-create **503**; **buyer pay** and **health** stay up (011-07: money stays true if membership lags).
3. Docker HEALTHCHECK should hit **liveness**, not ready, until start-period is understood. Hub used 90s because nine EF migrations boot. Pay must not need that if it does not migrate nine schemas (03).
4. Do not treat `/health` as “One is reachable.” Fingerprint One separately (`/api/v1/` name).

### 4.4 Minimum metrics (after Bar B is a real charge, not before)

Do not add a metrics endpoint to look production-ready while checkout is in-memory.

When charges exist, prefer ASP.NET `System.Diagnostics.Metrics` / EventCounters — **not** Hub `LazuarMetricsGauges`.

| Metric (names illustrative) | Why page |
|-----------------------------|----------|
| `http.server.request.duration` by route template | Checkout create latency |
| `pay.whoami.upstream_status` (or log-derived) | One 5xx / timeout rate |
| `pay.checkout.paid` / `pay.webhook.idempotent_hit` | Money loop |
| `pay.journal.unbalanced` (should stay 0) | 07 fail lock |
| Process / GC / Kestrel built-ins | Container OOM |

Export: Prometheus `/metrics` **or** the host’s cloud agent. Pick in implementation; this paper does not pick a vendor. **Refuse:** App Insights SDK “because the test bin folder has the DLL.” **Refuse:** copying `/health/metrics` JSON so Hub Grafana dashboards keep working.

### 4.5 What staging/prod must show an on-call (minimum dashboard, not a product)

| Question | How you answer it without Hub metrics |
|----------|---------------------------------------|
| Is Pay up? | `/health` 200 |
| Can it take traffic? | `/health/ready` 200 (once DB exists) |
| Is One the reason whoami 503s? | Logs: upstream status; do **not** fail ready |
| Did a webhook retry double-journal? | Idempotency row + balanced journal test + metric |
| Are we looking at Hub by mistake? | Fingerprint: Pay listen 8081 / new hostname; Hub `/health` on 8080 is **not** this process |

### 4.6 Frontend observability (tiny)

Merchant/checkout are static Vite. No Sentry required for Bar B. Console errors during dogfood are enough. Do not add Hub ops analytics, Posthog, or a second RUM product as a production-ready gate. If later: one error reporter, **no** tokens in `localStorage` logs, **no** PAN.

---

## 5. Staging topology vs 8080 collision

### 5.1 The collision (restated, not redesigned)

| Listener | Who wants it | Staging implication |
|----------|--------------|---------------------|
| Host **8080** | One API **and** Hub `api` / `task dev` | **One** owns 8080 whenever Pay dogfood/staging needs identity. Hub off on that box. |
| Host **8081** | Focused Pay | Pay API. Never “simplify” to 8080 after Hub dies (02 §10.5). |
| Host **5432** | One Postgres **and** Hub `lazuar-db` | Do not start `task infra:up` on a Pay+One staging box. Pay money DB, when it exists, publish **5435** (02 §3.3). |
| Host **3005** | Hub admin **and** One stock Login V2 | Both down for merchants. |
| Host **8090** | One OpenFGA HTTP **and** Hub dual-run tunnel hop | Hub `task tunnel:cf` is **not** Pay staging. |
| Container name `lazuar-api` | One compose profile `api` **and** Hub compose `api` | Global Docker name. Do not start Hub’s. |

Fingerprint every session (host README + 02 §3.5): One `GET /api/v1/` → `lazuar-one-api`. Pay `GET /v1/health` on **8081**. Hub `/health` `{status:ok}` on 8080 is **ambiguous** — do not use it as the One check.

### 5.2 Compose on this SHA (still Hub — locked)

**`docker-compose.yml`** (project defaults to directory `lazuar-pay`). Default `up` = `db` + `api`. Frontends need `--profile full`.

| Service | Profile | `container_name` | Host ports | Image / build | Hub pin |
|---------|---------|------------------|------------|---------------|---------|
| `db` | default | `lazuar-db` | **5432:5432** | `postgres:16-alpine` | DB `lazuar_mvp` |
| `api` | default | **`lazuar-api`** | **8080:8080** | `apps/lazuar-api/Dockerfile` → `ghcr.io/proxeon/lazuar-hub-api:local` | ConnectionStrings → `lazuar_mvp` |
| `lazuar-ops` | `full` | `lazuar-ops` | **3003:3000** | `apps/lazuar-ops/Dockerfile` | Build-arg `VITE_API_URL` default `http://localhost:8080/api/v1` |
| `lazuar-portal` | `full` | `lazuar-portal` | **3004:3000** | portal Dockerfile | `API_URL: http://api:8080/api/v1` (compose DNS `api` = Hub) |
| `lazuar-admin` | `full` | `lazuar-superadmin` | **3005:3000** | admin Dockerfile | `VITE_API_URL` Hub 8080 |
| `lazuar-developers` | `full` | `lazuar-developers` | **3002:3000** | developers Dockerfile | Hub OpenAPI tiles |

Network: `lazuar-network`. **No compose service for `apps/lazuar-pay`.** Host README: “Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real.”

**`docker-compose.ghcr.yml`:** `name: lazuar-hub`. Same six services, **no profiles** (all frontends start). Images `lazuar-hub-*:${TAG:-latest}`. Same 8080/5432 collision.

**`docker-compose.dev-proxy.yml`:** `name: lazuar-dev-proxy`. Service `caddy` only (`lazuar-dev-caddy`), host **9080**. Upstreams are **host** ports. If One owns 8080, Caddy `/health` and `/api/*` become **One** while the path map still claims Hub (02 §2.3). **Leave down** during Pay+One.

**`deploy/prod/docker-compose.yml`:** `name: lazuar-hub`. Path `/root/lazuar-hub-prod`. Internal network `hub`. Only Caddy publishes 80/443. This **is** production Hub. Pay staging/prod must not be “add `pay` to this file and `reverse_proxy` `/api` at 8081 while `/` still serves ops.”

### 5.3 Staging shapes that work vs fail (CI/ops view)

Aligned with 02 Phase B. This paper only cares that **CI and compose** do not invent a fourth shape.

| Shape | Staging hostname serves | Allowed? |
|-------|-------------------------|----------|
| **S1** — One staging (One’s URLs) + Pay staging API + merchant + checkout | New stack | **Yes.** Zero Hub containers on this hostname |
| **S2** — Laptop-like: processes not compose | Same topology, no Docker Hub `api` | **Yes** for early staging |
| **S3** — Hub compose + Pay sidecar 8081, one hostname, Caddy `/api` → Pay, `/` → ops | Strangler | **No** (02 §10.7, P60) |
| **S4** — Hub `VITE_API_URL=http://pay:8081` | Retarget | **No** |
| **S5** — Pay bound as container `api` on 8080 “because staging already maps 8080” | Steals One’s port | **No** |
| **S6** — GHCR Hub images + env rename `lazuar-pay-*` | Rename lie | **No** (02 §10.17) |
| **S7** — Shared `lazuar-mvp` for Pay money | Second org / Hub DSN | **No** (09) |

### 5.4 Staging vs GitHub vs One staging PASSED

| Claim | Policy |
|-------|--------|
| “We cannot stage Pay until One STAGING-PROOF-STATUS is PASSED” | **False.** 08 / 01 §7: do not wait. Laptop One + fake CI is enough to **build**. Staging Pay talks to whatever One HTTP is actually up, labelled honestly. |
| “Pay staging CI must docker-compose Hub for integration” | **False.** Hermetic tests. Staging deploy is CD of **Pay images that do not exist yet**, not Hub. |
| “Staging can keep Hub for rollback on the **same** DNS” | **False.** Hub staging, if it exists, is **labelled Hub** on a **different** hostname (02 Phase B). |
| “Pay staging needs 8080 published to the internet” | **False.** Pay publishes **its** API (internal 8081 behind TLS). One publishes One. |

### 5.5 When compose may swap (gate, not a date)

Host README + 01 + 02 Phase D:

**Not before:**

1. Bar B lived on the **new** three processes ([01](./01-production-ready-bar.md) §3.1 / §6) — not C99 whoami.
2. Pay has a real DB and a Dockerfile / run shape ([03](./03-host-production-seams.md)).
3. Merchant OIDC and checkout hosted pay exist ([04](./04-merchant-frontend.md), [05](./05-checkout-frontend.md)).
4. Cutover Phase B topology agreed: **new stack only** on Pay staging DNS ([02](./02-replace-old-cutover.md) §5).

**Swap means:** default `docker compose up` in **this** repo starts **Pay 8081 + Pay DB 5435** (names open), **or** compose is removed from default DX and README starts with `task pay:dev`. It does **not** mean adding a second `api` service.

Until then, default compose remaining Hub is **honest museum DX**, not a bug to “fix” by retargeting.

### 5.6 Tunnels (Hub-shaped — not Pay staging)

| Task | What it actually tunnels | Staging? |
|------|--------------------------|----------|
| `tunnel:api` | ngrok **8080** — comment says “standalone Pay API” meaning **Hub** | No |
| `tunnel:fe` | ngrok **3004** portal | No |
| `tunnel:cf` | Cloudflare `pay-local.lazuar.dev` → host **8090** (Aura hop A) | No — 8090 is OpenFGA next to One |
| `tunnel:cf:url` | Probes `https://pay-local/health`; prints `App__ApiBaseUrl=https://…/api/v1` | Hub webhook base, not Pay `/v1` |

Pay staging webhooks (06) need a **Pay** public URL to **8081** (or the TLS terminator in front of Pay), path on Bezos `/v1/…`, not Hub `/webhooks/payments/billplz/{tenantId}` and not Aura 8090.

---

## 6. DX after replace: `task pay:dev` + merchant + checkout + One

### 6.1 Target laptop (Phase A now, default README after Phase D)

```text
One API          :8080   (One repo: pnpm api:dev / their docs)
One login        :5175
One app          :5174   (mint tenant / Ada)
Zitadel          :8085   (One’s IdP — Pay does not hold PAT)
OpenFGA HTTP     :8090   (One’s — Pay does not bind)
Pay host         :8081   task pay:dev
Merchant Vite    :5178   task pay:merchant
Checkout Vite    :5179   task pay:checkout
```

**Leave down:** `task dev`, `task infra:up`, `task fe`, `task proxy`, `docker compose up`, `task docker:up:full`, `task docker:up:ghcr`, `task tunnel:*`, `pnpm dev`.

Fingerprint: One `/api/v1/` name; Pay `/v1/health`; merchant origin in Pay CORS (already 5178/5179, not 3003 — `CorsTests.Health_does_not_allow_ops_origin`).

### 6.2 Taskfile: keep / grow / kill (DX)

| Task | After replace | Notes |
|------|---------------|-------|
| `pay:restore` `pay:build` `pay:test` `pay:dev` `pay:spec` | **Keep** — default DX | Fix stale `pay:test` desc when touching Taskfile |
| `pay:merchant` `pay:checkout` | **Keep** | Already the right FE tasks |
| `dev` (`pnpm --filter lazuar-api dev` after `infra:up`) | **Kill from default docs**; delete when Phase D | Name says “dev,” starts Hub 8080 |
| `infra:up` / `down` / `reset` / `logs` | Kill or retarget at **Pay** Postgres 5435 **after** 03 | Today: Hub `docker-compose up db` on 5432 |
| `fe` (`mprocs-dev.yaml`) | Kill | 3002–3005, not 5178/5179 |
| `proxy` `proxy:up` `proxy:down` `proxy:validate` | Kill as Pay DX | Caddy 9080 Hub path map |
| `docs` `docs:build` | Hub VitePress 5180 — later replace or leave as museum docs | Collides with One docs 5180 (02 §3.1) |
| `api:*` | Museum until Phase D | Not in Getting Started |
| `gen*` `contracts:honesty` | Hub only until Phase D | Never absorb `pay:spec` |
| `docker:*` | Hub bake/push until Pay images exist; then **new** tasks or new bake targets `lazuar-pay*` | Do not retag Hub images |
| `tunnel:*` | Hub / Aura | New Pay tunnel task only when 06 needs a public webhook URL to **8081** |

### 6.3 What you type vs what you get (today — 02 §4.4 expanded)

| You type | You think | You get | After replace you type |
|----------|-----------|---------|------------------------|
| `task dev` | start Pay | Hub 8080 + 5432 | `task pay:dev` |
| `task pay:dev` | start Pay | **8081** (correct) | same |
| `task fe` | frontends | Hub 3002–3005 | `task pay:merchant` + `pay:checkout` |
| `task proxy` | like prod | Hub Caddy 9080 → 8080 | Pay’s own terminator, or none locally |
| `task docker:up:full` | full stack | Hub `--profile full` | Pay compose **or** process DX |
| `task infra:up` | deps | Hub Postgres 5432 (fights One) | Pay DB 5435 **or** One’s docs for identity DB |
| `pnpm dev` | all apps | **turbo pile-up** (§8) | `pnpm --filter lazuar-pay-merchant dev` etc. |
| `pnpm --filter lazuar-pay dev` | focused host | 8081 (correct) | same |
| `task api:test` | tests | Hub five projects | `task pay:test` |
| `task gen` | contracts | Hub OpenAPI | `task pay:spec` |

### 6.4 One’s DX (not this Taskfile)

Pay README must **link** One’s bootstrap (their `prove-local-stack` / compose / `pnpm api:dev`). Do not vendor One’s Taskfile into Pay. Do not start One’s `container_name: lazuar-api`. Do not send merchants to One staff SPA `:5173` or stock Login V2 `:3005`.

Demo human stays One’s (often `ada@acme.test`). Hub `founder@acme.test` is **not** Pay login (02 §2.10).

### 6.5 mprocs after replace

`mprocs-dev.yaml` today: `lazuar-developers`, `lazuar-ops`, `lazuar-admin`, `lazuar-portal`, optional caddy/ngrok. **Autostart Hub UIs.**

After replace: either delete the file, or replace procs with `pay` / `merchant` / `checkout` **without** ops/portal/admin. Do **not** add merchant as a fifth proc next to ops (strangler DX). `task fe` must not keep meaning Hub.

### 6.6 Root README / host README

Today root README is Hub CaaS Getting Started (ops 3003, portal 3004, admin 3005, modular monolith). Host README is already the correct **Pay** getting started (`task pay:test` / `pay:dev` / `pay:merchant` / `pay:checkout`).

Replace of DX includes **root README starting with the host README loop**, not a paragraph at the bottom. Kill criterion: 02 §6.13. This paper only requires that CI badges, if any, do not imply Hub `CI` workflow = Pay tests.

### 6.7 Suggested engineer aliases (not a new product)

A later implementation may add `task pay:stack` that runs merchant+checkout **and documents** that Pay host + One are separate processes (mprocs **Pay-only**, or three terminals). It must **not** start Hub. It must **not** be named `task dev`.

---

## 7. Ordered decommission checklist

Each row: **artifact → not before (gates) → then**. Gates point at sibling papers. Do not treat this as a license to delete on this SHA. Dark ≠ delete on day one (02 Phase D).

Kill **order** is the point: CI for Pay **before** Hub CI dies; Pay images **before** Hub GHCR stops; Bar B **before** compose swap; 01 sentence **before** claiming production; 02 Phase C **before** DNS; 02 Phase D **before** tree delete.

### 7.0 Precondition: do not decommission Hub to “make room” for whoami

C99 is already green. Hub still serves (or can serve) `hub.lazuar.com`. IsolationTests do not delete `apps/lazuar-api`. **First commits of a later 013 program add Pay CI and Pay observability; they do not `rm` Hub workflows.**

### 7.1 Order (must not skip)

```text
 0. Keep IsolationTests + hermetic pay:test (already local)
 1. Add GitHub job for Lazuar.Pay.slnx  (this paper §2.7, §3.1)
 2. Add GitHub job for merchant+checkout tsc/build  (§3.3)
 3. Host seams: DB, ready probe, JSON logs  (03) — still dual-run
 4. Merchant OIDC + checkout pay + rails + journal  (04–07)
 5. One SPA / keys / HMAC as needed for Bar B  (08)
 6. Greenfield Pay DB; no Hub org table  (09)
 7. Bar B sentence lived  (01)  → 02 Phase B staging DNS on new stack
 8. Pay images + Pay CD  (new GHCR names)  — Hub GHCR may still build for rollback
 9. 02 Phase C production DNS to new stack
10. Rollback window  (02 §9)
11. 02 Phase D Hub dark: stop Hub processes
12. Then: Hub CI job, Hub bake, Hub compose default, task dev, mprocs, tunnels
13. Last: delete or archive app folders
```

### 7.2 Checklist table (uncondensed)

| # | Artifact | Not before | Then | Cheat (refuse) |
|---|----------|------------|------|----------------|
| 1 | IsolationTests + banned tokens | — (already) | Keep forever | Narrow bans to allow `BuildingBlocks` “for logging” |
| 2 | GHA job `pay` / `pay-dotnet` running `task pay:test` | Job exists **before** any Hub CI sunset | Hermetic; no `lazuar_mvp` | Fold into Hub `dotnet` job with two slnx |
| 3 | GHA job `pay-frontends` `tsc -b && vite build` for 5178/5179 | Before claiming merchant/checkout production-shaped (01 §3.5) | Filter those packages only | `pnpm build` turbo whole repo |
| 4 | Optional `task pay:spec` on PR | When pay-spec grows beyond health | Separate from `contracts` | Add pay-spec to `task gen` dirty-check |
| 5 | JSON logs + request id | 03 logging pick; useful as soon as staging exists | NuGet, not Hub Observability | Copy `/health/metrics` |
| 6 | `/health/ready` | Pay Postgres exists (03, 09) | Ready never calls One | Ready requires Zitadel |
| 7 | Pay Dockerfile + bake target `lazuar-pay` (name open) | 03 deploy shape; Bar B close | New image name, not `lazuar-hub-api` | Retag Hub image |
| 8 | Merchant/checkout images or static hosting | 04/05 have more than health probe | Own origins, not Caddy `/` → ops | Bake into `lazuar-hub-ops` |
| 9 | Staging hostname → new stack only | 01 Bar B **or** an honest “staging connected” URL that is **not** called production-ready; 02 Phase B enter gates (DB, secrets, OIDC) | Zero Hub containers on that DNS | Hub compose + 8081 sidecar |
| 10 | Default compose swap / removal | **S1 dogfood real** (host README); 01 §6 sentence; 02 Phase D enter for **default DX**, Phase B for **staging compose** | `docker compose up` is Pay+Pay DB **or** gone | `api` service becomes Pay still on 8080 |
| 11 | `task infra:up` retarget or delete | Pay DB port **5435** decided (02 §3.3, 03) | Must not fight One 5432 | Keep 5432 “we won the port” |
| 12 | `mprocs-dev.yaml` / `task fe` | 04/05 local DX documented | Pay-only procs or delete | Add 5178 next to 3003 |
| 13 | `task proxy` / Caddy 9080 | Never the Pay prod shape (02 §10.7) | Delete as Pay DX; Hub may keep until dark | `/pay` handle on Hub Caddy |
| 14 | `task tunnel:*` Hub/Aura | 06 Pay webhook URL exists if needed | New tunnel to **8081** / Pay hostname | `tunnel:api` ngrok 8080 as Pay |
| 15 | `task dev` / root README Getting Started | 02 §6.13; Phase D for delete | README = host README loop | Alias `task dev` → Hub “for old times” |
| 16 | GHCR matrix Hub five images | 02 Phase D + rollback elapsed (§9); Pay images already shipping | Stop `latest`; keep sha tags | Stop Hub CD **before** Pay CD exists |
| 17 | `ci.yml` job `dotnet` (Hub) | Pay jobs (rows 2–3) green on `main` **and** 02 Phase D | Remove or move to archive branch | Delete Hub tests to go green faster |
| 18 | `ci.yml` job `contracts` | Hub OpenAPI no longer shipped; pay-spec has its own gate | Remove; do not reuse for pay-spec honesty-allowlist | Merge honesty allowlists |
| 19 | `deploy/prod` `/root/lazuar-hub-prod` | 02 Phase C DNS + Phase D stop | Stop pulling `lazuar-hub-*` | Point Hub Caddy `/api` at Pay |
| 20 | `apps/lazuar-ops` DX (`:3003`, `VITE_API_URL`) | 04 merchant is the staff UI; 02 §6.4 kill | Stop shipping; then archive | Set `VITE_API_URL` to 8081 (P60) |
| 21 | `apps/lazuar-portal` `:3004` | 05 checkout is the buyer UI; 02 §6.5 | Same | Call Hub portal from 5179 “for compatibility” |
| 22 | `apps/lazuar-admin` `:3005` | Never a Pay merchant destination (NP-ONE-005); 02 §6.6 | Dark with Hub | Send merchants here because One Login V2 is also 3005 |
| 23 | `apps/lazuar-developers` Scalar 3002 | Pay `/v1` docs exist (pay-spec or later docs) | Stop Hub tiles | Import `/public/commerce` into pay-spec |
| 24 | `apps/lazuar-api` process | 01 Bar B + 02 Phase D + 09 data decision executed or “greenfield abandoned” | Stop process; then archive | Implement issues 261–334 so Hub can be “finished” first |
| 25 | `packages/api-spec` + `api-types-*` + lhdn-sdk | No remaining Hub client in default DX; Pay uses pay-spec | Stop `task gen` on `main` | Generate Pay DTOs with NSwag from Hub yaml |
| 26 | turbo `dev` including Hub apps | README forbids `pnpm dev`; later turbo filter Pay-only | `--filter=lazuar-pay…` or remove Hub `dev` scripts from default | Document `pnpm dev` as “full stack” |
| 27 | Hub Postgres volume `lazuar-pay_pgdata` / `lazuar_mvp` | 09: not Pay’s money. Dump if legal need, then stop | Do not migrate silently into `lazuar_pay` | Connection string copy |
| 28 | Sample `examples/hub-cashier-next` | 02 §8: pointed at Pay `/v1` or sunset | Stop teaching Hub M2M `sk_test_` | Keep as “quick demo” against 8080 |
| 29 | Root Hub README / ADRs as **shipping** docs | 02 §6.13 / §2.7 | Historical | Rewrite ADR 021 as if it described 8081 |
| 30 | Delete folders from git HEAD | Grep for `lazuar-ops` allowed empty; legal/ops ok with archive tag (02 §7.2–7.3) | `archive/hub/` or tag | Delete while Aura still posts Hub provision |

### 7.3 Gates mapped to papers 01–09 (the “not before” index)

| Paper | What must be true before Hub DX/CI goes dark |
|-------|-----------------------------------------------|
| [01](./01-production-ready-bar.md) | Bar B = 011 sentence on **new** processes. Success ≠ Hub parity. Fail locks (no password form, no second org table, buyer not Zitadel, setup ≠ paid, receipt ≠ tax invoice) still hold. |
| [02](./02-replace-old-cutover.md) | Phases A–D. Dual-run W1 (One+Pay, Hub off). No strangler. Kill tables 6.1–6.18. Rollback artifacts kept until D. |
| [03](./03-host-production-seams.md) | Pay DB, secrets (no PAT, no Hub `Jwt__Secret`), ready probe, deploy shape. Tests stay hermetic. |
| [04](./04-merchant-frontend.md) | `:5178` OIDC. Not ops routes. Not `:5173`/`:3005`. |
| [05](./05-checkout-frontend.md) | `:5179` hosted pay. Fail if login appears. |
| [06](./06-money-rails.md) | BYOK, signature verify, retry no-ops. Honest wrap-rails. |
| [07](./07-fulfillment-ledger-docs.md) | Same-handler journal + `RCPT-`. SST judgment stolen, not `Modules.Lhdn`. |
| [08](./08-one-identity-production.md) | SPA register, redirects, `lzr_sk_`, HMAC. Do not wait One staging PASSED to **develop**; production identity still One HTTP. |
| [09](./09-data-migration.md) | Greenfield default. Hub `Organization` ≠ One tenant id. No second org table. |

This paper (10) is the **sequence** of CI/compose/Taskfile kills **after** those gates, plus the **addition** of Pay CI **before** Hub CI dies.

### 7.4 What stays in git after dark (02 §7.2 — do not fight it)

| Keep in history | Stop shipping |
|-----------------|---------------|
| `apps/lazuar-api` as oracle browse / `archive/` | `task dev`, compose default, GHCR `latest` |
| `issues/001–334` markdown | Reopening as Hub PRs |
| ADRs 014–023 | Teaching them as 8081 architecture |
| Hub GHCR **sha** tags | `latest` pull on the VPS |
| SST math as **cited code** | `ProjectReference` |

### 7.5 CI decommission detail (so someone does not delete the wrong job)

| Job | Add / keep / remove | Timing |
|-----|---------------------|--------|
| **New** `pay` (`Lazuar.Pay.slnx`) | **Add first** | Next 013 implementation phase |
| **New** `pay-frontends` | **Add** with or right after `pay` | Before Bar B UI claim |
| **New** `pay-spec` (optional compile) | **Add** if tsp is cheap | Never merge with `contracts` |
| Existing `contracts` | **Keep** while Hub clients ship | Remove at Phase D |
| Existing `dotnet` Hub | **Keep** while Hub may still receive a fix | Remove at Phase D; **not** before Pay jobs exist |
| Existing `build-and-push` Hub | **Keep** for rollback until D | Add **parallel** Pay matrix entries under **new names**; do not reuse `lazuar-hub-api` |
| Existing `deploy` Hub VPS | **Keep** until Phase C/D | New deploy job to a **Pay** path, not `/root/lazuar-hub-prod` Caddy `/api` splice |

---

## 8. Footguns

Named so a later PR description can say “this is F9” without a speech.

### 8.1 F9 — `pnpm dev` / turbo `dev` starts **all** apps

Root script: `"dev": "turbo run dev --filter=!@examples/*"`. Workspace `apps/*` includes **both** museums and the new stack. `turbo.json` `dev` is `persistent: true`, `cache: false` — it will try to start every `dev` script.

| Package `name` | `dev` script | Port | Collision / lie |
|----------------|--------------|------|-----------------|
| `lazuar-api` | `dotnet watch` Hub | **8080** | Fights One. You think “Pay.” |
| `lazuar-pay` | `dotnet watch` focused | **8081** | Correct, **buried** in the pile |
| `lazuar-ops` | Vite | **3003** | Hub cookie IdP |
| `lazuar-portal` | Next | **3004** | Hub checkout |
| `lazuar-admin` | Vite | **3005** | Fights One Login V2 |
| `lazuar-developers` | Next | **3002** | Hub Scalar |
| `lazuar-docs` | VitePress | **5180** | Fights One docs |
| `lazuar-pay-merchant` | Vite `strictPort` | **5178** | Correct |
| `lazuar-pay-checkout` | Vite `strictPort` | **5179** | Correct |

`examples/*` is filtered out (`@examples/*`). Hub **apps** are not.

`pnpm test` / `pnpm build` / `pnpm check-types` are the same turbo pattern. Running them in CI as “the monorepo gate” would compile Hub + Pay together and hide a red IsolationTests behind a Hub architecture-test timeout.

**DX rule:** document `pnpm --filter lazuar-pay…` and `task pay:*`. Treat unqualified `pnpm dev` as a **bug** in Getting Started.

### 8.2 F-3005 — Hub admin and One Login V2

| Process | Port 3005 |
|---------|-----------|
| Hub `lazuar-admin` | Vite `dev --port=3005`; compose `full` `3005:3000`; Caddy `/admin` |
| One stock Login V2 | Compose always-on `3005:3000` (012/05, 02 §3.1) |

Shipping merchants to `:3005` is ambiguous **and** `NP-ONE-005` / `NP-XX-018`. Merchant homepage is **5178**. Password UI is One **5175**. One staff SPA is **5173** (also forbidden for merchants).

`CorsTests` deny **3003** (ops), not 3005. Do not “fix” CORS by allowing 3005.

### 8.3 F-8080 — one laptop, one process

| Occupant | How you started it | Whoami `One:BaseUrl=http://localhost:8080/api/v1` talks to |
|----------|--------------------|--------------------------------------------------------------|
| One API | One repo | **Correct** |
| Hub `task dev` / compose `api` | This repo | Hub `/me` or 404 — looks like “One is broken” (012/10 §11) |
| Nothing | — | Connection refused → Pay whoami 503 |

Default `One:BaseUrl` in `appsettings.json` is correct **only** when One occupies 8080. Compose `api` publishing 8080 next to a running Pay process is how live whoami lies.

After Hub is gone: Pay still does **not** bind 8080. One keeps 8080. Pay keeps 8081 (02 §10.5).

### 8.4 F-VITE — ops `VITE_API_URL` (P60)

```ts
// apps/lazuar-ops/src/lib/api-client.ts
export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
```

Compose build-arg and bake default the same Hub URL (prod `https://hub.lazuar.com/api/v1`). Ops uses Hub cookie `lazuar_auth`, `POST /one/auth/login`, `@repo/api-types-ts`, header `X-Tenant-Id` (old name).

Setting this to `http://localhost:8081/api/v1` or `:8081` **fails today** (P60.2): Pay has no password login; whoami is not `GET /one/auth/me`; hundreds of ops routes do not exist on 8081.

Merchant env is `VITE_PAY_API_URL` → `http://localhost:8081` (merchant README). Different name on purpose.

Portal `NEXT_PUBLIC_API_URL` / SSR `API_URL: http://api:8080/api/v1` is the same footgun for checkout. Compose DNS `api` is Hub.

### 8.5 Other named traps (short)

| Id | Trap | Refuse |
|----|------|--------|
| F-turbo-test | `pnpm test` runs Hub `dotnet test` **and** Pay tests | CI uses `task pay:test` in `apps/lazuar-pay` |
| F-gen | “One generate pipeline” | `task pay:spec` ≠ `task gen` |
| F-honesty | pay-spec paths added to `honesty-allowlist.yaml` | Allowlist is Hub Minimal vs Hub OpenAPI |
| F-9080 | Caddy `/health` → host 8080 | Whatever owns 8080, path map still says Hub |
| F-5432 | `task infra:up` during One+Pay | One remap 5433 is **One’s** workaround; Pay money is 5435 |
| F-8090 | `task tunnel:cf` hop A | OpenFGA HTTP. Not Pay |
| F-name | Image `lazuar-hub-api` described as Pay | 02 §10.17 |
| F-ready | Ready probe calls One | Buyer pay dies when membership lags |
| F-playwright-ops | E2e that logs into ops 3003 | Wrong product |
| F-ci-postgres | Pay tests use `LAZUAR_TEST_PG` / `lazuar_mvp` | 09 greenfield `lazuar_pay` |
| F-docs-5180 | Both repos’ VitePress | Pick one per laptop session |
| F-mprocs | `task fe` after merchant exists | Still starts 3003–3005 |

---

## 9. Anti-goals

If a later checklist or PR does any of these, it is out of program 013 even if tests are green.

1. **Make `task pay:test` start Compose, Zitadel, or live One.** Hermetic is locked.
2. **Delete IsolationTests** or allow `MediatR` / `BuildingBlocks` / `Modules.` / `lazuar-api` references.
3. **Hook `packages/pay-spec` into `task gen`, NSwag, Kiota LHDN, or `contracts:honesty`.** P60.3; 012/10 §12.2.
4. **Run `pnpm build` / `pnpm test` / `pnpm dev` turbo as the Pay CI/DX story.**
5. **Retarget `lazuar-ops` / `lazuar-portal` `VITE_API_URL` / `NEXT_PUBLIC_API_URL` at 8081.** P60. 02 §10.1.
6. **Swap default compose to Pay before S1 dogfood is real** — or swap by putting Pay on **8080**.
7. **Add Pay as a sibling service on `deploy/prod` network `hub` while Caddy `/` still serves ops.**
8. **Stop Hub `ci.yml` `dotnet` / `ghcr.yml` before Pay jobs and Pay images exist.** Going dark on both.
9. **Require One staging PASSED to merge Pay PRs.**
10. **Add Playwright as the first frontend lock** on health-probe SPAs.
11. **Copy Hub `/health/metrics`, outbox-lag ready, `lhdn_stuck_count`, App Insights SDK.**
12. **Ready probe that depends on One / Zitadel / OpenFGA.**
13. **Bind focused Pay to 8080** after Hub dies “to simplify Caddy.”
14. **Use host 3005 or One `:5173` as merchant DX.**
15. **Tunnel Pay webhooks through `task tunnel:cf` :8090 or ngrok 8080 Hub paths.**
16. **Implement issues 261–334 on the cathedral as a gate to delete it.** 011 binding; 02 §10.13.
17. **Share Hub cookie `lazuar_auth` with One or Pay.**
18. **Second org table / Hub `Organization` id as `org_id`.** 09.
19. **Buyer as Zitadel human; Pay password form.**
20. **Mega-merge One into Pay; five-deploy Notify/Media/Audit as the replace plan.**
21. **Rename Hub GHCR images to `lazuar-pay-*` without changing the process.**
22. **Claim production-ready because IsolationTests and whoami are green.** That is C99, not Bar B (01).
23. **Go rewrite / second Pay tree in this program’s CI.** 011/05 is out of 013.
24. **Treat this analysis folder as an implementation checklist** and flip 011/11 cells from this file.

---

## 10. Suggested later 013 implementation phase **names** only

Analyses 01–10 stay evidence. A **later** program (checklists, not this folder) implements. Names below are **handles** for that program. They are not tickets, not a mega-PR, not a schedule. Do not expand them here.

| Name | One-line intent (name only — details live in 01–10) |
|------|------------------------------------------------------|
| `013-CI-HERMETIC` | GitHub job: `task pay:test` / `Lazuar.Pay.slnx` |
| `013-CI-FRONTENDS` | GitHub job: merchant + checkout `check-types` + `build` |
| `013-CI-PAY-SPEC` | Optional PR compile of `packages/pay-spec` (not `task gen`) |
| `013-OBS-LOGS` | JSON console + request id + One upstream status (no secrets) |
| `013-OBS-PROBES` | `/health/ready` after Pay DB; liveness never calls One |
| `013-OBS-METRICS` | Process metrics after real charges; not Hub `/health/metrics` |
| `013-DX-TASKS` | README/Taskfile: `pay:dev` + merchant + checkout + One; stale desc hygiene |
| `013-DX-TURBO` | Stop documenting `pnpm dev` as full stack; filter or split |
| `013-STAGE-TOPOLOGY` | Staging hostname = new stack only; 8080 = One |
| `013-COMPOSE-SWAP` | After Bar B: default compose is Pay+5435 **or** removed |
| `013-GHCR-PAY` | Bake/push `lazuar-pay*` images; Pay CD path ≠ Hub VPS splice |
| `013-HUB-CI-SUNSET` | Remove Hub `dotnet` / `contracts` / Hub matrix **after** Pay CI+CD exist and Phase D |
| `013-HUB-DX-DARK` | `task dev`, `fe`, `proxy`, Hub tunnels, mprocs, root Getting Started |
| `013-HUB-TREE-ARCHIVE` | Folders to `archive/` / tag after grep is allowed empty |

Host/money/UI implementation names already belong to the papers that own them (`013-HOST-SEAMS`, merchant, checkout, rails, ledger, One identity, data). This list is **only** CI / observability / staging / decommission.

---

## 11. Open questions this paper does not close

Not invitations to reverse § locks.

1. **Exact GitHub job ids** (`pay` vs `pay-dotnet`) and whether they live in `ci.yml` or `pay.yml`. Shape is §2.7; filename is later.
2. **Whether `task pay:spec` is required on every PR** or only when `packages/pay-spec` changes. Lean: path filter. Do not merge with Hub `contracts`.
3. **Serilog vs MEL JSON** — 03 §10.2 / §10.10. This paper requires structured logs, not a brand.
4. **Prometheus `/metrics` vs cloud agent** — pick at `013-OBS-METRICS`.
5. **Pay GHCR names** (`lazuar-pay` vs `lazuar-pay-api` vs merchant/checkout static). Must not be `lazuar-hub-*`.
6. **`task pay:stack` convenience** vs three tasks. Must not be named `dev`.
7. **Playwright in PR vs nightly** — after 04/05 exist. Buyer context isolation is non-negotiable; runner shape is not.
8. **Hub CI on a long-lived `archive/hub` branch** vs delete jobs on `main` at Phase D.
9. **Whether VitePress 5180 Hub docs are rewritten or dropped** — 02 §6.14; not a CI gate.
10. **Production hostnames / TLS** — 01 open question 1; 03 Caddy; 02 Phase C. This paper only forbids Hub path-map splice.

---

## 12. Evidence index (paths opened)

### Pay repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`) at `6f866ff0`

- `Taskfile.yml` — `pay:*`, `api:*`, `infra:*`, `fe`, `proxy*`, `tunnel:*`, `gen*`, `contracts:honesty`, `docker:*`
- `turbo.json`, `pnpm-workspace.yaml`, `package.json`
- `apps/lazuar-pay/package.json`, `README.md`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json` (`http://localhost:8081`)
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/` — `IsolationTests.cs`, `HealthTests.cs`, `CorsTests.cs`, `WhoamiTests.cs`, `OrgReadyTests.cs`, `CheckoutTests.cs`, `PayApiFactory.cs`, `FakeOneHandler.cs`
- `apps/lazuar-pay-merchant/package.json`, `README.md`
- `apps/lazuar-pay-checkout/package.json`, `README.md`
- `apps/lazuar-ops/package.json`, `src/lib/api-client.ts` (`VITE_API_URL` default 8080)
- `apps/lazuar-portal/package.json`, `apps/lazuar-admin/package.json`, `apps/lazuar-api/package.json`, `apps/lazuar-developers/package.json`, `apps/lazuar-docs/package.json`
- `.github/workflows/ci.yml` — jobs `contracts`, `dotnet`
- `.github/workflows/ghcr.yml` — jobs `build-and-push`, `deploy`; matrix five `lazuar-hub-*`
- `docker-compose.yml`, `docker-compose.ghcr.yml`, `docker-compose.dev-proxy.yml`, `deploy/prod/docker-compose.yml`
- `docker-bake.hcl`, `mprocs-dev.yaml`, `deploy/dev/Caddyfile`
- `packages/pay-spec/package.json`, `README.md`

### Plans

- `plans/013-prods/README.md`, `01`–`09` (headers + cited sections; **not** rewritten)
- `plans/012-one-to-pay/10-dogfood-and-tests.md` (structure + §11–12)
- `plans/012-one-to-pay/checklists/p60-old-frontends.md`

### One repo at `0f79fe4`

- Ports and staging honesty as already recorded in 01 §7 / 02 §3 / 012/10 — not re-inventoried in this write.

---

## 13. What “done” looks like for this slice (analysis bar)

This paper is done if a later implementer can:

1. Add a GitHub job that runs `Lazuar.Pay.slnx` **without** touching Hub honesty or Hub Postgres.
2. Add frontend typecheck/build for **5178/5179 only**.
3. Leave compose Hub-shaped until Bar B, then swap **without** retargeting ops.
4. Put JSON logs and a One-free ready probe on the focused host without `BuildingBlocks`.
5. Tell an engineer to run `task pay:dev` + `pay:merchant` + `pay:checkout` + One, and to treat `pnpm dev` as a footgun.
6. Decommission Hub CI/CD/DX in the **order** in §7, with **not before** pointers into 01–09, especially 01–02.

It is **not** done by merging workflows from this file (there are none to merge). It is not done by deleting `ci.yml` `dotnet`. It is not done by a green whoami.

**Do not implement from this file.**
