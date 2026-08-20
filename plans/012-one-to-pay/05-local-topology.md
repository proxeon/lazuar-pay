# 05 — Local topology: run Lazuar One and new Pay together

**Date:** 20 August 2026  
**Kind:** analysis only. No implementation in this slice.  
**Audience:** a developer on one laptop who wants a `GET /me` (whoami) proof: One identity plane live, focused Pay process live, neither process pretending to be the other.

This paper is about **ports, processes, Docker networks, CORS, cookies vs Bearer, health probes, demo logins, and the 8080 footgun**. It does not design Pay’s OIDC client, webhook HMAC, or money loop. Those live in [011 02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) and [011 03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md).

---

## Repos and SHAs (as read)

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **Lazuar One** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |
| **Lazuar Pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-one-to-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `6ca8f19f` | `feat(pay): add TypeSpec package for the focused Pay host` (2026-08-20 21:00:06 +0800) |

Working trees were clean at read time (`git status --porcelain` empty on both).

**.NET SDK pin (both hosts):** `10.0.100`, `rollForward: latestFeature`. One additionally `allowPrerelease: true` in `apps/lazuar-api/global.json`.

**pnpm pin (not a port, still a laptop trap):** One `packageManager: pnpm@9.0.0`. Pay `packageManager: pnpm@11.5.2`. Install each repo with its own pin. Do not assume one `pnpm install` at a parent of both checkouts.

---

## Sources actually read

### One (sibling)

- Root `README.md` — first-time local, demo users, port summary, CORS note, health URLs.
- Root `docker-compose.yml` — project `name: lazuar-one`, network `lazuar-one-network`, published ports, optional profile `api`.
- Root `Taskfile.yml` — `bootstrap`, `compose:up`, `fga`, `login-pat`, `seed`, `api` (:8080), `app` (:5174), `admin` (:5173), `login` (:5175).
- `apps/lazuar-docs/docs/reference/ports.md` — the ports table the prompt called `docs/reference/ports.md`. It is **not** at repo-root `docs/reference/ports.md` (that path does not exist). VitePress serves it from `apps/lazuar-docs/docs/`.
- `scripts/bootstrap-local.sh` — compose wait → OpenFGA → login-client PAT → seed.
- `scripts/prove-local-stack.sh`, `scripts/seed-dev-demo.sh`, `deploy/dev/smoke.sh`.
- `deploy/dev/README.md` — hybrid DX, host env overrides, stub vs real, Login V2 cutover, **explicit note that host 5432 is often taken by lazuar-pay**.
- `deploy/dev/postgres/init/01-databases.sql` — creates `zitadel`, `openfga`, `lazuar`.
- `.env.example` — image pins, published ports, Login V2 URLs pointing at `:5175`.
- `apps/lazuar-api` launchSettings, `appsettings.json` / `appsettings.Development.json`, `Program.cs` CORS + health mapping, `Configuration/CorsOriginList.cs`, `Infrastructure/Health/HealthEndpoints.cs`.
- `apps/lazuar-login` `.env.example`, `vite.config.ts` (5175 → proxy 5176), `src/server/cookies.ts`, `src/server/config.ts` cookie names, `src/server/routes/health.ts`.
- `apps/lazuar-app` `.env.example`, `src/auth/oidcConfig.ts` (sessionStorage), `src/auth/bearerToken.ts`, `src/api/client.ts`.
- `apps/lazuar-docs/docs/local/api.md`, `bootstrap-platform.md`, `spa-oidc-setup.md`, `docker-identity.md`.
- `apps/e2e/helpers/urls.ts` — E2E defaults 5174 / 5175 / 8080 / 5173.

### Pay (this repo)

- Root `README.md` — old-stack boot (`task infra:up`, `task dev`, `task fe`), old demo accounts, old port map, optional Caddy `:9080`, dual-run `:8090` next to Aura.
- Root `docker-compose.yml` — **db + old api by default**, frontends on profile `full`. Network name `lazuar-network`. Container `lazuar-api` on **8080**.
- `docker-compose.dev-proxy.yml` — Caddy `:9080`, project `lazuar-dev-proxy`, `/health` and `/api/*` → host `:8080`.
- `docker-compose.ghcr.yml` — same host ports as local compose; project `lazuar-hub`; still `container_name: lazuar-api` on 8080.
- Root `Taskfile.yml` — `pay:dev` (8081), `dev` (old API 8080), `infra:*`, `fe`, `proxy`, `tunnel:cf` (Pay dual-run **8090**).
- `apps/lazuar-pay/README.md`, `Properties/launchSettings.json` (`http://localhost:8081`), `Program.cs` (only `/health` and `/v1/health`), `appsettings.json` (no One URL yet), `package.json` `dev` script, tests `HealthTests.cs`.
- `packages/pay-spec/main.tsp` — `@server("http://localhost:8081")`, `GET /v1/health`.
- Old API `apps/lazuar-api` launchSettings (8080), `Composition/HealthEndpointExtensions.cs`, `Composition/AuthAndCorsExtensions.cs` (JWT **and** cookies `lazuar_auth` / `lazuar_admin_auth`), `appsettings.Development.json` CORS + demo users.
- `mprocs-dev.yaml`, `deploy/dev/Caddyfile`.

### Plan papers

- `plans/011-new-lazuar-pay/02-one-integration.md` — Pay is a separate origin; Bearer access_token; product login `:5175`; One API `http://localhost:8080/api/v1`; authority `http://localhost:8085`.
- `plans/011-new-lazuar-pay/03-first-slice.md` and `12-first-slice-tracker.md` — step 2 is sign-in via `:5175` then `GET /me`.
- `plans/011-new-lazuar-pay/01-product.md`, `11-checklist.md` (NP-ONE-003, NP-ONE-005, NP-ONE-006).

---

## What this topology is trying to prove

**Whoami proof (S0, backend-first):**

1. One identity stack (Postgres + Zitadel + OpenFGA) is up.
2. One API answers on **8080** and is actually One (not old Pay).
3. Product login answers on **5175**.
4. Focused Pay answers on **8081**.
5. A human JWT (or later a `lzr_sk_`) can call `GET http://localhost:8080/api/v1/me` with `Authorization: Bearer`.
6. Focused Pay does **not** need a browser origin yet. `curl` against both health endpoints is enough for process liveness. CORS is not on the critical path for this proof.

**Explicitly not this paper’s proof:** staging PASSED, One SMTP, Pay checkout, Billplz hop A, old Hub Caddy path map, Aura dual-run.

Honesty from 011: One staging proof is **NOT PASSED**. Local compose dogfood is the path.

---

## Two products, overlapping names

Both checkouts use the folder `apps/lazuar-api` and the pnpm package name `lazuar-api`. Both have `apps/lazuar-admin` and `apps/lazuar-docs`. Docker Compose in both places wants a container named `lazuar-api`. Humans say “the API on 8080” and mean opposite processes.

| Name you will type | In One | In Pay |
|--------------------|--------|--------|
| `apps/lazuar-api` | `Lazuar.One.Api`, identity, **8080** | Old modular Hub monolith, money + fake-One, **8080** |
| `apps/lazuar-pay` | (does not exist) | Focused money host, **8081** |
| `apps/lazuar-admin` | Staff SPA **5173** | Old platform admin Vite **3005** |
| `apps/lazuar-docs` | VitePress **5180** | VitePress **5180** |
| `lazuar-app` | Customer SPA **5174** | (does not exist; closest is `lazuar-ops` **3003**) |
| `lazuar-login` | Product login BFF **5175** | (does not exist) |
| `docker compose up` | Identity only (API is a **profile**) | **db + old API** (frontends are a profile) |
| `task dev` | (no such task; use `task api`) | Old API on 8080 via `pnpm --filter lazuar-api dev` |
| `task pay:dev` | (does not exist) | Focused Pay on 8081 |

If a sentence does not say **One** vs **old Pay** vs **focused Pay**, assume it is wrong.

---

## How a developer actually boots One

Default DX is **hybrid**: Docker for Postgres / Zitadel / OpenFGA / stock Login V2; **host processes** for One API and all SPAs. Compose leaves 5173–5181 and **8080** free on purpose.

### 1. Identity containers

From `/Users/akmalfirdaus/Code/lazuar/lazuar-one`:

```bash
cp .env.example .env
docker compose up -d --wait
# equivalent Taskfile:
#   task compose:up
```

Compose project name is **`lazuar-one`** (`name: lazuar-one` in `docker-compose.yml`). Network is created as **`lazuar-one-network`** (the compose key is `lazuar`; the explicit `name:` avoids colliding with Pay’s already-claimed `lazuar-network` — the file says this in a comment).

Containers started by the **default** compose (no profiles):

| Compose service | `container_name` | Image pin (`.env.example`) | Host publish |
|-----------------|------------------|----------------------------|--------------|
| `postgres` | `lazuar-postgres` | `postgres:17.10-alpine` | `5432:5432` (override `POSTGRES_PUBLISHED_PORT`) |
| `openfga-migrate` | `lazuar-openfga-migrate` | `openfga/openfga:v1.18.3` | none (oneshot) |
| `openfga` | `lazuar-openfga` | same | `8090:8080` HTTP, `8091:8081` gRPC, `3009:3000` playground, `2112:2112` metrics |
| `zitadel-api` | `lazuar-zitadel-api` | `ghcr.io/zitadel/zitadel:v4.16.0` | `8085:8080` |
| `zitadel-login` | `lazuar-zitadel-login` | `ghcr.io/zitadel/zitadel-login:v4.16.0` | `3005:3000` stock Login V2 |

**Not started by default:** `api` (profile `api`). Host `pnpm api:dev` is the documented path.

Inside the Docker network, Zitadel listens on container port **8080** (`ZITADEL_PORT: 8080`). That is **not** host 8080. Host sees Zitadel at **8085**. OpenFGA’s container 8080/8081 are host **8090/8091**. Focused Pay’s host **8081** does not collide with OpenFGA’s **container** 8081.

First-boot init SQL (empty volume only) creates databases `zitadel`, `openfga`, `lazuar` on that Postgres. Connection for host-run One API:

```
Host=localhost;Port=5432;Database=lazuar;Username=postgres;Password=postgres
```

If 5432 is already taken (Pay’s `lazuar-db` is the documented case), set in One `.env`:

```bash
POSTGRES_PUBLISHED_PORT=5433
```

and for the **host** API:

```bash
export ConnectionStrings__Lazuar="Host=localhost;Port=5433;Database=lazuar;Username=postgres;Password=postgres"
```

Container-internal DSNs stay `postgres:5432`. Do not change those when only the **published** port moved.

### 2. Umbrella bootstrap (preferred over clicking Console)

```bash
./scripts/bootstrap-local.sh
# equivalent:
#   task bootstrap
```

The script, in order:

1. Copies `.env.example` → `.env` if missing.
2. `docker compose up -d --wait`.
3. `./deploy/dev/openfga/bootstrap.sh` (warn, do not abort, if OpenFGA is unhappy). Writes gitignored `deploy/dev/openfga/.env.local` with store/model ids.
4. `./scripts/login-dogfood-setup.sh` — copies login-client PAT out of the Zitadel volume into `apps/lazuar-login/.secrets/login-client.pat` and ensures `apps/lazuar-login/.env`. This PAT is **not** `ZITADEL_PAT`.
5. `WRITE_ENV=1 ./scripts/seed-dev-demo.sh` — demo human `ada@acme.test` + platform SPA clients when a Management PAT is available. Warn, do not abort, on failure.

Printed next steps from the script itself:

```text
pnpm install
pnpm login:dev    # :5175
pnpm api:dev      # :8080
pnpm app:dev      # :5174
```

Demo logins printed there: customer `ada@acme.test` / `Password1!` on `:5174`; staff `zitadel-admin@zitadel.localhost` / `Password1!` on `:5173`.

Taskfile splits the same work if you want pieces:

| Task | What |
|------|------|
| `task compose:up` | Postgres + Zitadel + OpenFGA |
| `task fga` | OpenFGA store/model |
| `task login-pat` | login-client PAT extract |
| `task seed:users` | ada only |
| `task seed:spas` / `task seed` | users + `lazuar-app` / `lazuar-admin` OIDC clients (`WRITE_ENV=1`) |
| `task bootstrap` | all of the above via the umbrella script |

`ZITADEL_PAT` is a **Management API** token for `seed-platform-spa-clients.sh` only. Login-client PAT must never be set as `Zitadel__ServiceUserToken` on the API, and the provisioner PAT must never go in `lazuar-login`.

### 3. Host processes (long-running; bootstrap does not start them)

`scripts/prove-local-stack.sh` says this out loud: it will re-bootstrap compose, then **fail** if `http://localhost:8080/health` is down, because it will not start the API/login/app for you.

| Want | Command (from One repo root) | Taskfile | Listen |
|------|------------------------------|----------|--------|
| One API | `pnpm api:dev` | `task api` | **http://localhost:8080** (`launchSettings` profile `http`, `ASPNETCORE_ENVIRONMENT=Development`) |
| Product login UI + BFF | `pnpm login:dev` | `task login` | UI **5175**, BFF **5176** (Vite proxies `/api`, `/health`, `/logout` → 5176 so cookies stay same-origin to 5175) |
| Customer SPA | `pnpm app:dev` | `task app` | **5174** |
| Staff SPA | `pnpm admin:dev` | `task admin` | **5173** |
| VitePress docs | `pnpm docs:dev` | (no task; pnpm only) | **5180** |
| Scalar reference | `pnpm reference:dev` | `task reference` | **5181** |
| Example SPA | `pnpm --filter example-vite-spa dev` | — | **5177** |
| EF schema | `pnpm api:migrate` | — | writes `lazuar` DB; serving replica does **not** auto-migrate |

`pnpm api:dev` is `pnpm --filter lazuar-api dev` → `dotnet watch run --project src/Lazuar.One.Api/Lazuar.One.Api.csproj --launch-profile http`.

`pnpm dev` (turbo everything with a `dev` script) will try to start API + all SPAs + docs at once. That is loud. For whoami, start the three host processes you need in separate terminals.

Optional: put the API **in Docker**:

```bash
docker compose --profile api up -d --build
```

That publishes `${API_PUBLISHED_PORT:-8080}:8080`, sets `container_name: lazuar-api`, talks to `zitadel-api:8080` / `openfga:8080` / `postgres:5432` on `lazuar-one-network`, and healthchecks `curl -sf http://127.0.0.1:8080/health/ready`. **Do not do this** while Pay’s compose API is up (same container name, same host 8080). Default DX does not need this profile.

### 4. Login cutover (5175 vs 3005)

Compose / `.env.example` default Login V2 URLs:

```text
Login:  http://localhost:5175/login?authRequest=
Logout: http://localhost:5175/logout?post_logout_redirect=
Base:   http://localhost:5175/
```

Stock Login V2 remains published at **3005** as break-glass (`zitadel-login` container). Existing Zitadel volumes often ignore first-instance `DEFAULT*` env on recreate. If Sign in still lands on `:3005`, run:

```bash
./scripts/login-dogfood-setup.sh --apply-root-cutover
docker compose up -d --force-recreate zitadel-api
# or set the same URLs in Zitadel Console instance Login V2 settings
```

Rollback to stock (minutes, no SPA change): `--apply-root-rollback` or restore `:3005/ui/v2/login/…` URLs.

SPA `VITE_ZITADEL_*` authority stays **8085** either way. The login **UI host is not** in the SPA env. Zitadel redirects the browser to whichever Login V2 URL the instance has.

### 5. Development defaults that affect dogfood quality

From `apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json`:

- `Zitadel:UseStub=true` — tenant create uses synthetic `stub-org-*`. JWT **validation** is still real JWKS against `http://localhost:8085` when you send a real access token. Whoami (`GET /me`) with Ada’s JWT works in stub mode. Real org ids need `Zitadel__UseStub=false` + provisioner PAT.
- `OpenFga:Enabled=false` — FGA writes skipped. `GET /health/ready` still becomes ready when Postgres is up (Zitadel skipped because stub, FGA skipped because disabled). `prove-local-stack.sh` **rejects** a ready body with `"skipped": true`. That script is stricter than “API process is up.”
- `Invite:ReturnTokenInResponse=true` — local DX only.
- CORS CSV includes 5173/5174/5177/5180/5181 and 127.0.0.1 twins (Development overlay). **5175 is not a CORS origin** (BFF is same-origin to the login UI). **8081 is not a CORS origin.**

### 6. Prove One is up (before touching Pay)

```bash
# identity
curl -sf "http://localhost:8085/.well-known/openid-configuration" | head
curl -sf "http://localhost:3005/ui/v2/login/healthy"
curl -sf "http://localhost:8090/healthz"

# product login (after pnpm login:dev)
curl -sf "http://localhost:5175/health"
# expect JSON including "service":"lazuar-login"

# API (after pnpm api:dev)
curl -sf "http://localhost:8080/health"
curl -sf "http://localhost:8080/health/live"
curl -sf "http://localhost:8080/health/ready"
curl -sf "http://localhost:8080/api/v1/health"
curl -sf "http://localhost:8080/api/v1/"
# expect {"name":"lazuar-one-api","version":"v1"}

./deploy/dev/smoke.sh
# live + ready + GET /api/v1/me → 401
```

---

## How a developer actually boots Pay

There are **three** Pay-side ways to get an HTTP server. Only one is the new product.

### A. Focused Pay (the one this plan wants) — `task pay:dev`

From `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`:

```bash
task pay:dev
# Taskfile: dir apps/lazuar-pay
#   dotnet watch run --project src/Lazuar.Pay/Lazuar.Pay.csproj
#
# equivalent:
pnpm --filter lazuar-pay dev
# or:
cd apps/lazuar-pay && pnpm dev
```

`Properties/launchSettings.json` profile `http`:

- `applicationUrl`: **`http://localhost:8081`**
- `ASPNETCORE_ENVIRONMENT`: `Development`
- `launchBrowser`: false

`Program.cs` today maps only:

- `GET /health` → `{ "status": "ok" }`
- `GET /v1/health` → `{ "status": "ok" }`

No CORS middleware. No auth. No database. No `.env` loader (unlike old `Lazuar.Api` Program.cs, which reads a repo-root `.env` by walking `../../../../.env` from cwd). No `ONE_API_URL` yet — see env sketch below.

`packages/pay-spec/main.tsp` documents the same listen URL: `@server("http://localhost:8081", "Local focused Pay host")`.

This task does **not** start Docker. It does **not** take 8080. That is the whole point of 8081 (`apps/lazuar-pay/README.md`: “Listen on **8081** so the old API can keep **8080**”).

### B. Old Hub API (do **not** start for One+Pay whoami)

```bash
task infra:up     # docker-compose up db -d  → host 5432, container lazuar-db, db lazuar_mvp
task dev          # deps infra:up, then pnpm --filter lazuar-api dev
                  # → apps/lazuar-api launchSettings http://localhost:8080
```

`task fe` → `mprocs -c mprocs-dev.yaml`: developers **3002**, ops **3003**, portal **3004**, admin **3005** (`strictPort: true` on Vite ops/admin).

`task proxy` / `task proxy:up` → Caddy **9080**, and **`handle /health` reverse_proxies host `:8080`**. If One is on 8080, the Pay gateway will health-check **One** and call it Hub. If old Pay is on 8080, `/api/*` is the old monolith.

`docker compose up -d --build` (Pay root, no extra flags) starts **db + old api**. Frontends need `--profile full`. This is the opposite of One’s default compose (identity only, API opt-in).

### C. Dual-run next to Aura (also do **not** start next to One)

Root README and `task tunnel:cf`:

- Aura owns **8080**.
- Old Pay listens **8090**.
- Named Cloudflare tunnel `pay-local.lazuar.dev` → `127.0.0.1:8090`.

One’s OpenFGA HTTP is **already** host **8090**. Dual-run Pay and One compose **cannot** share a laptop without remapping one of them. This mode is for Aura, not for Consumer-0 One dogfood.

### D. What Pay compose files bind, in full

**`docker-compose.yml`** (project name defaults to directory **`lazuar-pay`** because `name:` is unset):

| Service | Profile | `container_name` | Host |
|---------|---------|------------------|------|
| `db` | default | `lazuar-db` | **5432** |
| `api` | default | **`lazuar-api`** | **8080** |
| `lazuar-ops` | `full` | `lazuar-ops` | 3003 |
| `lazuar-portal` | `full` | `lazuar-portal` | 3004 |
| `lazuar-admin` | `full` | `lazuar-superadmin` | **3005** |
| `lazuar-developers` | `full` | `lazuar-developers` | 3002 |

Network: **`lazuar-network`** (explicit `name: lazuar-network`). Volume: `pgdata` → Docker name `lazuar-pay_pgdata`. Image: Postgres **16**-alpine (One is **17.10**-alpine). Database: `lazuar_mvp` (One app DB is `lazuar`).

**`docker-compose.ghcr.yml`**: project `lazuar-hub`, same host ports, same `container_name: lazuar-api`, same `lazuar-network`.

**`docker-compose.dev-proxy.yml`**: project `lazuar-dev-proxy`, `container_name: lazuar-dev-caddy`, host **9080**, `extra_hosts: host.docker.internal:host-gateway`. No Postgres. Upstreams are **host** ports, not compose DNS.

---

## Full port table (both products, one laptop)

Listen addresses are **host** ports unless marked “container only”. “Taken by” is the process that **claims** the port when that product is running its default/local DX. Empty “conflict if both” means the other product does not publish it.

| Host port | One | Pay | Conflict if both default DX? | Whoami need? |
|-----------|-----|-----|------------------------------|--------------|
| **3000** | Legacy `apps/web` (docs still list it) | Old CORS list includes it; not a current app listen | Unlikely | No |
| **3001** | Legacy docs | Old CORS list | Unlikely | No |
| **3002** | — | `lazuar-developers` (`next dev -p 3002`); compose `full` 3002:3000 | No | No |
| **3003** | — | `lazuar-ops` Vite `strictPort` 3003; compose `full` | No | No |
| **3004** | — | `lazuar-portal` `next dev -p 3004`; compose `full` | No | No |
| **3005** | **zitadel-login** stock Login V2 (compose, always-on) | **lazuar-admin** Vite `strictPort` 3005; compose `full` 3005:3000; Caddy `/admin` | **YES** if Pay admin or `profile full` | One wants 3005 as break-glass. Pay admin must stay down. |
| **3009** | OpenFGA playground | — | No | No (debug) |
| **3020** | — | `examples/hub-cashier-next` optional | No | No |
| **4040** | — | ngrok agent API (`task tunnel:status`) | No | No |
| **5173** | **lazuar-admin** staff SPA | — (Pay admin is 3005) | No | Optional (staff). Merchants never use this (011). |
| **5174** | **lazuar-app** customer SPA | — | No | **Yes for interactive token mint.** Curl-only whoami can skip if you already have a JWT. |
| **5175** | **lazuar-login** product UI | — | No | **Yes** for the S0 sign-in path. Not Pay’s homepage. |
| **5176** | Login BFF loopback (proxied by 5175) | — | No | Yes, implied by `pnpm login:dev`. Do not open 5176 in the browser as the product URL. |
| **5177** | `examples/vite-spa` | — | No | No |
| **5180** | **lazuar-docs** VitePress | **lazuar-docs** VitePress | **YES** if both `pnpm docs:dev` | No |
| **5181** | **lazuar-reference** Scalar | — | No | No |
| **5432** | Compose Postgres 17 (`lazuar-postgres`) DBs `zitadel`/`openfga`/`lazuar` | Compose Postgres 16 (`lazuar-db`) DB `lazuar_mvp`; `task infra:up` | **YES** | **One needs it** (or remapped 5433). Focused Pay has **no DB yet** — leave Pay db down. |
| **8025 / 1025** | Optional Mailpit (not in compose) | — | No | No unless invite email dogfood |
| **8080** | **One API** host `pnpm api:dev` **or** compose profile `api` | **Old Hub API** host `task dev` **or** compose `api` **or** GHCR `api`; Caddy `/health` and `/api/*` | **YES — the collision this paper exists for** | **One API must own 8080.** Old Pay must not. |
| **8081** | OpenFGA **container** gRPC 8081 (published as **host 8091**, not 8081) | **Focused Pay** `task pay:dev` / launchSettings | **No** (host 8081 is free relative to One) | **Yes** |
| **8085** | **Zitadel API** / issuer / Console | — | No | **Yes** (OIDC discovery + JWKS). Login `.env` `ZITADEL_API_URL=http://localhost:8085`. One API `Zitadel:Authority=http://localhost:8085`. |
| **8090** | **OpenFGA HTTP** | Old Pay **dual-run** listen (Aura hop A); old CORS list includes 8090 | **YES** if anyone starts `task tunnel:cf` / dual-run Pay | One needs 8090 for FGA. Whoami can run with `OpenFga:Enabled=false`, but default One compose still **binds** 8090. Do not start Pay-on-8090. |
| **8091** | OpenFGA gRPC | — | No | No (HTTP 8090 is enough) |
| **9000** | — | MinIO-shaped R2 in old `appsettings.Development.json` (not default compose) | No | No |
| **9080** | Mentioned as a *future* One proxy idea (`deploy/dev/README.md`); not implemented | **Caddy** local gateway (`task proxy`) | Only if One later takes 9080 | **No. Keep down.** `/health` on 9080 is old-Pay-shaped and points at 8080. |
| **2112** | OpenFGA metrics | — | No | No |

### Container ports that are easy to misread

| Process | Container listen | Host publish | Misread |
|---------|------------------|--------------|---------|
| Zitadel API | 8080 | **8085** | “Zitadel is on 8080” is true **inside** `lazuar-one-network` (`http://zitadel-api:8080`). Host API and browsers must use **8085**. Tokens with `iss=http://zitadel-api:8080` fail on the host API. |
| OpenFGA HTTP | 8080 | **8090** | Playground iframe often still targets **:8080**. `deploy/dev/README.md` warns: prefer curl/CLI against **8090**. That iframe is another way to “see something on 8080” that is not One. |
| OpenFGA gRPC | 8081 | **8091** | Host 8081 is **focused Pay**, not OpenFGA. |
| zitadel-login | 3000 | **3005** | Pay admin Vite also wants host 3005. |
| One API (profile `api`) | 8080 | **8080** | Same numbers, different network namespace. |
| Pay old API (compose) | 8080 | **8080** | Same as One host API. |
| Pay focused | (host process) | **8081** | No compose service yet (`apps/lazuar-pay/README.md`: “Compose still points at `apps/lazuar-api`. Swap later.”). |

---

## Docker networks, DNS, and why host ports are the integration

Three Compose projects, three networks, **no peering**:

| Project `name:` | Network `name:` | Who is on it |
|-----------------|-----------------|--------------|
| `lazuar-one` | `lazuar-one-network` | postgres, openfga, zitadel-api, zitadel-login, optional api |
| `lazuar-pay` (directory default) | `lazuar-network` | db, old api, optional frontends |
| `lazuar-dev-proxy` | default `lazuar-dev-proxy_default` | caddy only; talks to **host.docker.internal**, not to either app network |

Focused Pay is a **host** `dotnet` process. One API (default DX) is a **host** `dotnet` process. They do not need a shared Docker network. They talk `http://localhost:8080` and `http://localhost:8081`.

If someone later puts focused Pay in Compose on `lazuar-network` and One API in Compose on `lazuar-one-network`, **localhost inside a container is not the other product**. They would need either:

- published host ports + `host.docker.internal`, or
- a shared/external network, or
- an explicit extra_hosts hack.

Do not invent that for whoami. Keep both APIs on the host.

**DNS names that exist only on `lazuar-one-network`:** `postgres`, `zitadel-api`, `openfga`. One’s compose `api` profile uses those. Host-run One API must **not** use them as `Zitadel__Authority` (issuer mismatch).

**DNS names that exist only on `lazuar-network`:** `db`, `api`. Old portal compose sets `API_URL: http://api:8080/api/v1` for SSR. That `api` is old Pay, never One.

**Container name collision (hard Docker error, not just a port bind):** both files set `container_name: lazuar-api`. You cannot have One `--profile api` and Pay compose `api` on the same engine. Even with different project names, `container_name` is global.

**Volume names do not collide:** One `lazuar-one_lazuar_pgdata` + `lazuar-one_zitadel_bootstrap`; Pay `lazuar-pay_pgdata`. Data is not shared. You cannot point One at `lazuar_mvp` or Pay at `lazuar` without a deliberate (and version-skewed: PG 16 vs 17) mash-up. Do not.

---

## What cannot run at once

This is the operational matrix. “Cannot” means bind failure, wrong process, or guaranteed mis-route — not mere clutter.

### Hard bind conflicts (second starter loses)

1. **Host 8080:** One `pnpm api:dev` **xor** One compose `--profile api` **xor** Pay `task dev` / `pnpm --filter lazuar-api dev` **xor** Pay `docker compose up` (api) **xor** Pay GHCR api. One process.
2. **Host 5432:** One compose postgres **xor** Pay compose db / `task infra:up` / GHCR db. Workaround: One `POSTGRES_PUBLISHED_PORT=5433` + host connection string. Do not need the workaround if Pay db stays down.
3. **Host 3005:** One `zitadel-login` (default compose) **xor** Pay `lazuar-admin` Vite / compose `full` / mprocs. Workaround: stop Pay admin; keep stock Login V2. Or stop `zitadel-login` and lose break-glass (not recommended).
4. **Host 8090:** One OpenFGA HTTP (default compose) **xor** Pay dual-run / `task tunnel:cf`. Workaround: remap `OPENFGA_HTTP_PUBLISHED_PORT` **or** do not dual-run. For One+Pay whoami: **do not dual-run**.
5. **Host 5180:** One docs **xor** Pay docs. Neither needed for whoami.
6. **Container name `lazuar-api`:** One profile `api` **xor** Pay compose api.

### Soft / logic conflicts (binds succeed, brain fails)

7. **Pay `task proxy` (:9080)** while One owns 8080: Caddy `/health` and `/api/*` silently become One. Old Hub frontends on 3003–3005 talking to `VITE_API_URL=http://localhost:8080/api/v1` also hit One. Cookies `lazuar_auth` will not satisfy One (different auth). You will see 401s that look like “identity is broken” when you actually pointed Hub UI at One.
8. **One OpenFGA playground** at `:3009/playground` targeting **:8080**: if One API is on 8080, the playground talks to the wrong process; if old Pay is on 8080, same. Real FGA checks are `curl http://localhost:8090/...`.
9. **`pnpm dev` turbo in Pay** while trying to dogfood One: turbo includes `lazuar-api` (old) and `lazuar-pay` (new). Old API will fight One for 8080; focused Pay may still come up on 8081. Partial success is worse than a clean bind error.
10. **`pnpm dev` turbo in One** plus Pay docs or Pay admin: extra 5180/3005 fights.
11. **Two copies of `lazuar-admin` in the brain:** One staff is **5173**. Pay old platform admin is **3005**. 011: merchants never use One `lazuar-admin`. Pay old admin is also not the S0 merchant path.

### What *can* run at once (the point)

- One compose identity (5432, 8085, 3005, 8090, 8091, 3009, 2112)
- One host API **8080**
- One login **5175** (+ BFF 5176)
- One app **5174** (and optionally One admin 5173, docs 5180, reference 5181)
- Focused Pay **8081**
- Optional Mailpit 1025/8025

That set has **no port overlap** as long as Pay compose, Pay `task dev`, Pay `task fe`, Pay `task proxy`, and Pay dual-run stay **off**.

---

## Recommended dogfood compose of processes (whoami proof)

Goal: Ada signs in on **5175** (via **5174** OIDC), you copy a JWT access token, `GET /api/v1/me` on **8080** returns `user_id` + `tenants[]`, and focused Pay on **8081** answers health. Pay does not implement `/me` itself (011: call One). Backend-first: Pay does not need a browser origin.

### Leave down (checklist)

From the **Pay** repo, do **not** run:

- `task dev`
- `task infra:up` (unless One Postgres was remapped; even then focused Pay does not need `lazuar_mvp`)
- `task fe` / `mprocs-dev.yaml`
- `task proxy` / `task proxy:up`
- `docker compose up` / `--profile full` / `docker-compose.ghcr.yml`
- `task tunnel:cf` / `task tunnel:api`
- `pnpm dev` (turbo)

From the **One** repo, do **not** run:

- `docker compose --profile api` (host API is enough; container name `lazuar-api` is a footgun)
- `pnpm dev` turbo unless you want every SPA at once

### Bring up (copy-paste)

**Terminal 0 — identity (once per volume):**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-one
cp .env.example .env
# if lsof -iTCP:5432 -sTCP:LISTEN  shows lazuar-db, either:
#   docker stop lazuar-db
#   or set POSTGRES_PUBLISHED_PORT=5433 in .env and export ConnectionStrings__Lazuar with Port=5433
./scripts/bootstrap-local.sh
pnpm install
pnpm api:migrate
```

**Terminal 1 — One API (8080):**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-one
# if Postgres published on 5433:
#   export ConnectionStrings__Lazuar="Host=localhost;Port=5433;Database=lazuar;Username=postgres;Password=postgres"
pnpm api:dev
```

**Terminal 2 — product login (5175):**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-one
pnpm login:dev
```

**Terminal 3 — customer SPA (5174), needed to mint a user JWT:**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-one
cp apps/lazuar-app/.env.example apps/lazuar-app/.env   # first time; seed may have written VITE_ZITADEL_CLIENT_ID
pnpm app:dev
```

**Terminal 4 — focused Pay (8081):**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
task pay:dev
```

Staff console (`pnpm admin:dev` :5173) is **not** required for Ada whoami. Docs/reference are not required.

### Fingerprint 8080 before you believe `/me`

Do this **after** Terminal 1 is up, **before** you debug tokens:

```bash
echo '--- /api/v1/ (One fingerprint) ---'
curl -sS http://localhost:8080/api/v1/
# WANT: {"name":"lazuar-one-api","version":"v1"}
# OLD PAY: not that body (no such map). If you do not see lazuar-one-api, you are not on One.

echo '--- /health/live (One-only) ---'
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8080/health/live
# WANT: 200
# OLD PAY: 404 (old host has /health and /health/ready, not /health/live)

echo '--- /health/metrics (old-Pay-only) ---'
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8080/health/metrics
# WANT: 404 if One
# OLD PAY: 200

echo '--- /api/v1/auth/me (old-Pay whoami) vs /api/v1/me (One whoami) ---'
curl -sS -o /dev/null -w 'GET /api/v1/me      -> %{http_code}\n' http://localhost:8080/api/v1/me
curl -sS -o /dev/null -w 'GET /api/v1/auth/me -> %{http_code}\n' http://localhost:8080/api/v1/auth/me
# One:  /me is 401 without Bearer (mapped); /auth/me is typically 404 (not mapped)
# Hub:  /me is typically 404; /auth/me is 401 without Hub cookie/HMAC JWT

echo '--- /health/ready shape ---'
curl -sS http://localhost:8080/health/ready
# One: {"status":"ready"|"not_ready","checks":{...}}
# Old Pay: {"status":"...","database":"up"|"down","outbox_lag_seconds":...,"reason":...}

echo '--- focused Pay ---'
curl -sS http://localhost:8081/health
curl -sS http://localhost:8081/v1/health
# WANT: {"status":"ok"} both
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8081/health/live
# WANT: 404 (Pay has no /health/live)
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8080/v1/health
# WANT: 404 on One (One’s versioned live is /api/v1/health, not /v1/health)
```

### Interactive whoami

1. Browser: `http://localhost:5174` → Sign in.
2. Browser must land on **`http://localhost:5175/login?authRequest=…`**, not `:3005/ui/v2/login/…`, not Pay `:3005` admin.
3. Customer: `ada@acme.test` / `Password1!` (see credentials section). First workspace: **Create workspace** in lazuar-app, not a Zitadel org create, not Pay ops.
4. DevTools → Application → Session Storage → origin `http://localhost:5174` → oidc-client-ts user JSON → copy **`access_token`** (JWT, three segments). Never copy `id_token` (011 / `bearerToken.ts`).
5. Curl One:

```bash
export ACCESS_TOKEN='…'   # JWT access_token only
curl -sS http://localhost:8080/api/v1/me \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json"
# unauthenticated control:
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/v1/me
# WANT: 401
```

6. Optional later (when Pay grows a server client): Pay process calls the **same** URL with the **same** header. Pay must not read `lazuar_login_sess` or `lazuar_auth`. There is no Pay `/me` today.

### Curl-only control (no SPA) that still proves the **ports**

You can prove process identity without Ada:

```bash
curl -sf http://localhost:8080/api/v1/          # One name
curl -sf http://localhost:8080/health/live
curl -sf http://localhost:8085/.well-known/openid-configuration
curl -sf http://localhost:5175/health           # service=lazuar-login
curl -sf http://localhost:8081/v1/health
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/v1/me   # 401
```

That is enough to know you did not boot old Pay on 8080. It is **not** enough to flip NP-ONE-006 (need a real Bearer). Token mint still wants 5174+5175 or a minted `lzr_sk_`.

### Stub vs real for this proof

| Check | Stub Development defaults OK? |
|-------|-------------------------------|
| `GET /health` One + Pay | Yes |
| OIDC discovery :8085 | Yes (real Zitadel) |
| Sign in Ada, JWT, `GET /me` | Yes (real JWKS; stub only replaces **Management** org create) |
| `POST /tenants` with real `zitadel_org_id` | **No** — need `Zitadel__UseStub=false` + provisioner PAT |
| OpenFGA `authz/check` tuples | **No** — need `OpenFga__Enabled=true` + store/model from bootstrap `.env.local` |
| `prove-local-stack.sh` | **No** — rejects skipped ready checks |

Whoami (NP-ONE-006) can proceed on Development stub. Slice step 3 (`POST /tenants` as Pay org_id) should switch off stub when you care that org ids are real. Do not block health/port proof on that.

---

## Env file sketch for `apps/lazuar-pay` (`ONE_API_URL`)

**Today:** there is no `apps/lazuar-pay/.env`, no `.env.example`, and no One-related keys in `appsettings.json` / `appsettings.Development.json` / `launchSettings.json`. `Program.cs` does not bind options. This sketch is **proposed**, not present in the tree.

Focused Pay should learn One the way `lazuar-login` already does (server-side URL, no `VITE_*` secrets), and the way 011 describes Pay’s identity: authority `:8085`, One API `:8080/api/v1`, product login `:5175`.

### `apps/lazuar-pay/.env.example` (sketch — do not invent a committed file in this slice)

```bash
# Focused Pay — local. Not the old apps/lazuar-api/.env.
# Copy to .env (gitignored) when Program.cs actually reads it.
# .NET also accepts double-underscore: One__ApiUrl overrides.

# --- this process ---
ASPNETCORE_ENVIRONMENT=Development
# launchSettings already binds http://localhost:8081
ASPNETCORE_URLS=http://localhost:8081

# --- One (Consumer-0) ---
# Canonical local One API. Trailing /api/v1 matches lazuar-app VITE_API_URL
# and lazuar-login LAZUAR_ONE_API_URL.
ONE_API_URL=http://localhost:8080/api/v1
# Alias if we bind IOptions One:ApiUrl:
# One__ApiUrl=http://localhost:8080/api/v1

# OIDC issuer (Zitadel). Host port, never http://zitadel-api:8080.
ONE_AUTHORITY=http://localhost:8085
# Zitadel__Authority=http://localhost:8085

# Public Pay origin (future SPA / redirect registration). Not used while backend-only.
PAY_PUBLIC_URL=http://localhost:8081

# Product login is One's, not Pay's homepage.
ONE_LOGIN_URL=http://localhost:5175

# Future SPA (not required for curl whoami):
# ONE_CLIENT_ID=          # from POST /tenants/{id}/apps or seed; public
# ONE_REDIRECT_URI=http://localhost:XXXX/callback
# Never put login-client PAT, ZITADEL_PAT, OpenFGA admin token, or Zitadel masterkey here.
# Machine calls to One:
# ONE_API_KEY=            # lzr_sk_… shown once; server-only

# Do not set:
# ZITADEL_SERVICE_USER_TOKEN
# Zitadel__ServiceUserToken
# OPENFGA admin
# Jwt__Secret from old Pay (symmetric Hub JWT is not One)
```

### `launchSettings.json` environmentVariables (sketch of what *would* be added)

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "ONE_API_URL": "http://localhost:8080/api/v1",
  "ONE_AUTHORITY": "http://localhost:8085"
}
```

`applicationUrl` stays `http://localhost:8081`.

### `appsettings.Development.json` (sketch)

```json
{
  "One": {
    "ApiUrl": "http://localhost:8080/api/v1",
    "Authority": "http://localhost:8085",
    "LoginUrl": "http://localhost:5175"
  }
}
```

Env `ONE_API_URL` should map onto `One:ApiUrl` in code when that options type exists. Until it exists, exporting `ONE_API_URL` does **nothing** — curl whoami talks to One **directly**, not through Pay.

### Names already in the family (do not collide blindly)

| Variable | Owner | Value locally |
|----------|-------|----------------|
| `VITE_API_URL` | One `lazuar-app` / `lazuar-admin` | `http://localhost:8080/api/v1` |
| `LAZUAR_ONE_API_URL` | One `lazuar-login` BFF (HRD, server-only) | `http://localhost:8080/api/v1` |
| `VITE_ZITADEL_AUTHORITY` | One SPAs | `http://localhost:8085` |
| `ZITADEL_API_URL` | One login BFF | `http://localhost:8085` |
| `Zitadel__Authority` | One API | `http://localhost:8085` |
| `App__ApiBaseUrl` | One API (its own public base) | `http://localhost:8080/api/v1` |
| `App__ApiBaseUrl` | **Old Pay** (Billplz hop A) | public `https://pay-local.lazuar.dev/api/v1` or similar — **different product** |
| `VITE_API_URL` | Old Pay ops/admin Docker build-args | `http://localhost:8080/api/v1` meaning **old Hub** |
| `NEXT_PUBLIC_API_URL` | Old Pay portal | same old Hub |

When Pay grows a SPA, prefer a **Pay-prefixed** public env (`VITE_PAY_API_URL=http://localhost:8081`, `VITE_ONE_API_URL=http://localhost:8080/api/v1`) so nobody reuses Hub’s `VITE_API_URL` and silently posts money calls to One or identity calls to old Hub.

---

## CORS: Pay 8081 called from which origin?

**Short answer for this slice: none. Backend-only first. `curl` is enough. Do not add CORS to focused Pay until a browser origin exists.**

### What exists today

**Focused Pay (`Lazuar.Pay`):** no `AddCors`, no `UseCors`. A browser on another origin that `fetch`es `http://localhost:8081/health` will fail the CORS check (no `Access-Control-Allow-Origin`). `curl` and server-to-server do not use CORS. That is the intended first dogfood.

**One API:** default policy, **credentials allowed**, exact origin list (never `*`). Development overlay (`appsettings.Development.json`):

```
http://localhost:5173
http://localhost:5174
http://localhost:5180
http://localhost:5181
http://localhost:3000
http://localhost:3001
http://localhost:5177
http://127.0.0.1:5173
http://127.0.0.1:5174
http://127.0.0.1:5177
http://127.0.0.1:5180
http://127.0.0.1:5181
```

`CorsOriginList.DevelopmentDefaults` (used only when the CSV is **empty** in Development) is slightly smaller: 5173, 5174, 5177, 5180 × localhost and 127.0.0.1 — **no 5181, no 3000/3001**. In real local runs the Development JSON CSV is non-empty, so the overlay list wins.

**Not on One’s CORS list:**

- `http://localhost:5175` — login BFF is same-origin to the login UI; login talks to Zitadel and (server-side) to One. The **browser** on 5175 does not call One API as a SPA. HRD is server-side (`LAZUAR_ONE_API_URL`).
- `http://localhost:8081` — focused Pay is not a SPA origin, and even as a server it does not need CORS to call One.
- `http://localhost:3002`–`3005`, `9080` — old Pay frontends/gateway.

Compose profile `api` sets `App__CorsOrigins` default `http://localhost:5173,http://localhost:5174,http://localhost:5180` (no 127.0.0.1, no 5177, no 5181).

One CORS is `AllowAnyHeader` + `AllowAnyMethod` + `AllowCredentials`. Wildcard origin is a boot failure (`AppOptionsValidator`). Staging/Production empty CSV is a boot failure.

**localhost ≠ 127.0.0.1.** One issue 077 documented this; both are listed for the Vite ports in Development JSON. If you open One app as `http://127.0.0.1:5174` you need the 127 twin. If a future Pay SPA is only listed as localhost, the 127 twin will CORS-fail `/me`.

**Old Pay API CORS** (`appsettings.Development.json` `App:CorsOrigins`):

```
http://localhost:3000
http://localhost:3001
http://localhost:3002
http://localhost:3003
http://localhost:3004
http://localhost:3005
http://localhost:3020
http://localhost:3021
http://localhost:8080
http://localhost:8090
http://localhost:9080
```

That list is the **old Hub UI + tunnels**, not One’s 517x. Old Pay also `AllowCredentials`. Empty CORS in Development falls through to `AllowAnyOrigin` **without** credentials (`AuthAndCorsExtensions`). Production/Staging empty CORS fails boot — same idea as One, different origin set.

### Who would call Pay :8081 from a browser?

| Caller | Origin | Need CORS on 8081? |
|--------|--------|---------------------|
| `curl` / HTTPie / Postman (no Origin header) | n/a | **No** |
| Focused Pay itself (same process) | n/a | No |
| One API (server) | n/a | No |
| Future Pay merchant SPA (not in tree) | TBD — must **not** reuse 5173/5174/5175 or 3003–3005 | **Yes**, later, exact origin |
| Future Pay hosted checkout (buyer) | TBD, likely a Pay-owned origin | Yes, later |
| Old `lazuar-ops` :3003 | `http://localhost:3003` | Do not. That UI talks to **old** `:8080/api/v1`. |
| One `lazuar-app` :5174 | `http://localhost:5174` | Not for Pay health. App talks to **One** `:8080/api/v1`. If we later embed Pay merchant chrome in One’s app, that is a product decision; default 011 is Pay as a **separate origin**. |

011: “Pay is a **separate origin**. Users are One humans.” The SPA origin is registered via `POST /tenants/{id}/apps`, same kind of object as seeded `lazuar-app`. That origin is **not** 8081 unless we serve the SPA from the Pay host (we do not today). 8081 is the **API** listen, analogous to One’s 8080.

So: **Pay 8081 is not “called from” One 5174.** One 5174 calls One 8080 (CORS already allows 5174). Pay **server** will call One 8080 with Bearer (no CORS). A future Pay SPA origin will call **both** One 8080 (add that origin to One `App:CorsOrigins` + login `REDIRECT_ALLOWLIST`) **and** Pay 8081 (add CORS on Pay then).

### Login `REDIRECT_ALLOWLIST` (not CORS, still origin math)

`apps/lazuar-login/.env.example`:

```
REDIRECT_ALLOWLIST=http://localhost:5173,http://localhost:5174,http://localhost:5177,http://localhost:8085,http://localhost:5175
```

When Pay gets a browser origin, it must be added **here** and on the One OIDC app redirect URIs. Adding it only in Zitadel Console is the 011 “do not.” 8081 as an API origin does not belong on this list unless we literally redirect OIDC to `:8081/callback`.

### Backend-only first: curl is enough

For S0 whoami:

```bash
# no Origin header, no preflight
curl -sS http://localhost:8081/health
curl -sS http://localhost:8081/v1/health
curl -sS http://localhost:8080/api/v1/me -H "Authorization: Bearer $ACCESS_TOKEN"
```

Do not open `http://localhost:8081` in the SPA as a fetch target until CORS exists. Do not copy old Pay’s `AllowAnyOrigin` Development fallback onto focused Pay.

---

## Cookies vs Bearer

Three different session designs already exist on this laptop. Mixing them is how you get a 401 that “should have worked.”

### Map

| Surface | Origin (local) | Credential the **API** accepts | Where the browser keeps it | Cross-port? |
|---------|----------------|--------------------------------|----------------------------|-------------|
| One `lazuar-login` BFF | `http://localhost:5175` (proxied BFF 5176) | **Cookie** to the BFF only: `lazuar_login_sess` (HttpOnly, AES-GCM blob of Zitadel session id+token), `lazuar_login_csrf` (not HttpOnly, double-submit). SameSite **Lax**, Secure **false** in dev, Path `/`, Domain unset (host-only `localhost`). Max-age 12h. | Cookie jar for `localhost` | Cookies are **not port-scoped** (RFC 6265). A Lax cookie set on 5175 **may** be sent to other `http://localhost:*` on same-site navigations. One API **ignores** it. Old Pay looks for **different names**. |
| One SPAs (`lazuar-app` 5174, `lazuar-admin` 5173, example 5177) | those origins | **Authorization: Bearer** JWT **access_token** to One API. Never `id_token`. Opaque access → omit header → honest 401. Optional `X-Lazuar-Tenant-Id` **hint**. | `oidc-client-ts` `WebStorageStateStore` in **sessionStorage** of that origin. XSS can read it; comments in `oidcConfig.ts` say so. | sessionStorage is **origin-scoped** (port included). 5174 cannot read 5173’s user. 8080 never sees the storage; it only sees the header the SPA copies in. `apiFetch` does not set `credentials: 'include'`. |
| One API | `http://localhost:8080` | Bearer JWT (Zitadel JWKS, `jti` required, ID tokens fail) **or** Bearer `lzr_sk_…` **or** Bearer `lzr_scim_…`. **No cookie auth.** | n/a | CORS credentials flag is on so a future cookie’d SPA *could* send cookies; current first-party fetch does not rely on that. |
| Old Pay API | `http://localhost:8080` when it owns the port | Symmetric JWT (`Jwt:Issuer=lazuar-api`, `Audience=lazuar-clients`, HMAC `Jwt:Secret`) **plus** `OnMessageReceived` reads cookies **`lazuar_auth`** (merchant) or **`lazuar_admin_auth`** (path `/api/v1/platform`). HttpOnly, SameSite Lax, Secure false in dev, Domain null in dev / `.lazuar.com` in prod. | Cookie jar | This is **password-form Hub identity**, not Zitadel. Ada’s One JWT will not validate. Hub cookies will not validate on One. |
| Focused Pay | `http://localhost:8081` | **None yet.** 011: Bearer access_token from One, or `lzr_sk_` for workers. **Do not** port `AuthCookie` / `lazuar_auth`. | n/a | n/a |
| Zitadel Console / issuer | `http://localhost:8085` | Zitadel’s own cookies / OIDC | Cookie jar for 8085 | Authorize redirect is 302, not a Bearer the SPA invents. |

### What Pay must do (011, restated for topology)

- Send **access_token** as `Authorization: Bearer`. Never `id_token`.
- Do not send `lazuar_login_sess` to One or to Pay as if it were an API credential. That cookie is for Session API v2 on the login BFF.
- Do not implement a Pay password form that sets `lazuar_auth`.
- `GET /me` is on **One :8080**, not on Pay :8081.
- Login cookies live on **5175**. Pay origin will be a **different origin**. Cross-origin cookie SSO will not magically exist; OIDC code + PKCE will.

### Same-site note for localhost

Modern Chrome treats `http://localhost:5174` and `http://localhost:8080` as **schemeful same-site** (same scheme + host, port ignored for the site). That is why SameSite=Lax cookies from 5175 *can* ride along to 8080 if a navigation or credentialed request happens. It is **not** a reason to enable cookie auth on One or Pay. It **is** a reason to keep cookie **names** distinct so a confused middleware does not treat a login blob as a Hub JWT.

Old Pay cookie names: `lazuar_auth`, `lazuar_admin_auth`.  
One login cookie names: `lazuar_login_sess`, `lazuar_login_csrf`.  
No overlap. Keep it that way.

### SPA fetch vs cookie (One app, actual code)

`apps/lazuar-app/src/api/client.ts`: builds `Authorization: Bearer ${token}` from `pickApiBearerToken` (JWT access only). `fetch(url, { ...init, headers })` — default credentials mode `same-origin`. Cross-origin call to `:8080` therefore **does not** attach cookies. Auth is the header. That is the pattern Pay’s future SPA and Pay’s backend client should copy.

### Issuer pitfall (Bearer still fails if ports are “right” but iss is wrong)

Host One API: `Zitadel:Authority=http://localhost:8085`.  
Compose API profile: `Zitadel__Authority=http://zitadel-api:8080`.  
Tokens are only valid against the issuer that minted them. Browser login against host 8085 produces `iss=http://localhost:8085`. Those tokens work on **host** `pnpm api:dev`. They fail if you accidentally validate against the container issuer, and the reverse is also true.

---

## Health probes: One `/health` vs Pay `/health`

Same path, different product, overlapping JSON `{ "status": "ok" }` on liveness. **Never** use `/health` alone to decide which process owns 8080.

### One API (`Lazuar.One.Api`) — `Infrastructure/Health/HealthEndpoints.cs` + `Program.cs`

JSON naming: **snake_case** globally (`PropertyNamingPolicy = SnakeCaseLower`). `HealthResponse.Status` → `"status"`.

| Method | Path | Auth | Body / behavior |
|--------|------|------|-----------------|
| GET | `/health` | anonymous | Liveness. `{ "status": "ok" }`. No dependency probes. Name in OpenAPI: `HealthLiveLegacy`. |
| GET | `/health/live` | anonymous | Same liveness. `HealthLive`. |
| GET | `/health/ready` | anonymous | Readiness. 200 `{ "status": "ready", "checks": { "<name>": { "ok", "skipped", "detail" } } }` or **503** `{ "status": "not_ready", "checks": … }`. Checks: **database** always; **zitadel** when `UseStub=false`; **openfga** when `Enabled=true`; **email** (DevLog → ok + skipped). |
| GET | `/api/v1/health` | anonymous | Versioned liveness, same dumb ok. TypeSpec / clients. |
| GET | `/api/v1/` | anonymous | **Fingerprint:** `{ "name": "lazuar-one-api", "version": "v1" }` |

Compose profile `api` healthcheck: `curl -sf http://127.0.0.1:8080/health/ready`.  
`deploy/dev/smoke.sh`: `/health`, `/health/live`, `/health/ready`, then `/api/v1/me` must be **401**.  
`scripts/prove-local-stack.sh`: `/health` then `/health/ready`, and **fails** if the ready JSON contains `"skipped": true`.

Correlation: `X-Request-Id` echoed on health (tests lock this).

### Focused Pay (`Lazuar.Pay`) — `Program.cs`

Default System.Text.Json for anonymous types: property name `status` as-is.

| Method | Path | Auth | Body / behavior |
|--------|------|------|-----------------|
| GET | `/health` | anonymous | `{ "status": "ok" }`. Process up only. **No** `/health/live`, **no** `/health/ready`, **no** DB. |
| GET | `/v1/health` | anonymous | Same. TypeSpec `Health.check`. Tests: `HealthTests` asserts success + body contains `ok`. |

There is **no** `/api/v1/health` on focused Pay. One’s versioned path is `/api/v1/health`. Pay’s is `/v1/health`. Keep that difference; it is a cheap fingerprint.

### Old Pay API (`Lazuar.Api`) — `Composition/HealthEndpointExtensions.cs`

Also snake_case JSON.

| Method | Path | Auth | Body / behavior |
|--------|------|------|-----------------|
| GET | `/health` | anonymous | `{ "status": "ok" }` — **same as One liveness**. Tenant middleware exempts `/health`. |
| GET | `/health/ready` | anonymous | `{ "status", "database": "up"|"down", "outbox_lag_seconds", "reason" }`. 503 if DB down or optional outbox-lag / dead-letter gates. **No `checks` object.** |
| GET | `/health/metrics` | anonymous | Snapshot: outbox, dead letters, LHDN stuck, counters, schemas. **One does not have this path.** |
| GET | `/health/live` | — | **Not mapped** (404). |
| GET | `/api/v1/health` | — | Not the One helper. Do not assume it exists. |
| GET | `/api/v1/` name fingerprint | — | **Not** `lazuar-one-api`. |
| GET | `/api/v1/auth/me` | Hub cookie or HMAC JWT | Old Pay whoami (`AuthEndpoints`). One does **not** map this; One whoami is `GET /api/v1/me`. |

Caddy `deploy/dev/Caddyfile`: `handle /health { reverse_proxy host.docker.internal:8080 }`. Gateway health is **whatever owns host 8080**.

### Login BFF (One) — `apps/lazuar-login/src/server/routes/health.ts`

`GET http://localhost:5175/health` (Vite proxies to 5176):

```json
{ "status": "ok", "service": "lazuar-login", "zitadelConfigured": true }
```

`service` is the fingerprint. Focused Pay health has no `service` field.

Stock Login V2: `GET http://localhost:3005/ui/v2/login/healthy` (compose healthcheck). Different path, different product surface.

### Zitadel / OpenFGA / Postgres

| Probe | URL |
|-------|-----|
| Zitadel ready (in container) | `docker compose exec zitadel-api /app/zitadel ready` |
| OIDC discovery | `GET http://localhost:8085/.well-known/openid-configuration` |
| OpenFGA | `GET http://localhost:8090/healthz` |
| Postgres | `docker compose exec postgres pg_isready -U postgres` (One) / `pg_isready -U postgres -d lazuar_mvp` (Pay) |

### Probe matrix for the whoami laptop

| Question | Probe |
|----------|-------|
| Is focused Pay up? | `GET :8081/health` **and** `GET :8081/v1/health` |
| Is One API up (liveness)? | `GET :8080/health/live` (404 ⇒ not One) |
| Is it really One? | `GET :8080/api/v1/` → `name=lazuar-one-api`; `/health/metrics` 404; `/v1/health` 404 |
| Is One ready for JWT dogfood? | `GET :8080/health/ready` + discovery `:8085` |
| Is product login up? | `GET :5175/health` contains `lazuar-login` |
| Is stock login still there? | `GET :3005/ui/v2/login/healthy` |
| Did I boot old Pay by mistake? | `/health/live` 404, `/health/metrics` 200, ready JSON has `database`/`outbox_lag_seconds`, `/api/v1/` is not `lazuar-one-api`, `/api/v1/auth/me` is mapped (401 unauthenticated) while `/api/v1/me` is not One-shaped |

---

## Demo credentials (from One README / bootstrap — present)

All **local only**. One README, `bootstrap-local.sh` footer, and `scripts/seed-dev-demo.sh` agree.

### One (identity) — use these for whoami

| Role | App | URL | Email | Password | Seeded by |
|------|-----|-----|-------|----------|-----------|
| **Customer** | `lazuar-app` | http://localhost:5174 | `ada@acme.test` | `Password1!` | `seed-dev-demo.sh` (`DEMO_CUSTOMER_*` env; given name Ada, family Lovelace) |
| **Staff / platform** | `lazuar-admin` | http://localhost:5173 | `zitadel-admin@zitadel.localhost` | `Password1!` | Zitadel first-instance default (also Console at http://localhost:8085/ui/console) |

`deploy/dev/README.md` hedges first-instance admin: “User often like `zitadel-admin@zitadel.localhost` / `Password1!`. Do not hard-depend without confirming against your running instance after `start-from-init`.” After a `down -v` this is the compose default (`ZITADEL_FIRSTINSTANCE_*` in docker-compose). If login fails, check Zitadel logs, not Pay’s `Password123!`.

Re-seed: `task seed` or `./scripts/seed-dev-demo.sh`. `--users-only` skips SPA client rewrite.

**Merchants use Ada on 5174 → 5175. They do not use 5173.** 011: “Merchants never use `lazuar-admin` (`:5173`).”

### Old Pay (Hub) — do **not** use for One whoami

From Pay root README (seeded on first **Development** boot of **old** `task dev` from `apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json`):

| Role | App | URL | Email | Password |
|------|-----|-----|-------|----------|
| Superadmin | old `lazuar-admin` | http://localhost:3005/ | `admin@lazuar.com` | `Password123!` |
| Tenant admin | `lazuar-ops` | http://localhost:3003/ | `founder@acme.test` | `Password123!` |

Workspace slug **`acme`**. Portal `:3004` has no password login (buyer magic link).

### Confusable pairs (read out loud)

| Lookalike | One | Old Pay |
|-----------|-----|---------|
| Acme human | `ada@acme.test` | `founder@acme.test` |
| Password | `Password1!` | `Password123!` |
| Admin URL | **5173** (One staff) | **3005** (Hub superadmin) **and** One stock login **3005** |
| “acme” | Ada’s email domain; workspace created in-app | Seeded tenant **slug** `acme` in `lazuar_mvp` |

Signing into Hub with Ada’s password will fail. Signing into One with `Password123!` will fail. Landing on `:3005` might be **either** stock Zitadel Login V2 **or** old Pay admin depending on which process bound the port.

Zitadel masterkey in One `.env.example`: `MasterkeyNeedsToHave32Characters`. Postgres user/password `postgres`/`postgres` on **both** composes. Insecure by design; local only.

---

## Risks: accidentally hitting old Pay on 8080 thinking it is One

This is the highest-probability local footgun in the whole Consumer-0 plan.

### Why it is easy

1. **Both listen on 8080** by documented default (`launchSettings` both say `http://localhost:8080`).
2. **Both answer `GET /health` with `{ "status": "ok" }`.** A green health check is not an identity check.
3. **Both speak snake_case JSON** and have `/api/v1/…` trees. One whoami is `GET /api/v1/me`. Old Pay whoami is `GET /api/v1/auth/me` (cookie or HMAC JWT). A 401 on `/me` looks like “token problem” when the process is Hub (that path is not One’s `/me`) or when 8080 is Hub and the client still posts to `/api/v1/me`.
4. **Pay `docker compose up` starts old API on 8080 without a profile.** One `docker compose up` does **not** start One API. Muscle memory from Pay (`compose up` = API) applied to One (or vice versa) inverts expectations.
5. **Pay `task dev` is the old API.** The new process is the oddly spelled `task pay:dev`. Tab completion hits `dev` first.
6. **Same folder name `apps/lazuar-api`.** `cd apps/lazuar-api && pnpm dev` in the wrong repo starts the wrong host.
7. **Same container name `lazuar-api`.** Docker error messages and `docker ps` do not say One vs Hub.
8. **Pay Caddy `:9080/health` proxies 8080.** Browser “the API is healthy” via the Hub gateway is whatever bound 8080.
9. **OpenFGA playground** wants 8080. A developer “opens 8080 to check FGA” may actually be hitting One or old Pay.
10. **Aura dual-run docs in Pay README** say “Aura owns 8080, Pay owns 8090.” One docs say “One API owns 8080, OpenFGA owns 8090.” Third product, third mapping. All true in their context; lethal if mixed.
11. **Old Pay CORS includes `http://localhost:8080` and `8090`.** That looks like “the API origin.” One CORS includes Vite 517x. Seeing CORS errors on 5174 against 8080 might mean old Pay is bound (5174 not allowed) rather than One misconfig.
12. **Demo emails both say acme.** Token from the wrong login UI still looks like a JWT.

### What goes wrong functionally

| You think | You actually hit | Symptom |
|-----------|------------------|---------|
| One `GET /api/v1/me` with Ada JWT | Old Pay on 8080 | One’s `/api/v1/me` is **not** Hub’s whoami. Hub maps **`GET /api/v1/auth/me`** (`AuthEndpoints`) and expects HMAC issuer `lazuar-api` plus cookies `lazuar_auth` / `lazuar_admin_auth`. Ada’s Zitadel JWT will not validate. You may see 401 or a 404 depending on whether Hub mapped `/me` at all. |
| One `POST /api/v1/tenants` | Old Pay tenant create | Completely different module, writes `lazuar_mvp`, ignores Zitadel. You “created a workspace” that One `/me` will never list. |
| One `POST /tenants/{id}/api-keys` (`lzr_sk_`) | Old Pay API keys | Different hash/pepper/prefix rules. Keys will not work on the other host. |
| `task proxy` Hub UI | One on 8080 | Ops/admin 401; cookies `lazuar_auth` ignored by One. |
| Focused Pay `ONE_API_URL=http://localhost:8080/api/v1` | Old Pay | Pay “whoami” talks to Hub One-module leftovers. 011’s “do not copy Modules/One” is violated at **runtime** even if you never referenced the project. |
| `curl :8080/health` in a script | Either | Script is green; the rest of the stack is a lie. |

Old Pay **will** accept a credential if you send **its** cookie or **its** HMAC JWT. That is not a security boundary between products on localhost; it is a confusion boundary.

### Mitigations (operational, this slice — not code)

1. **Fingerprint** with `GET /api/v1/` (`lazuar-one-api`), `GET /health/live` (One), `GET /health/metrics` (old Pay), `GET /v1/health` (focused Pay on **8081**).
2. **Bind rule:** while dogfooding One+focused Pay, 8080 is One. Old Pay is off. Focused Pay is 8081. Say it in the shell prompt if you have to.
3. **Do not `docker compose up` in Pay** for this proof. If you need Hub money later, do it on another day or remap 8080/5432/3005/8090 explicitly and write the remap down.
4. **Alias the commands:** One API = `pnpm api:dev` in **lazuar-one**. Focused Pay = `task pay:dev` in **lazuar-pay**. Never `task dev` during One week.
5. **Read `docker ps` container names:** `lazuar-postgres` vs `lazuar-db`; `lazuar-zitadel-api` vs `lazuar-api` (if the latter exists, old Pay compose API is up — stop it).
6. **Ada vs founder:** if the login form is asking for `founder@acme.test` or the page is ops on 3003, you are in Hub.
7. **If `/me` 401s:** first prove `name=lazuar-one-api` and `iss` on the JWT is `http://localhost:8085`, then debug tokens. Do not rotate SPA client ids while 8080 is Hub.
8. **Future code (out of this slice, but lock it):** focused Pay should refuse to start if `ONE_API_URL` host:port fingerprints as old Pay (`/health/metrics` 200 or `/api/v1/` not `lazuar-one-api`). Not implemented today.

---

## Cookies, CORS, and 8081 — combined picture for whoami

```text
Browser                Host processes                         Docker (lazuar-one-network)
-------                --------------                         ---------------------------
:5174 lazuar-app  --Bearer access_token-->  :8080 One API
     |  sessionStorage JWT                     |  JWKS iss :8085
     |  CORS allow 5174                        \--SQL--> :5432 postgres (DB lazuar)
     |
     |  OIDC redirect
     v
:8085 Zitadel  --302 authRequest-->  :5175 lazuar-login
                                      cookie lazuar_login_sess (BFF :5176)
                                      NOT sent as One API auth

curl / scripts
  :8080 /api/v1/me   Authorization: Bearer <same access_token>
  :8081 /health      no auth, no CORS needed
  :8081 does not implement /me
```

Old Pay (`:8080` Hub, cookies `lazuar_auth`, Postgres `lazuar_mvp` on `lazuar-network`) is **absent** from this picture on purpose.

---

## Residual / out of this paper

- Wiring `ONE_API_URL` into `Lazuar.Pay` options and an HttpClient. Sketch only.
- Choosing a Pay SPA port (must avoid 5173–5177, 5180–5181, 3002–3005, 8080, 8081-as-UI, 9080).
- Adding that origin to One CORS + login allowlist + `POST /tenants/{id}/apps`.
- Sharing a Docker network between focused Pay and One.
- Remapping 5432 so old Hub db and One db run in parallel (possible, not recommended for whoami).
- Pay compose swapping `apps/lazuar-api` for `apps/lazuar-pay` (README already says “swap later when S1 dogfood is real”).
- One staging proof, SMTP, non-stub provisioner PAT runbook beyond pointers in `deploy/dev/README.md`.

---

## One-page command cheat sheet

**One identity + API + login + app:**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-one
cp .env.example .env
./scripts/bootstrap-local.sh          # or: task bootstrap
pnpm install
pnpm api:migrate
pnpm api:dev                          # :8080     terminal 1
pnpm login:dev                        # :5175     terminal 2
pnpm app:dev                          # :5174     terminal 3
# ada@acme.test / Password1!
curl -sf http://localhost:8080/api/v1/   # name=lazuar-one-api
```

**Focused Pay:**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
task pay:dev                          # :8081
curl -sf http://localhost:8081/health
curl -sf http://localhost:8081/v1/health
```

**Do not:**

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
task dev                              # old API :8080 — steals One
docker compose up                     # old API :8080 + db :5432
task fe                               # :3005 vs zitadel-login
task proxy                            # :9080 /health → :8080
task tunnel:cf                        # :8090 vs OpenFGA
```
