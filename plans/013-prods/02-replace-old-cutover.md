# 02 — Replacing the old backend and frontends (cutover, dual-run, kill criteria)

**Date:** 21 August 2026  
**Type:** Analysis only. **Do not implement product code from this file.**  
**Slice:** Operational **REPLACE** of the Hub stack by the focused Pay stack. Dual-run that works. Dual-run that fails. Kill criteria per old artifact. Rollback if the new stack fails.  
**Parent program:** [`plans/013-prods`](./) — production-ready new Pay, then replace the old tree.  
**Port bible (do not relitigate numbers):** [`plans/012-one-to-pay/05-local-topology.md`](../012-one-to-pay/05-local-topology.md).  
**AuthN / 8080 collision:** [`plans/012-one-to-pay/02-one-authn-tokens.md`](../012-one-to-pay/02-one-authn-tokens.md).  
**Old-UI refuse (P60):** [`plans/012-one-to-pay/checklists/p60-old-frontends.md`](../012-one-to-pay/checklists/p60-old-frontends.md).  
**Bezos door:** [`plans/011-new-lazuar-pay/08-bezos-door.md`](../011-new-lazuar-pay/08-bezos-door.md).  
**Platforms / process shape:** [`plans/011-new-lazuar-pay/06-platforms.md`](../011-new-lazuar-pay/06-platforms.md), [`07-separate-vs-one-binary.md`](../011-new-lazuar-pay/07-separate-vs-one-binary.md), [`13-monolith-vs-services.md`](../011-new-lazuar-pay/13-monolith-vs-services.md), [`14-google-aws-microsoft.md`](../011-new-lazuar-pay/14-google-aws-microsoft.md).  
**Old tree as reference, not year-two core:** [`plans/011-new-lazuar-pay/09-old-pay.md`](../011-new-lazuar-pay/09-old-pay.md).

---

## 0. What this paper is for (and what replace is not)

New Pay is a **separate origin** and a **separate process**. Users are One humans. Merchants are One tenants. Buyers are not. One tenant id **is** Pay `org_id`. The Hub modular monolith (`apps/lazuar-api` on **8080**) is the thing we left: nine modules, cookie JWT, homemade `Modules/One`, `/public/commerce`, LHDN factory, MediatR cathedral. 011/00: stop feature work on it; keep it as **reference**. 011/09: too expensive to extend, too specific to ignore.

The operational question is smaller than “when is Pay production-ready?” and larger than “stop the Docker container.” It is:

1. **Which surfaces still claim to be Pay** (API, UIs, compose, Caddy, GHCR, ngrok/Cloudflare, docs, sample cashier) while they are actually **Hub**?
2. **Which ports and DNS names collide with One** so that “run both products” is not a slogan but a bind matrix?
3. **Which dual-run shapes are proven** (One 8080 + focused Pay 8081) **versus guaranteed mis-route** (Hub `task dev` + One, Caddy 9080 while One owns 8080, turbo `pnpm dev`)?
4. **What is a cutover phase** (local dogfood → staging new-stack-only → production DNS → Hub dark) versus a **strangler** (point old UIs at new API)?
5. **When may each old artifact be deleted** — not “when we feel done,” but kill criteria a later program can check?
6. **What judgment to steal** (`SstTaxMath`, QuestPDF notes, wrap-rails, receipt ≠ tax invoice, tests as oracles) versus **what folders to delete**?
7. **Where integrators go after replace** — Bezos `/v1` on new Pay, not Hub `/public/commerce` and not Hub `POST /api/v1/integrations/payments/checkouts` as the long-term door.
8. **How to roll back** if the new stack fails without re-binding 8080 to Hub while One is live.

**Backend-and-DNS replace, not UI retarget.** P60 is still the refuse for this family: `lazuar-ops` `VITE_API_URL` stays Hub `http://localhost:8080/api/v1` until ops is **gone**. The replacement merchant origin is `lazuar-pay-merchant` **`:5178`**. The replacement buyer origin is `lazuar-pay-checkout` **`:5179`**. `@repo/api-types-ts` stays Hub’s spec until those UIs talk `packages/pay-spec` for real.

```text
REPLACE (this paper)
  One :8080  +  focused Pay :8081  +  merchant :5178  +  checkout :5179
  Hub processes STOP. Hub DNS eventually dark.

NOT REPLACE (anti-goal)
  lazuar-ops :3003  --VITE_API_URL-->  :8081
  lazuar-portal :3004 --NEXT_PUBLIC_API_URL--> :8081
  Hub Caddy 9080 /health --> whoever bound 8080
  both APIs on 8080
  merchants on :5173 or :3005
```

Sibling One **already owns** 8080 / 5173 / 5174 / 5175 / 8085 when it is the identity plane. Pay does not take those ports back. Pay does not absorb Zitadel. Pay does not become a second Login V2.

---

## 1. Method / SHAs

This paper is **operational replace**. It is not a production-ready bar (that is [01](./01-production-ready-bar.md)). It is not host seams, money rails, or data migration (those are later 013 papers). It answers: **when the new stack can take the job, how both stacks occupy one laptop / one DNS estate, and when each old artifact may die.**

**Replace** here is a hard word. It means:

1. The **new** processes take the merchant/buyer/money job.
2. The **old** processes are **turned off**.
3. It does **not** mean a strangler that points `lazuar-ops` / `lazuar-portal` at `:8081`.
4. It does **not** mean growing `apps/lazuar-api` until it looks like focused Pay.
5. It does **not** mean Hub `/public/commerce` remaining the public door.

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `6f866ff0` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| **lazuar-one** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

**Honesty lock (inherited, not re-proven here):**

- One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. There is no public hosted One SKU. Source: `plans/011-new-lazuar-pay/02-one-integration.md`.
- Focused Pay on this SHA is **connected** (C99): `GET /v1/whoami`, `GET /v1/orgs/{orgId}/ready`, in-memory `POST/GET /v1/checkouts` fixture. It is **not** S1 money. Checkout `status` is `"open"`. `CheckoutStore` comment: “In-memory fixture store. Not a ledger. Replace when money is real.”
- Merchant Vite `:5178` and checkout Vite `:5179` exist and probe `/health` only. OIDC is **not** wired (P10). SPA registration is **todo** (`NP-ONE-001`).
- Root `README.md` still sells the **Hub cathedral** (modular monolith, `task infra:up` + `task dev` + `task fe`, ports 8080 / 3003–3005 / 9080). That README is an old-surface document, not the Consumer-0 topology.
- `.github/workflows/ci.yml` tests **Hub** (`apps/lazuar-api` architecture / integration / module / billing / ops) plus Hub TypeSpec honesty. It does **not** run `task pay:test`. `.github/workflows/ghcr.yml` builds **Hub** images (`lazuar-hub-api|ops|portal|superadmin|developers`) and SSH-deploys **hub.lazuar.com**. Focused Pay has **no** Dockerfile, **no** bake target, **no** GHCR name.

**What “Pay” means in a sentence.** If a sentence does not say **One** vs **old Hub / old Pay** vs **focused Pay**, assume it is wrong. Both checkouts use folder `apps/lazuar-api`. Both compose files want `container_name: lazuar-api`. Humans say “the API on 8080” and mean opposite processes. This paper repeats the names on purpose.

**Locked (do not bargain in this paper):**

| Lock | Why |
|------|-----|
| One API **must** own host **8080** when both products run | Proven whoami shape. Old Hub and One cannot both bind 8080. |
| Focused Pay **never** binds 8080 | `launchSettings` `http://localhost:8081`. `task pay:dev` desc: “old API stays on 8080”. Isolation is a listen, not a comment. |
| Do **not** set ops/portal `VITE_API_URL` (or portal `NEXT_PUBLIC_API_URL`) to **8081** | P60. Ops `credentials: "include"` + `POST /one/auth/login` is Hub homemade IdP. Pay must not implement it. Hundreds of `/admin/commerce`, `/lhdn`, `/ops/chat` routes are not Consumer-0. |
| Do **not** ship merchants to One `lazuar-admin` **`:5173`** or Hub admin **`:3005`** | `NP-ONE-005`. `:3005` is also One stock Login V2. Ambiguous and forbidden. |
| Replace ≠ strangler | New stack takes the job; old stack is turned off. Old UIs stay on Hub 8080 until they are **deleted**, not retargeted. |
| Bezos is the **door** | Integrators call **new Pay `/v1`**. Not Hub `/public/commerce`. Not a back door into Pay tables. Linux is the **room** (one Pay binary). |
| Local port bible | [012/05](../012-one-to-pay/05-local-topology.md). This paper adds **cutover / kill / rollback**. It does not invent new local ports for One. |

**Method (what was opened):**

- Pay: `Taskfile.yml` (`pay:*` vs `api:*` vs `infra:*` vs `proxy` vs `tunnel:*` vs `docker:*`), `docker-compose.yml`, `docker-compose.ghcr.yml`, `docker-compose.dev-proxy.yml`, `docker-bake.hcl`, `deploy/dev/Caddyfile`, `deploy/dev/README.md`, `deploy/prod/{Caddyfile,docker-compose.yml,env.example,README.md}`, `mprocs-dev.yaml`, `scripts/remote-deploy.sh`, `.github/workflows/{ci,ghcr}.yml`, root `README.md`, `package.json`, `pnpm-workspace.yaml`, `turbo.json`.
- Apps: `apps/lazuar-api` launchSettings / Development CORS / JWT seed, `apps/lazuar-ops` package.json + vite + `src/lib/api-client.ts` + Dockerfile, `apps/lazuar-portal` package.json + `NEXT_PUBLIC_API_URL` / `API_URL`, `apps/lazuar-admin` package.json + vite, `apps/lazuar-developers` package.json, `apps/lazuar-docs` package.json (5180), `apps/lazuar-pay` README / Program.cs / `.env.example` / `OneOptions` / whoami / checkout fixture / CORS, `apps/lazuar-pay-merchant` and `lazuar-pay-checkout` README / vite / `.env.example` / `App.tsx`.
- Examples: `examples/README.md`, `examples/hub-cashier-next/{README.md,.env.example,lib/env.ts}`.
- Plans: 012/05 (full), 012/02 (ports + cookies), P60, C99, 012/04 (`/v1` vs `/api/v1`), 012/10, 011/00–09, 011/06–08, 011/13–14, 003-dev-caddy, 006-sample cashier, 009 old-pay keep-as-notes.
- One: `docker-compose.yml` (`name: lazuar-one`, `container_name: lazuar-api` on profile `api`, `lazuar-postgres` on 5432, `lazuar-one-network`), `deploy/dev/README.md` (5432 often taken by lazuar-pay → remap **5433**).

**Language note (do not relitigate):** 011/05 argued Go for a greenfield Pay. The focused host that exists is **C#** on 8081. Cutover is of **processes and DNS**, not of language.

---

## 2. Inventory of old surfaces that claim to be “Pay”

Every row below is still in this repo at `6f866ff0`. Each one will look like “the product” to a new engineer, a GHCR pull, a Caddy `handle`, or a sample README. **Replace** has to name them so kill criteria can later delete them without missing a tunnel.

### 2.1 Processes (the four old listen surfaces)

| Path | Package / image | Host listen (local DX) | Prod path on `hub.lazuar.com` | What it actually is | New counterpart |
|------|-----------------|------------------------|-------------------------------|---------------------|-----------------|
| `apps/lazuar-api` | pnpm `lazuar-api`; GHCR `ghcr.io/proxeon/lazuar-hub-api` | **8080** (`launchSettings` `applicationUrl: http://localhost:8080`) | `/api/*`, `/health` | Modular monolith. Cookie JWT `lazuar_auth` / `lazuar_admin_auth`. Homemade `POST /one/auth/login`. Nine EF schemas in DB `lazuar_mvp`. **Collides with One API.** | `apps/lazuar-pay` **8081** |
| `apps/lazuar-ops` | `lazuar-ops`; GHCR `lazuar-hub-ops` | **3003** Vite `strictPort` | `/` (catch-all) | Merchant console. `credentials: "include"`. `VITE_API_URL` default `http://localhost:8080/api/v1`. `@repo/api-types-ts`. `GET /one/auth/me`. | `apps/lazuar-pay-merchant` **5178** |
| `apps/lazuar-portal` | `lazuar-portal`; GHCR `lazuar-hub-portal` | **3004** Next `-p 3004` | `/portal` | Buyer/checkout SSR. `NEXT_PUBLIC_API_URL` + server `API_URL`. Magic link, `/public/commerce`. No password login (root README). | `apps/lazuar-pay-checkout` **5179** |
| `apps/lazuar-admin` | `lazuar-admin`; GHCR `lazuar-hub-superadmin` | **3005** Vite `strictPort` | `/admin/` (`handle_path`) | Hub staff UI. Cookie `lazuar_admin_auth` for `/api/v1/platform`. **Collides with One stock Login V2.** Not a Pay merchant destination. **No new Pay counterpart.** Platform staff stay on One `:5173` (and never as merchant homepage). |

Taskfile aliases that **start Hub**, not focused Pay:

| Task | What it actually starts |
|------|-------------------------|
| `task infra:up` | `docker-compose up db -d` → container `lazuar-db`, host **5432**, DB `lazuar_mvp`, Postgres **16**-alpine |
| `task infra:down` / `infra:reset` / `infra:logs` | Same compose project (directory default name `lazuar-pay`) |
| `task dev` | `deps: [infra:up]` then `pnpm --filter lazuar-api dev` → **old** API **8080** |
| `task fe` | `mprocs -c mprocs-dev.yaml` → developers 3002, ops 3003, portal 3004, admin 3005 (Caddy proc autostart **false**) |
| `task docs` | VitePress Hub integrator guides **5180** (`apps/lazuar-docs`) |
| `task proxy` / `proxy:up` / `proxy:down` / `proxy:validate` | Caddy **9080** via `docker-compose.dev-proxy.yml` |
| `task api:*` | Restore / build / test / migrate **Hub** `Lazuar.slnx` (nine DbContexts) |
| `task gen` / `contracts:honesty` | Hub `packages/api-spec` → `@repo/api-types-ts` / `api-types-dotnet` / LHDN SDKs |
| `task docker:*` | Bake/push **Hub** images; `docker:up:ghcr` / `docker:up:full` start Hub |
| `task tunnel:api` | **ngrok http 8080** — “standalone Pay, not Aura dual-run”. That “Pay” is Hub. |
| `task tunnel:fe` | **ngrok http 3004** — Hub portal |
| `task tunnel:cf` | Cloudflare named tunnel `pay-local.lazuar.dev` → **127.0.0.1:8090** (Hub dual-run next to Aura) |

Taskfile aliases that **start focused Pay** (the replacement):

| Task | Listen | Notes |
|------|--------|-------|
| `task pay:dev` | **8081** | `dotnet watch run` `Lazuar.Pay.csproj`. Comment in Taskfile: “not the old modular API” |
| `task pay:test` | n/a | `Lazuar.Pay.slnx` — health, whoami, org ready, checkout fixture, CORS, isolation. **Does not boot One.** |
| `task pay:build` / `pay:restore` | n/a | Focused solution only |
| `task pay:spec` | n/a | `packages/pay-spec` — **not** `packages/api-spec` |
| `task pay:merchant` | **5178** | `pnpm --filter lazuar-pay-merchant dev`. Desc: “not lazuar-ops” |
| `task pay:checkout` | **5179** | `pnpm --filter lazuar-pay-checkout dev`. Desc: “not lazuar-portal” |

Tab-completion footgun (012/05 already named it): `task dev` is Hub. `task pay:dev` is the product. During Consumer-0 dogfood, **do not type `task dev`.**

### 2.2 Compose files (three projects, three networks, Hub API on by default)

**`docker-compose.yml`** (no `name:` → project defaults to directory **`lazuar-pay`**). Comment at top: “Local runtime stack. Host Caddy (on the server later) reverse_proxies these ports.” Default `docker compose up` is **db + old api**. Frontends need `--profile full`.

| Compose service | Profile | `container_name` | Image (local build) | Host publish | Env / build-args that pin Hub |
|-----------------|---------|------------------|---------------------|--------------|-------------------------------|
| `db` | default | **`lazuar-db`** | `postgres:16-alpine` | **5432:5432** | `POSTGRES_DB=lazuar_mvp` |
| `api` | default | **`lazuar-api`** | `ghcr.io/proxeon/lazuar-hub-api:local` from `apps/lazuar-api/Dockerfile` | **8080:8080** | `ConnectionStrings__Default|TenantConnection|MessagingConnection` → `Host=db;…Database=lazuar_mvp`. `ASPNETCORE_ENVIRONMENT=Development` |
| `lazuar-ops` | `full` | `lazuar-ops` | `lazuar-hub-ops:local` from `apps/lazuar-ops/Dockerfile` | **3003:3000** | Build-arg `VITE_API_URL=${VITE_API_URL:-http://localhost:8080/api/v1}`, `VITE_PORTAL_URL=${VITE_PORTAL_URL:-http://localhost:3004}` |
| `lazuar-portal` | `full` | `lazuar-portal` | `lazuar-hub-portal:local` | **3004:3000** | Build-arg `NEXT_PUBLIC_API_URL` default `http://localhost:8080/api/v1`. Runtime `API_URL: http://api:8080/api/v1` (compose DNS **`api`** = old Hub, never One) |
| `lazuar-admin` | `full` | **`lazuar-superadmin`** | `lazuar-hub-superadmin:local` | **3005:3000** | Build-arg `VITE_API_URL` default Hub 8080 |
| `lazuar-developers` | `full` | `lazuar-developers` | `lazuar-hub-developers:local` | **3002:3000** | `OPENAPI_SPEC_ROOT=/app/openapi-specs` (Hub OpenAPI tiles) |

Network: explicit `name: lazuar-network`. Volume: `pgdata` → Docker name `lazuar-pay_pgdata`.

**There is no compose service for `apps/lazuar-pay`.** README of the focused host says it out loud: “Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real.” Swap, in this paper, means **replace the service**, not add a second API next to Hub on the same network still named `api`.

**`docker-compose.ghcr.yml`** — `name: lazuar-hub`. Same host ports. Same `container_name: lazuar-api` / `lazuar-db` / `lazuar-ops` / `lazuar-portal` / `lazuar-superadmin` / `lazuar-developers`. Images from GHCR `lazuar-hub-*:${TAG:-latest}`. **No profiles** — all frontends start. Portal still `API_URL: http://api:8080/api/v1`. Network still `lazuar-network` (explicit name) — **collides** with local compose’s network name if both projects try to own it.

**`docker-compose.dev-proxy.yml`** — `name: lazuar-dev-proxy`. Only service `caddy`, `container_name: lazuar-dev-caddy`, host **9080:9080**, volume `./deploy/dev/Caddyfile`. `extra_hosts: host.docker.internal:host-gateway`. **No Postgres. No app containers.** Upstreams are **host ports**, not compose DNS. If One owns host 8080, this gateway’s `/health` and `/api/*` become **One** while the path map still claims to be Hub.

**`deploy/prod/docker-compose.yml`** — `name: lazuar-hub`. Server path `/root/lazuar-hub-prod`. Only Caddy publishes 80/443. Internal network `hub` (`name: hub`). Container names: `hub-caddy`, `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`. API `ASPNETCORE_URLS: http://+:8080`, healthcheck `curl http://127.0.0.1:8080/health`. Portal `API_URL: http://api:8080/api/v1`, `NEXT_PUBLIC_API_URL` default `https://hub.lazuar.com/api/v1`. **This is production Hub.** It is the thing production DNS cutover must stop pointing at.

Focused Pay: **no** prod compose, **no** `hub-pay` container, **no** `ASPNETCORE_URLS` on 8081 in deploy/. That absence is the staging/production work of later 013 papers. This paper’s kill table assumes those files will exist **before** Hub goes dark — not that Hub compose is edited to add Pay as a sibling service on `hub` network still calling the old `api`.

### 2.3 Caddy (dev 9080 and prod hub.lazuar.com)

**Local** `deploy/dev/Caddyfile` (listen `:9080`):

| Handle | Upstream |
|--------|----------|
| `/health` | `host.docker.internal:8080` |
| `/api/*` | `host.docker.internal:8080` |
| `/portal*` | `host.docker.internal:3004` |
| `/docs*` | `host.docker.internal:3002` |
| `/admin` → redir `/admin/`; `handle_path /admin/*` | `host.docker.internal:3005` |
| catch-all `/` | `host.docker.internal:3003` (ops) |

**Production** `deploy/prod/Caddyfile` (site `hub.lazuar.com`):

| Handle | Upstream (compose DNS on network `hub`) |
|--------|------------------------------------------|
| `/health` | `api:8080` |
| `/api/*` | `api:8080` |
| `/portal*` | `portal:3000` |
| `/docs*` | `developers:3000` |
| `/admin` / `handle_path /admin/*` | `superadmin:3000` |
| catch-all `/` | `ops:3000` |

Comment: “DNS: hub.lazuar.com A → this VPS (Cloudflare DNS only / grey cloud for ACME).”

003-dev-caddy (`plans/003-dev-caddy/01-done.md`) pinned Vite `strictPort` and added the local gateway **on purpose** so local path layout matches prod. That was the right Hub DX. It is the **wrong** dogfood edge for One+Pay: `/health` on 9080 is “whatever owns 8080,” and 012/05 already called that a silent mis-route.

**Replace implication:** do not “add a `/pay` handle to the Hub Caddyfile” as the production shape. Hub path-routing exists because four Hub UIs share one host. Focused Pay is **two Vite origins + one API**, talking to **One** on a **different** host. A future Pay Caddy (if any) is a **new** site, not a fifth `handle` on `hub.lazuar.com` that still reverse_proxies `api:8080`.

### 2.4 GHCR, bake, CI, remote deploy

`docker-bake.hcl` group `default` targets: `api`, `lazuar-portal`, `lazuar-ops`, `lazuar-admin`, `lazuar-developers`. Flat image names (comment: avoids nested-package 403):

```text
ghcr.io/proxeon/lazuar-hub-api
ghcr.io/proxeon/lazuar-hub-ops
ghcr.io/proxeon/lazuar-hub-portal
ghcr.io/proxeon/lazuar-hub-superadmin
ghcr.io/proxeon/lazuar-hub-developers
```

Bake args default to **production Hub URLs**:

| Variable | Default |
|----------|---------|
| `VITE_API_URL` | `https://hub.lazuar.com/api/v1` |
| `VITE_PORTAL_URL` | `https://hub.lazuar.com/portal` |
| `NEXT_PUBLIC_API_URL` | `https://hub.lazuar.com/api/v1` |
| `NEXT_PUBLIC_OPS_URL` | `https://hub.lazuar.com` |
| `VITE_BASE_PATH_ADMIN` | `/admin/` |
| `NEXT_BASE_PATH` (portal) | `/portal` |

Labels: `org.opencontainers.image.source = https://github.com/proxeon/lazuar-hub`, description “Lazuar Hub CaaS platform”. OCI title per image is `lazuar-hub-*`.

`.github/workflows/ghcr.yml`: on push to `main` (paths `apps/**`, `packages/**`, bake, deploy, …) plus `workflow_dispatch`. Matrix builds the five Hub images `linux/amd64`, tags `latest` (default branch), `sha-<short>`, full SHA. Deploy job: rsync `deploy/prod/` → `/root/lazuar-hub-prod/` (excludes `.env`), rsync `scripts/remote-deploy.sh` → `/root/lazuar-hub-remote-deploy.sh`, optional inject `HUB_ENV_FILE`, then SSH `VERSION=sha-… /root/lazuar-hub-remote-deploy.sh`.

`scripts/remote-deploy.sh`: waits healthy on **`hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy`**. Then `docker compose exec -T caddy caddy reload`. There is no `hub-pay`. A green deploy today is a green **Hub** deploy.

`.github/workflows/ci.yml` jobs:

1. **`contracts`** — `task gen --force`, fail if Hub generated clients dirty, `scripts/check-openapi-minimal-honesty.mjs` against Hub Minimal API + `honesty-allowlist.yaml`.
2. **`dotnet`** — working-directory `apps/lazuar-api`, service Postgres 16 `lazuar_mvp` on **5432**, `LAZUAR_TEST_PG=Host=localhost;Port=5432;Database=lazuar_mvp;…`, tests Architecture / Integration / Module / Billing / Ops.

**No job runs `task pay:test`.** Killing Hub CI before focused Pay has its own workflow is how you go dark on both. Kill Hub CI **after** Pay CI is the gate (paper 10 in this program). This paper only records the coupling.

### 2.5 Env keys that mean Hub (do not reuse on 8081)

| Key | Where | Local default | What it points at |
|-----|-------|---------------|-------------------|
| `VITE_API_URL` | ops, admin (code + Docker build-arg + bake) | `http://localhost:8080/api/v1` | **Hub** `/api/v1`. Ops uses `credentials: "include"` (cookie `lazuar_auth`). |
| `VITE_PORTAL_URL` | ops Dockerfile / bake | `http://localhost:3004` / prod `/portal` | Hub portal |
| `VITE_BASE_PATH` | ops `/`, admin `/admin/` | mprocs sets admin `/admin/` | Hub path-routing |
| `NEXT_PUBLIC_API_URL` | portal browser | `http://localhost:8080/api/v1` | Hub |
| `API_URL` | portal **SSR** (compose + prod) | `http://api:8080/api/v1` | Compose DNS `api` = Hub container |
| `NEXT_PUBLIC_OPS_URL` | portal | `http://localhost:3003` / `https://hub.lazuar.com` | Hub ops; invite 302 target |
| `NEXT_BASE_PATH` | portal `/portal`, developers `/docs` | mprocs / bake | Hub Caddy prefixes |
| `Jwt__Secret` / `Jwt__Issuer=lazuar-api` / `Jwt__Audience=lazuar-clients` | Hub API + `deploy/prod/env.example` | Dev: `secure_development_key_minimum_32_characters_long` | Homemade HS256 cookie JWT. **Never copy into focused Pay.** |
| `App__ApiBaseUrl` | Hub (Billplz hop A) | tunnel `https://pay-local.lazuar.dev/api/v1` or `https://hub.lazuar.com/api/v1` | Public Hub `/api/v1`. Name collision with One’s `App__ApiBaseUrl` (different product). |
| `App__ClientUrl` / `App__OpsUrl` | Hub | `:3004` / `:3003` | Hub portal / ops |
| `App__CorsOrigins` | Hub Development | 3000–3005, 3020, 3021, 8080, 8090, **9080** | **Not** 5178/5179. **Not** One’s 517x. |
| `INTEGRATOR_PROVISION_SECRET` / `X-Lazuar-Provision-Key` | Hub | prod env.example | Hatch for `POST /api/v1/one/integrations/workspaces/provision` |
| `LAZUAR_HUB_BASE_URL` | sample cashier | `http://localhost:8080/api/v1` | Hub M2M |
| `LAZUAR_SK_TEST_KEY` | sample | `sk_test_…` | **Hub** prefix `sk_`, **not** One `lzr_sk_` |
| `LAZUAR_WEBHOOK_SECRET` | sample | `whsec_…` | Hub outbound HMAC |
| `ConnectionStrings__Default` etc. | Hub | `Host=localhost;Port=5432;Database=lazuar_mvp` | Hub Postgres. Dual-run Aura note in Taskfile: `localhost:5434 / lazuar_mvp` |
| `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` | Hub seed | `admin@lazuar.com` / `Password123!` | Hub superadmin. Not Ada. |

Focused Pay keys (do **not** collapse names):

| Key | Where | Local default |
|-----|-------|---------------|
| `One__BaseUrl` | `apps/lazuar-pay/.env.example`, `appsettings.json` `One:BaseUrl` | `http://localhost:8080/api/v1` meaning **One**, not Hub. Client appends `/me`. |
| `One__TimeoutSeconds` | same | `5` |
| `VITE_PAY_API_URL` | merchant + checkout `.env.example` | `http://localhost:8081` (no `/api/v1`) |

012/05 already warned: when a Pay SPA exists, prefer `VITE_PAY_API_URL` + (if needed) `VITE_ONE_API_URL`. Reusing Hub’s `VITE_API_URL` for 8081 is how you silently post money to One or identity to Hub.

**CORS on focused Pay today** (`Program.cs`): allow **only** `http://localhost:5178`, `http://127.0.0.1:5178`, `http://localhost:5179`, `http://127.0.0.1:5179`. Tests lock: merchant origin allowed, checkout origin allowed, **ops `http://localhost:3003` is not allowed** (`CorsTests.Health_does_not_allow_ops_origin`). That test is a kill-criteria oracle: if a PR adds 3003 to Pay CORS “so ops can talk to 8081,” it has implemented the strangler.

### 2.6 Ngrok, Cloudflare, dual-run hop A (Hub-shaped tunnels)

| Task | Target | Product it actually fronts |
|------|--------|----------------------------|
| `task tunnel:api` | `ngrok http 8080` | Hub API standalone. Prints Billplz pattern `{App__ApiBaseUrl}/webhooks/payments/billplz/{tenantId}`. Agent API **4040**. |
| `task tunnel:fe` | `ngrok http 3004` | Hub portal |
| `task tunnel:status` | `http://127.0.0.1:4040/api/tunnels` | Whatever ngrok advertised |
| `task tunnel:cf` | named tunnel default `aura-025-fulfillment`; `CF_PAY_HOST` default **`pay-local.lazuar.dev`** → `127.0.0.1:8090` | **Hub dual-run**. Aura owns 8080; Hub listens **8090**. Comment: “Do not also start ngrok for hop A.” |
| `task tunnel:cf:url` | probes `https://pay-local.lazuar.dev/health` | Expects Hub on **8090**. Prints `DB: localhost:5434 / lazuar_mvp` |

Root README: “Dual-run next to Aura (hop A): listen on **`:8090`** (Aura owns 8080).” One’s OpenFGA HTTP is **already** host **8090**. 012/05: this mode is for Aura, **not** for Consumer-0 One dogfood.

Hostname `pay-local.lazuar.dev` **looks like new Pay**. It is Hub. After replace, either retire the name or **repaint** it only when the origin is 8081 (or a new Pay public URL) **and** One still owns 8080. Do not leave a tunnel that health-checks Hub `/health` while docs say “Pay.”

`mprocs-dev.yaml` optional procs `ngrok-api-tunnel` / `ngrok-fe-tunnel` wrap those Hub tasks. `caddy` proc wraps `docker-compose.dev-proxy.yml`. None of these start focused Pay.

### 2.7 Docs, Scalar, Postman, ADRs that still teach Hub

| Surface | Port / URL | Claim |
|---------|------------|-------|
| Root `README.md` | Getting Started = `task infra:up` + `task dev` + `task fe` | “Lazuar Platform (Checkout-as-a-Service)” modular monolith. Port table is **Hub only** (8080, 3002–3005, 3020, 9080). Does not mention 8081 / 5178 / 5179. |
| `apps/lazuar-docs` VitePress | **5180** (`task docs`) | Hub integrator guides (cashier vs Commerce vs LHDN, Aura hop, sample app). **Collides with One VitePress 5180.** |
| `apps/lazuar-developers` Scalar | **3002** local; prod `/docs` | Hub OpenAPI tiles (`docs-payments`, commerce, lhdn, one, ops). |
| `docs/payments-integration-quickstart.md` | Hub `/api/v1` | M2M cashier. Product-line table: Payments vs **Commerce `/public/commerce/*`** vs LHDN vs Aura Plan. Local `http://localhost:8080/api/v1`; prod `https://hub.lazuar.com/api/v1`. |
| `docs/postman/` | — | Hub collection |
| `docs/architecture-decision-log/` | — | ADR 021/023 still describe Hub CaaS pivot; useful history, not new Pay IA |
| `packages/api-spec` | `task gen` | Hub TypeSpec SSoT. Includes `/public/commerce`, `/one/auth/*`, LHDN. |
| `issues/001`–`334` | — | Hub defect catalog. 261–334 still open on paper. Do not implement them on the cathedral as the replace plan. |

`apps/lazuar-pay/README.md` is the **honest** local DX for the new host (8081, One `One__BaseUrl`, do not set ops `VITE_API_URL` to 8081, fingerprint One on 8080). Root README has not been rewritten. Cutover of **docs** is a kill item: the first README a clone sees must not boot Hub on 8080 next to One.

### 2.8 Sample cashier (integrator of Hub, not of focused Pay)

`examples/hub-cashier-next` (package `@examples/hub-cashier-next`, port **3020**):

- `LAZUAR_HUB_BASE_URL=http://localhost:8080/api/v1`
- Machine key `sk_test_…` (Hub), webhook `whsec_…` (Hub)
- Provision: `POST $HUB/one/integrations/workspaces/provision` with `X-Lazuar-Provision-Key`
- Checkout: Hub `POST …/integrations/payments/checkouts`
- Webhook path: `/webhooks/hub/payments`
- README: “proves **Lazuar Hub** as a multi-app payments cashier.” Prerequisites: “Hub API on `http://localhost:8080` (`task dev`).”

Root turbo **excludes** `@examples/*` from product `pnpm build/dev/lint/test`. The sample is still a **customer of Hub `/api/v1`**. After replace, a sample that still posts to Hub is a live integrator of a dark product. See §8.

### 2.9 New surfaces (the replacement set — inventory for contrast)

| Path | Listen | What it is today (`6f866ff0`) | Compose / GHCR / Caddy |
|------|--------|-------------------------------|-------------------------|
| `apps/lazuar-pay` | **8081** | Health, whoami, org ready, in-memory checkout fixture. CORS 5178/5179 only. `One:BaseUrl` → One 8080. **No DB.** No Dockerfile. | None |
| `apps/lazuar-pay-merchant` | **5178** `strictPort` | Health probe of `VITE_PAY_API_URL`. No OIDC. Not ops. | None |
| `apps/lazuar-pay-checkout` | **5179** `strictPort` | Health probe. Buyers have no One account. Not portal. | None |
| `packages/pay-spec` | documents `:8081` `/v1` | Health, whoami, orgs ready, checkouts. README: do not import One, LHDN, or `/public/commerce`. | n/a |

`pnpm dev` at **repo root** is `turbo run dev --filter=!@examples/*`. Every app with a `dev` script starts: **Hub API 8080**, focused Pay 8081, ops 3003, portal 3004, admin 3005, developers 3002, docs 5180, merchant 5178, checkout 5179. That is the loudest dual-run fail on a laptop (§4).

### 2.10 Demo credentials that claim to be “Pay login”

From root README (Hub Development seed `appsettings.Development.json`):

| Role | App | URL | Email | Password |
|------|-----|-----|-------|----------|
| Superadmin | Hub `lazuar-admin` | `:3005` | `admin@lazuar.com` | `Password123!` |
| Tenant admin | Hub `lazuar-ops` | `:3003` | `founder@acme.test` | `Password123!` |

Workspace slug **`acme`**. Portal `:3004` has no password login.

One (012/05 / One README):

| Role | App | URL | Email | Password |
|------|-----|-----|-------|----------|
| Customer | `lazuar-app` | `:5174` → login `:5175` | `ada@acme.test` | `Password1!` |
| Staff | One `lazuar-admin` | `:5173` | `zitadel-admin@zitadel.localhost` | `Password1!` |

Mixing these tables is a fail mode. After replace, Hub rows **die with Hub**. Merchants are Ada (One humans). Staff console for **Lazuar** is One `:5173`, never Hub `:3005`, never a Pay password form.

---

## 3. Port and DNS collisions with One

The local port bible is 012/05. This section restates the collisions that **block replace**, plus DNS names that will lie after a half-cutover, plus the Postgres **5432 vs 5435** rule for the *next* Pay database (not yet in compose).

### 3.1 Host port matrix (One vs Hub vs focused Pay)

“Taken by” = default/local DX of that product. Empty = that product does not publish it.

| Host port | One (`0f79fe4`) | Old Hub (this repo) | Focused Pay | If both default DX bind |
|-----------|-----------------|---------------------|-------------|-------------------------|
| **3002** | — | `lazuar-developers` | — | No (One does not use 3002) |
| **3003** | — | `lazuar-ops` `strictPort` | — | No. Still: do not retarget at 8081. |
| **3004** | — | `lazuar-portal` | — | No |
| **3005** | **zitadel-login** stock Login V2 (compose **always-on**, `3005:3000`) | **lazuar-admin** `strictPort`; compose `full` `3005:3000`; Caddy `/admin` | — | **YES.** Shipping merchants here is ambiguous **and** `NP-ONE-005`. |
| **3009** | OpenFGA playground | — | — | No |
| **3020** | — | sample cashier | — | No |
| **4040** | — | ngrok agent | — | No |
| **4178 / 4179** | — | — | merchant/checkout `vite preview` | Keep off 5173–5177 |
| **5173** | **lazuar-admin** staff SPA | — (Hub admin is 3005) | — | No. Merchants **never**. |
| **5174** | **lazuar-app** | — | — | No. Ada mints JWT here. |
| **5175** | **lazuar-login** | — | — | No. Password UI. Not Pay homepage. |
| **5176** | login BFF loopback | — | — | No |
| **5177** | examples/vite-spa | — | — | No |
| **5178** | — | — | **lazuar-pay-merchant** `strictPort` | Intended. Do not move to 5173. |
| **5179** | — | — | **lazuar-pay-checkout** `strictPort` | Intended. Do not move to 3004. |
| **5180** | **lazuar-docs** VitePress | **lazuar-docs** VitePress | — | **YES** if both `pnpm docs:dev` |
| **5181** | lazuar-reference Scalar | — | — | No |
| **5432** | Compose Postgres 17 `lazuar-postgres` DBs `zitadel` / `openfga` / `lazuar` | Compose Postgres 16 `lazuar-db` DB `lazuar_mvp`; `task infra:up` | **none today** | **YES.** One `deploy/dev/README.md`: “Common if another stack (e.g. lazuar-pay) holds Postgres. Set `POSTGRES_PUBLISHED_PORT=5433`.” |
| **5433** | Documented **remap** when 5432 is taken | — | — | One’s workaround, not Pay’s future money port |
| **5434** | — | Taskfile dual-run print: `DB: localhost:5434 / lazuar_mvp` (Aura laptop) | — | Aura-shaped leftover. Not One. |
| **5435** | — | **not bound** | **recommended published port for Pay money Postgres when it exists** | See §3.3. Not in any compose file at this SHA. |
| **8080** | **One API** `pnpm api:dev` **or** compose profile `api` | **Hub API** `task dev` **or** compose `api` **or** GHCR `api`; Caddy `/health` `/api/*` | — | **YES — the collision 012 exists for.** One **must** own 8080 when both products run. |
| **8081** | OpenFGA **container** gRPC 8081 published as **host 8091** | — | **focused Pay** | Host 8081 is free relative to One. Do not publish OpenFGA gRPC onto 8081. |
| **8085** | **Zitadel** issuer / Console | — | — | Keep free. Pay never binds this. |
| **8090** | **OpenFGA HTTP** | Hub **dual-run** listen (Aura hop A); Hub CORS includes 8090 | — | **YES** if `task tunnel:cf` / Hub-on-8090 |
| **8091** | OpenFGA gRPC | — | — | No |
| **9080** | Mentioned as a *future* One proxy idea; not implemented | **Caddy** Hub gateway | — | Keep Hub Caddy **down** during One+Pay. `/health` is Hub-shaped and points at 8080. |

### 3.2 Container names and Docker networks (hard errors, not just ports)

| Name | One | Hub | Collision |
|------|-----|-----|-----------|
| `container_name: lazuar-api` | compose **profile `api`** | compose `api` **and** GHCR `api` | **Global Docker name.** Cannot run both. Default One DX does **not** start this profile — do not start it during dogfood. |
| `container_name: lazuar-postgres` | default compose | — | vs Hub `lazuar-db` (different name, **same host 5432**) |
| `container_name: lazuar-db` | — | default compose / GHCR | Stop it when One needs 5432 |
| Network `lazuar-one-network` | explicit | — | DNS: `postgres`, `zitadel-api`, `openfga` |
| Network `lazuar-network` | — | local + GHCR compose (explicit `name: lazuar-network`) | DNS: `db`, `api` (**Hub**) |
| Network `hub` | — | **prod** compose | DNS: `api`, `ops`, `portal`, `superadmin`, `developers`, `caddy` |
| Project `lazuar-one` | yes | — | |
| Project `lazuar-pay` (directory default) | — | local compose | |
| Project `lazuar-hub` | — | GHCR compose **and** prod compose | Two files, same project name if used on one engine with different compose files — ops hazard |
| Volume `lazuar-one_lazuar_pgdata` | yes | — | Data **not** shared with Hub |
| Volume `lazuar-pay_pgdata` | — | local | PG 16 vs One PG 17. Do not point One at `lazuar_mvp` or Pay at `lazuar`. |

012/05: three Compose projects, **no peering**. Focused Pay is a **host** `dotnet` process. One API default DX is a **host** `dotnet` process. They talk `localhost:8080` / `localhost:8081`. Do not invent a shared Docker network for whoami. When Pay later gets a compose service, **do not** join `lazuar-network` and expect to resolve One as `api` — that `api` is Hub.

### 3.3 Postgres: 5432 vs 5433 vs 5434 vs **5435**

Nothing in this repo **binds 5435** at `6f866ff0`. The number is the **cutover assignment** so the next Pay database does not repeat Hub’s fight with One.

| Port | Who uses it today | After replace (recommendation) |
|------|-------------------|--------------------------------|
| **5432** | **Both** default composes. One needs it for Zitadel + OpenFGA + `lazuar`. Hub `task infra:up` takes it for `lazuar_mvp`. | **One only.** Identity plane. Never Hub. Never new Pay. |
| **5433** | One’s documented **escape hatch** when 5432 is already Hub (`POSTGRES_PUBLISHED_PORT=5433` + host `ConnectionStrings__Lazuar` Port=5433). Container-internal DSN stays `postgres:5432`. | Keep as One’s remap only if a leftover Hub db is still up during a messy laptop week. Not a product port. |
| **5434** | Taskfile `tunnel:cf:url` print for Aura dual-run Hub DB. | Retire with Hub dual-run. Do not reuse for new Pay (Aura folklore). |
| **5435** | Unbound | **Focused Pay money Postgres** when S1 stops being in-memory. Publish `5435:5432`. Database name **not** `lazuar_mvp` and **not** One’s `lazuar` (e.g. `lazuar_pay`). Image pin independent of One 17 / Hub 16. |

Why 5435 instead of “also 5432, we’ll be careful”:

- One compose **defaults** to 5432. Zitadel and OpenFGA DSNs inside `lazuar-one-network` assume container `postgres:5432`. Host-run One API assumes `Host=localhost;Port=5432;Database=lazuar` unless overridden.
- Hub compose **defaults** to 5432. `task infra:up` is muscle memory. CI Hub job **also** publishes 5432.
- 012/05 whoami rule: focused Pay has **no DB yet — leave Pay db down.** That remains true until money is real. The moment Pay grows a database, if it publishes 5432 it **reopens the whoami collision** even if Hub is gone, because a careless `task infra:up` leftover or a second clone will fight One.
- Assigning **5435** makes `lsof -iTCP:5432` a fingerprint of **One**, and `lsof -iTCP:5435` a fingerprint of **Pay money**. 8080/8081 already work that way for HTTP.

Hub tests that open `localhost:5432` or `LAZUAR_TEST_PG` (`BillingQueryServiceTests` and friends) die with Hub CI. They are not a reason to keep 5432 for Pay.

### 3.4 DNS names that will lie

| Name | Today | After a half-cutover | Honest end state |
|------|-------|----------------------|------------------|
| `http://localhost:8080` | Hub **or** One depending on who won the bind | Still the footgun | **One API only** whenever Pay is running |
| `http://localhost:8081` | Focused Pay | Focused Pay | Focused Pay forever locally |
| `http://localhost:9080` | Hub path gateway | If left up with One on 8080: `/api/*` is One, `/` is ops cookie world | **Off.** Do not reuse 9080 as “Pay gateway” without a new Caddyfile |
| `https://hub.lazuar.com` | Production Hub path router | If DNS still hits Hub VPS after Pay is “the product,” merchants still get ops | Dark or static “moved” page. Money is not here. |
| `https://hub.lazuar.com/api/v1` | Integrator base (quickstart, sample, Aura) | Webhooks still arrive if not cut | Integrators use **Pay** public `/v1` (hostname TBD, §11) |
| `https://pay-local.lazuar.dev` | Cloudflare → Hub **8090** | Looks like new Pay | Either retire or retarget to **8081** (or Pay staging) **after** Hub-on-8090 is stopped |
| `https://pay.lazuar.com` / similar | **does not exist in these files** | Do not assume | Open question §11 |
| Compose DNS `api` | Hub container | A Pay compose that reuses service name `api` on `lazuar-network` will be read as Hub by leftover portal env | New service name (`pay`), new network, or host ports only |
| Container `lazuar-api` | Hub or One profile `api` | `docker logs lazuar-api` is ambiguous | One should not use this name if Pay ever containerizes; Pay should not steal it |

### 3.5 Fingerprints (keep using these during every phase)

Copied from 012/05; they remain the cutover health checks. **Never** use `GET /health` → `{status:ok}` alone to decide who owns 8080.

| Probe | One | Hub | Focused Pay |
|-------|-----|-----|-------------|
| `GET :8080/api/v1/` | `{"name":"lazuar-one-api","version":"v1"}` | not that body | n/a (Pay is 8081) |
| `GET :8080/health/live` | 200 | **404** | n/a |
| `GET :8080/health/metrics` | **404** | 200 | n/a |
| `GET :8080/api/v1/me` anonymous | 401 ProblemDetails | typically **404** (Hub whoami is `/api/v1/auth/me` or `/one/auth/me`) | n/a |
| `GET :8080/v1/health` | **404** | **404** | n/a |
| `GET :8081/v1/health` | n/a | n/a | `{"status":"ok"}` |
| `GET :8081/health/live` | n/a | n/a | **404** |
| `GET :5175/health` | `service: lazuar-login` | n/a | n/a |
| `GET :3005/ui/v2/login/healthy` | stock Login V2 | if Hub admin won 3005: **not** this path | n/a |

Focused Pay whoami: `GET :8081/v1/whoami` with `Authorization: Bearer <access_token>`. Missing header → 401. One down → 503. That path is **not** Hub `/one/auth/me` and **not** One `/api/v1/me` (Pay **forwards** to One `/me`).

---

## 4. Dual-run shapes that work vs that fail

“Dual-run” is overloaded in this repo. Count **three** historical meanings:

1. **Aura dual-run:** Aura owns 8080; Hub listens **8090**; tunnel `pay-local.lazuar.dev`. Root README / `task tunnel:cf`.
2. **Hub full stack:** API 8080 + ops/portal/admin/developers + optional Caddy 9080. `task dev` + `task fe`.
3. **Consumer-0 dual-run (the one this program proved):** **One API 8080 + focused Pay 8081** (+ One identity compose + login 5175 + app 5174). Hub **off**.

Only (3) is the proven whoami shape. (1) **collides with OpenFGA 8090**. (2) **collides with One 8080 / 5432 / 3005**.

### 4.1 Works (no bind overlap, honest fingerprints)

**W1 — Proven whoami laptop (012/05 + C99).** Leave Hub down.

```text
One compose:   postgres :5432, zitadel :8085, login-v2 :3005, openfga :8090/:8091
One host:      API :8080, login :5175 (+BFF 5176), app :5174
Pay host:      focused Pay :8081
Optional:      One admin :5173 (staff only), One docs :5180, One reference :5181
Pay optional:  merchant :5178, checkout :5179 (health probe; CORS already allows them)
```

Commands (copy from 012/05, still correct):

```bash
# One repo
./scripts/bootstrap-local.sh
pnpm api:dev          # 8080  — this is One
pnpm login:dev        # 5175
pnpm app:dev          # 5174

# Pay repo — do NOT task dev / infra:up / fe / proxy / compose up
task pay:dev          # 8081
task pay:merchant     # 5178  (optional)
task pay:checkout     # 5179  (optional)
```

Fingerprint 8080 as One **before** debugging tokens. `One__BaseUrl=http://localhost:8080/api/v1` on Pay is One, not self, not Zitadel 8085, not OpenFGA 8090.

**W2 — Hub-only laptop (museum mode).** No One. `task infra:up` + `task dev` + `task fe` + optional `task proxy`. This **works as Hub**. It is **not** Consumer-0. Allowed for: reading a Hub test as an oracle, reproducing a Hub bug, emergency rollback of Hub **on a machine that is not running One**. Forbidden as the daily Pay engineering default once 013 starts shipping money.

**W3 — One identity + Pay 8081 + Pay Postgres 5435 (future).** When Pay grows a DB, publish **5435**, not 5432. One keeps 5432. No Hub `lazuar-db`. This is the intended **post-replace local** shape. Not implementable today (no Pay DbContext). Recorded so the first Pay compose file does not copy Hub’s `"5432:5432"`.

**W4 — Staging/prod: One in its estate, Pay in its estate.** Different hosts or at least different published ports. One’s 8080 is One’s load balancer. Pay’s public API is **not** 8080 on the same instance. No Hub containers. This is phase 2–3 (§5). “Works” here means **no port math on one kernel** — the collision class goes away.

### 4.2 Fails (bind, mis-route, or identity lie)

| ID | Shape | Why it fails | Symptom |
|----|-------|--------------|---------|
| **F1** | Hub `task dev` **and** One `pnpm api:dev` | Both `launchSettings` `http://localhost:8080` | Second process bind error **or** silent winner. `GET /health` still `{status:ok}`. Whoami talks to the wrong product. |
| **F2** | Hub `docker compose up` (default **includes api**) **and** One host API | Same 8080; compose `container_name: lazuar-api` | Host API cannot bind; or container owns 8080 and `One__BaseUrl` hits **Hub** |
| **F3** | Hub compose api **and** One `docker compose --profile api` | `container_name: lazuar-api` **global** | Docker refuses the second container |
| **F4** | Hub `task infra:up` **and** One default postgres | Both **5432** | Second Postgres fails. One Zitadel/OpenFGA/host API unhappy. One’s own README remap is 5433, which is a workaround, not a lifestyle. |
| **F5** | Hub `task fe` / admin / compose `full` **and** One compose (stock Login V2) | Both **3005** | Vite `strictPort` fails **or** Login V2 disappears. Landing on `:3005` might be Hub admin **or** Zitadel. |
| **F6** | Hub `task tunnel:cf` / Hub-on-8090 **and** One OpenFGA | Both **8090** | One `OpenFga:ApiUrl=http://localhost:8090` hits **Hub**. Pay must never talk 8090 anyway. |
| **F7** | Both `pnpm docs:dev` | Both **5180** | Second docs site loses |
| **F8** | Pay `task proxy` (`:9080`) while One owns 8080 | Caddy `/health` + `/api/*` → host 8080 | Gateway health-checks **One**, still routes `/` to ops. Ops cookies `lazuar_auth` 401 on One. Looks like “identity is broken.” |
| **F9** | Pay root `pnpm dev` (turbo) | Starts **Hub API + focused Pay + ops + portal + admin + developers + docs + merchant + checkout** | F1 + F5 + F7 + leftover 3003 talking to whoever won 8080. Partial success is worse than a clean bind error. 012/05 item 9. |
| **F10** | Hub `pnpm --filter lazuar-api dev` in the **wrong repo** (`cd apps/lazuar-api` in One vs Pay) | Same folder name | Wrong host, same port |
| **F11** | `VITE_API_URL=http://localhost:8081/api/v1` on ops/portal | P60 | Ops posts `POST /one/auth/login` to Pay. Pay does not map it. Types from `@repo/api-types-ts` lie. CORS test would have to be inverted. **Strangler.** |
| **F12** | `NEXT_PUBLIC_API_URL` / portal `API_URL=http://api:8080` while compose `api` was swapped to focused Pay without rewriting portal | Portal SSR still wants Hub `/api/v1` + cookie | 404/401 storm. Not a migration. |
| **F13** | Sharing Hub cookie `lazuar_auth` with One or Pay | Different JWT (issuer `lazuar-api`, HMAC secret) | One 401. Pay whoami forwards that cookie **as Bearer** only if someone pastes it — still 401 at One. |
| **F14** | Ada password on Hub ops (`Password1!` on `:3003`) or founder on One (`Password123!` on `:5175`) | Different IdP | Login fails; people “fix” by building a Pay password form |
| **F15** | Merchants sent to `:5173` or `:3005` | `NP-ONE-005` | Staff console or Login V2 or Hub admin |
| **F16** | OpenFGA playground targeting **:8080** | Playground iframe folklore | Talks to One or Hub, never FGA (FGA is **8090**) |
| **F17** | Focused Pay `One__BaseUrl` left at 8080 while Hub won the bind | Runtime copy of `Modules/One` | Whoami JSON is not One `MeResponse` projection. C99 violated without a code change. |
| **F18** | Two clones of `lazuar-admin` in the brain | One staff 5173 vs Hub 3005 | “Open admin” is undefined |
| **F19** | GHCR `docker compose -f docker-compose.ghcr.yml up` on a laptop that also runs One | 8080, 5432, 3005, `container_name: lazuar-api`, network `lazuar-network` | Same as F2–F5 plus **production images** locally |
| **F20** | Prod Hub compose + a new Pay container on network `hub` still named `api` | Caddy `reverse_proxy api:8080` | Edge traffic still Hub **or** Pay on Hub’s path map (`/` = ops). Not replace. |

### 4.3 Dual-run that people will propose and must refuse

**“Keep Hub UI, new API.”** Point ops at 8081. This is the strangler P60 exists to forbid. Evidence:

- Ops `api-client.ts`: `credentials: "include"`; types `One.AuthUser`; intercepts `X-Tenant-Id` from `ops_active_workspace_id`.
- Hub auth: `POST /one/auth/login` → cookie `lazuar_auth` HMAC JWT (`Jwt:Issuer=lazuar-api`).
- Focused Pay: Bearer access_token or later `lzr_sk_`. No cookie JWT. CORS **excludes** 3003.
- Pay TypeSpec `/v1/whoami` is a **projection**, not Hub `AuthUser`, not One `MeResponse` clone (012/04).
- Hundreds of Hub routes (`/admin/commerce`, `/lhdn`, `/ops/chat`) are not on 8081 and **must not** be added to make ops compile.

**“Keep Hub API, new Vite.”** Point merchant 5178 at Hub 8080. That ships a new skin on the cathedral. IsolationTests ban `lazuar-api` / `Modules.` / `MediatR` in the **host**; the UI would still be a Hub client (`@repo/api-types-ts`). Replace requires **both** new API and new UIs.

**“Run both APIs, remap Hub to 8090 like Aura.”** Then OpenFGA is gone or remapped, `pay-local.lazuar.dev` still means Hub, and engineers have three 8080 stories (Aura, One, Hub). Allowed only as a **time-boxed Hub museum** on a machine **without** One. Not the team default.

**“One compose profile `api` plus Pay compose `api` with different project names.”** `container_name: lazuar-api` is still global. Remove the name **or** don’t start both. Prefer neither container during dogfood.

### 4.4 What `task` names encourage

| You type | You think | You get |
|----------|-----------|---------|
| `task dev` | “start Pay” | Hub 8080 + Hub Postgres 5432 |
| `task pay:dev` | “start Pay” | Focused 8081 (**correct**) |
| `task fe` | “frontends” | Hub 3002–3005, not 5178/5179 |
| `task pay:merchant` / `pay:checkout` | “frontends” | 5178 / 5179 (**correct**) |
| `task proxy` | “like prod” | Hub Caddy 9080 → 8080 |
| `task docker:up:full` | “full stack” | Hub compose `--profile full` |
| `task infra:up` | “deps” | Hub Postgres **5432** (fights One) |
| `pnpm dev` | “all apps” | F9 turbo pile-up |

Replace of **DX** is part of replace of **product**. Kill criteria for `task dev` is in §6.

---

## 5. Cutover phases

Replace is sequenced so that One **never** loses 8080 to Hub, and old UIs are **never** the clients of 8081. Each phase has an enter gate, a running shape, and an exit gate. Later 013 papers (host seams, merchant OIDC, money, data) fill the enter gates; this paper locks the **topology** of each phase.

### Phase A — Local dogfood (now → S1 on a laptop)

**Goal:** Ada signs in on One (`:5174` → `:5175`), Pay on **8081** knows who she is, merchant **5178** and checkout **5179** are the only Pay browser origins, Hub is **off** on this machine.

**Enter:** C99 connected (already true at `6f866ff0`). IsolationTests green. Do not wait for Hub parity.

**Running shape:** W1 in §4. Hub tasks listed in 012/05 “Leave down” stay down: `task dev`, `infra:up`, `fe`, `proxy`, `docker compose up`, `tunnel:cf`, `pnpm dev` turbo.

**Exit (this phase does not ship customers):**

- Fingerprint 8080 = One every session (`/api/v1/` name `lazuar-one-api`).
- `GET :8081/v1/whoami` 200 with Ada JWT; 401 without.
- Merchant 5178 and checkout 5179 reachable; ops 3003 **not** in Pay CORS.
- P10 still honest: OIDC unwired is OK for curl dogfood; **not** OK as “production-ready UI.”
- Checkout fixture may remain in-memory. Do not call that production.

**Forbidden in Phase A:** setting ops `VITE_API_URL` to 8081 “to demo faster”; starting Hub “because the sample cashier README says `task dev`.”

### Phase B — Staging, **new stack only**

**Goal:** A staging URL serves **focused Pay + merchant + checkout + One staging**. **Zero** Hub containers. **Zero** Hub GHCR images. **Zero** `hub.lazuar.com` path map.

**Enter (owned by other 013 papers, listed so cutover does not start early):**

- Pay has a real database (this paper: publish **5435** locally / staging equivalent, never 5432).
- Secrets: no `Jwt__Secret` Hub cookie; no Zitadel PAT on Pay (`NP-ONE-020`).
- Health that is more than process-up when a DB exists (paper 03).
- Merchant OIDC against staging Zitadel / One (paper 04 / 08). Checkout still **no** Zitadel (paper 05).
- CI runs `task pay:test` (paper 10). Hub CI may still run on `main` until Phase D.

**Running shape:**

- One staging owns **identity** HTTP (whatever port/host One uses there — not Pay’s job to steal).
- Pay staging API is **not** bound as host 8080 on a box that also runs One.
- No service named `api` that is Hub. No Caddy `handle /` → ops.
- Sample cashier, if used, pointed at **Pay `/v1`**, not Hub `/api/v1` (§8).
- Hub VPS may still run for rollback (§9) but **staging DNS for Pay must not hit it**.

**Exit:** a stranger (you, tomorrow) can finish a dogfood sentence on staging **without** cloning Hub: sign-in via One login, merchant origin, buyer origin, Pay `/v1`. Staging is not “Hub compose with an extra 8081 sidecar.”

**Hub on staging:** if a Hub staging exists today, it stays **Hub staging**, labelled Hub. It is not “Pay staging.” Do not put both behind one hostname.

### Phase C — Production DNS

**Goal:** Customers and integrators resolve **Pay** to the new stack. `hub.lazuar.com` stops being the money door.

**Enter:** Phase B boring for the dogfood loop (011/01 sentence), not for Hub feature-parity. Wrap-rails, receipt `RCPT-`, journal in one handler — those bars live in papers 06–07. This paper only requires: **the hostname you print on invoices and in integrator docs is the new stack.**

**Running shape (DNS, not code):**

1. New public origin(s) for Pay API `/v1`, merchant SPA, checkout SPA (names open — §11).
2. Integrator docs (`payments-integration-quickstart`, sample README, Scalar) print the **new** base. Hub `/api/v1` marked **legacy / sunset**.
3. Gateway webhooks (Stripe/CHIP/…) endpoint on **Pay**, not `https://hub.lazuar.com/api/v1/webhooks/payments/…`.
4. One production remains the IdP. Pay still does not bind One’s 8080-equivalent.
5. `hub.lazuar.com` either: (a) static sunset page, (b) 301 **only** for human marketing paths that have a new URL, (c) still Hub **read-only** for a documented window — **not** a second writer of money.

**Caddy:** production Hub Caddyfile is **not** edited to `reverse_proxy pay:8081` for `/api/*` while `/` still serves ops. That is a strangler at the edge. If Pay needs TLS termination, it gets **its own** site block / its own VPS / its own compose.

**GHCR:** new images (`lazuar-pay`, `lazuar-pay-merchant`, `lazuar-pay-checkout` — names open) are what CD pulls. Hub matrix may still build for rollback until Phase D.

**Exit:** a merchant who bookmarks “ops” lands on **5178-equivalent production**, signs in via **One login**, not `founder@acme.test` on Hub. An integrator’s `POST /v1/checkouts` (or the locked Pay public path) hits focused Pay. `curl https://hub.lazuar.com/health` is **not** the Pay liveness you page on.

### Phase D — Hub dark

**Goal:** Hub processes stopped. Hub images not pulled. Hub Postgres not written. Hub Caddy not serving money. Old app folders eligible for **delete** (not just stop).

**Enter:** Phase C stable; rollback window elapsed (§9); data decision from paper 09 (migrate vs greenfield) executed or explicitly “greenfield, Hub data abandoned”; sample + Aura-class integrators moved or sunset (§8).

**Running shape:** W4 only. Local DX README starts with `task pay:dev` + One bootstrap, not `task infra:up`. `docker compose up` in this repo either **does not exist** or starts **Pay 8081 + Pay DB 5435**, never Hub 8080.

**Exit:** kill criteria in §6 all checked or explicitly deferred with an owner. `container_name: lazuar-api` in **this** repo is gone. GHCR `lazuar-hub-*` not updated. CI `dotnet` job for `apps/lazuar-api` gone or moved to an archive branch.

**Dark ≠ delete on day one.** Dark is **stop serving**. Delete is a later commit when grep for `lazuar-ops` is allowed to be empty. Keep the tree as **git history / `archive/` / a tag** if legal/ops need it. Do not keep it as `task dev`.

### Phase map (one screen)

```text
A  local     One:8080 + Pay:8081 + :5178 + :5179     Hub OFF on dogfood laptop
B  staging   new stack only                          Hub VPS may exist but not this DNS
C  prod DNS  customers → new stack                   hub.lazuar.com sunset / not money
D  Hub dark  Hub processes stopped                   then delete artifacts per §6
```

Never: A+F11 (strangler), B with Hub compose “just in case” on the same hostname, C with Caddy `/api` → Pay and `/` → ops, D while Aura still posts to Hub provision.

---

## 6. Kill criteria for each old artifact

“May delete” means a later program is allowed to remove it from **default DX, CI, CD, and eventually the tree**. Until the criterion is true, the artifact stays **as Hub**, labelled Hub, not retargeted.

Each row: **keep until** / **kill when** / **how you would cheat** (refuse).

### 6.1 `apps/lazuar-api` (Hub process + cathedral)

| | |
|--|--|
| **Keep until** | Focused Pay takes money for real (webhook + journal + `RCPT-` in one handler — 011/01). Tests that are **oracles** have been stolen or cited (§7). Rollback window still needs a runnable Hub **or** a tagged image. |
| **Kill when** | Phase D enter. No integrator `LAZUAR_HUB_BASE_URL`. No GHCR deploy of `lazuar-hub-api`. No `task dev`. Fingerprint 8080 is One on every Pay laptop. |
| **Cheat** | Keep adding features to Modules/* “until Pay catches up.” That is 011/09 year-two core. Refuse. |

`task api:test` / nine DbContext migrations die with the app. Do not port MediatR to 8081 to keep them green.

### 6.2 Compose service `api` / `container_name: lazuar-api` / default `docker compose up` starts Hub

| | |
|--|--|
| **Keep until** | Phase A–B: file may remain so Hub museum and rollback still `compose up`. Must **not** be the README default once Phase A is the team DX. |
| **Kill when** | Default compose in this repo starts **Pay** (8081) + **Pay DB 5435** **or** starts nothing (host `task pay:dev` + One compose). Service name is **not** `api` if One profile `api` still uses `container_name: lazuar-api` anywhere an engineer might combine engines. |
| **Cheat** | Add focused Pay as a second service beside Hub `api` on `lazuar-network`. That is dual-run F2 forever. |

### 6.3 `task infra:up` / container `lazuar-db` / host **5432** Hub Postgres

| | |
|--|--|
| **Keep until** | Hub museum needs `lazuar_mvp`. CI Hub job needs 5432 (until Hub CI killed). |
| **Kill when** | Pay money DB publishes **5435** (or cloud equivalent). One owns **5432**. No `task infra:up` that binds 5432. Volume `lazuar-pay_pgdata` discarded or archived, not reused as One’s `lazuar`. |
| **Cheat** | Remap Hub db to 5433 and leave it in the default Taskfile. Then One’s documented 5433 hatch collides. |

### 6.4 `apps/lazuar-ops` / compose `lazuar-ops` / Caddy catch-all `/` / GHCR `lazuar-hub-ops` / port **3003**

| | |
|--|--|
| **Keep until** | `lazuar-pay-merchant` does the merchant job: OIDC to One, whoami, products/keys/receipts as a **client of `/v1`** (paper 04). Not when 5178 only shows `/health`. |
| **Kill when** | Production merchant origin is 5178-equivalent. No bookmark `https://hub.lazuar.com/` serving ops. `VITE_API_URL` for ops never pointed at 8081 (even in a leftover `.env`). CORS on Pay still excludes 3003. |
| **Cheat** | `VITE_API_URL=http://localhost:8081` “temporary.” P60. `CorsTests` would fail or be “fixed” by allowing 3003 — treat that diff as a stop-ship. |

**When ops may be deleted:** Phase D + merchant SPA dogfood (011/01 “merchant sees payment and receipt”). Until then ops is a **Hub museum UI**, still on Hub 8080.

### 6.5 `apps/lazuar-portal` / port **3004** / Caddy `/portal*` / GHCR `lazuar-hub-portal`

| | |
|--|--|
| **Keep until** | `lazuar-pay-checkout` is a hosted pay page (not a health probe): buyer **without** a One account can pay; magic-link/receipts later on **this** origin, not ops. |
| **Kill when** | Production checkout origin is 5179-equivalent. Caddy `/portal*` removed from **any** edge that still claims to be Pay. Portal `NEXT_PUBLIC_API_URL` never set to 8081. |
| **Cheat** | Portal as “buyer SPA” talking to Pay `/v1` while still implementing Hub `/public/commerce` GUID arrears. That copies a P0 (bare GUID update-payment) into the new stack. |

**When portal 3004 is removed from Caddy:** when **no** production or local gateway still path-routes `/portal` to Hub, **and** checkout 5179 (or its prod host) is the only buyer origin in docs. Local `deploy/dev/Caddyfile` handle `/portal*` can die as soon as Phase A is the default DX (the whole 9080 gateway should be down for dogfood anyway). Prod `deploy/prod/Caddyfile` `/portal*` dies in Phase C–D, not by pointing `/portal` at checkout while `/` is still ops.

### 6.6 `apps/lazuar-admin` / port **3005** / Caddy `/admin` / GHCR `lazuar-hub-superadmin` / container `lazuar-superadmin`

| | |
|--|--|
| **Keep until** | Nothing in Pay needs it. Hub platform staff (`admin@lazuar.com`) is not a Pay role. One staff is `:5173`. |
| **Kill when** | Phase A can already leave it down forever on dogfood laptops. Production: Phase C does not publish `/admin` on a Pay hostname. Phase D: delete the app. **3005 must remain One Login V2 break-glass** on One compose — killing Hub admin **frees** 3005 for One, which is a **success**. |
| **Cheat** | “Pay needs a superadmin.” That is One `is_platform_admin` / One admin SPA. Not Hub `:3005`. Not a new Pay password form. |

### 6.7 `apps/lazuar-developers` / 3002 / Caddy `/docs*` / GHCR `lazuar-hub-developers`

| | |
|--|--|
| **Keep until** | Pay has an integrator docs surface generated from **`packages/pay-spec`**, not Hub Scalar tiles that include LHDN and `/public/commerce`. |
| **Kill when** | Docs hostname in integrator README is Pay’s. Hub `/docs` dark. |
| **Cheat** | Point Scalar at Pay OpenAPI **and** Hub OpenAPI in one hub. Two products, two references (One already has `lazuar-reference` 5181). |

### 6.8 Local Caddy 9080 / `docker-compose.dev-proxy.yml` / `task proxy` / `lazuar-dev-caddy`

| | |
|--|--|
| **Keep until** | Someone is path-smoke-testing **Hub** museum. Not needed for whoami. 012/05: **keep down.** |
| **Kill when** | Default DX no longer documents 9080 as “like prod.” Prod is not path-based Hub. File can remain in git history. |
| **Cheat** | Rewrite `deploy/dev/Caddyfile` so `/api/*` → 8081 and `/` → 5178 **while still calling it Hub gateway**. That confuses 9080 forever. If Pay wants a local edge, new file, new port, new name. |

**When portal 3004 is removed from Caddy** (explicit, as required): remove `handle /portal*` from `deploy/dev/Caddyfile` when the local gateway is either deleted or no longer fronts Hub portal — i.e. when Phase A default is “no 9080,” or when a *new* Pay Caddyfile never included `/portal`. Remove `handle /portal*` from `deploy/prod/Caddyfile` when `hub.lazuar.com` no longer serves buyers (Phase C–D). Do **not** keep the handle and change upstream to 5179 as a “soft cutover.”

### 6.9 GHCR `lazuar-hub-*` / `docker-bake.hcl` / `.github/workflows/ghcr.yml` deploy to `/root/lazuar-hub-prod`

| | |
|--|--|
| **Keep until** | Rollback window (§9). Tagged images are the rollback artifact. |
| **Kill when** | Phase D. Stop pushing `latest` to Hub names (leave tags immutable). Stop rsync `deploy/prod`. Stop `wait_healthy hub-api`. New workflow deploys Pay images to a **Pay** directory, not `/root/lazuar-hub-prod`. |
| **Cheat** | Bake Pay into the same matrix and deploy both to one VPS sharing Caddy `hub.lazuar.com`. F20. |

### 6.10 `task tunnel:api` / `tunnel:fe` / `tunnel:cf` / hostname `pay-local.lazuar.dev` → **8090**

| | |
|--|--|
| **Keep until** | A Hub Billplz sandbox still needs hop A **and** Aura still owns 8080 on that laptop. That is a **Hub/Aura** concern, not Pay-as-Consumer-0. |
| **Kill when** | Pay’s public webhook base is Pay staging/prod, not Hub ngrok 8080 and not Hub 8090. Cloudflare ingress for `pay-local.lazuar.dev` either deleted or retargeted to **8081** **after** Hub-on-8090 stopped. OpenFGA keeps 8090. |
| **Cheat** | Leave `pay-local.lazuar.dev` → 8090 and tell people “that’s Pay.” |

### 6.11 `packages/api-spec` / `task gen` / `@repo/api-types-ts` / honesty allowlist / CI `contracts` job

| | |
|--|--|
| **Keep until** | Hub museum compiles. Ops/portal still exist. 012/04: do **not** hook `pay-spec` into `task gen`. |
| **Kill when** | Last Hub UI deleted. Pay clients use `@repo/pay-types-ts` (or equivalent) generated from `packages/pay-spec`. CI honesty is Pay Minimal maps vs `pay-spec`, not Hub allowlist archaeology. |
| **Cheat** | Import `/public/commerce` into `pay-spec` “so types exist.” Pay-spec README forbids it. |

### 6.12 `examples/hub-cashier-next` (Hub M2M)

| | |
|--|--|
| **Keep until** | A Pay sample exists that calls **Pay `/v1`** with One `lzr_sk_` (not Hub `sk_test_`) and verifies **Pay** webhooks. |
| **Kill when** | README no longer says `task dev` on 8080. Package renamed or replaced so “cashier” does not mean Hub. |
| **Cheat** | Change `LAZUAR_HUB_BASE_URL` to `http://localhost:8081/api/v1` (wrong prefix: Pay is `/v1`, not `/api/v1`) or to 8081 while still posting `/integrations/payments/checkouts`. |

### 6.13 Root `README.md` Getting Started / port table / demo accounts

| | |
|--|--|
| **Keep until** | Phase A is true and someone has rewritten the first screen. **This is an early kill** — a lying README recreates F1 every onboarding. |
| **Kill when** | Clone → One bootstrap + `task pay:dev` is the documented path. Hub ports in an “Archive / Hub museum” section only. Demo table is Ada, not founder. |
| **Cheat** | Add 8081 as a row **and** keep `task dev` as step 2. |

### 6.14 `apps/lazuar-docs` VitePress **5180** (Hub guides)

| | |
|--|--|
| **Keep until** | Pay docs exist (may live in this package rewritten, or in One docs, or a new origin). 5180 collision with One docs is already real. |
| **Kill when** | `task docs` does not start a second 5180. Hub-vs-DIY pages that teach Hub cashier are archived. |
| **Cheat** | Keep teaching `/public/commerce` as the door. |

### 6.15 Hub auth cookies and env (`Jwt__Secret`, `lazuar_auth`, `INTEGRATOR_PROVISION_SECRET`)

| | |
|--|--|
| **Keep until** | Hub process dies. |
| **Kill when** | Pay env has no `Jwt__Issuer=lazuar-api`. Provision hatch, if any, is One-shaped (`lzr_sk_` / One apps), not `X-Lazuar-Provision-Key` on Hub. |
| **Cheat** | Copy `AuthAndCorsExtensions` `OnMessageReceived` into `Lazuar.Pay`. IsolationTests should grow a ban on `lazuar_auth` if someone tries. |

### 6.16 `pnpm --filter lazuar-api dev` / turbo including Hub `dev`

| | |
|--|--|
| **Keep until** | Hub museum. |
| **Kill when** | Root `pnpm dev` does **not** start `lazuar-api`. Filter it out the way `@examples/*` is filtered — earlier if needed (Phase A DX). Focused Pay + merchant + checkout may remain. |
| **Cheat** | Leave turbo as-is and tell people “just don’t use it.” F9 will happen. |

### 6.17 One compose profile `api` (`container_name: lazuar-api`) — **not ours to delete**, but a kill-adjacent footgun

Pay cannot rename One’s container. Pay **can** refuse to ever ship `container_name: lazuar-api` on a Pay compose file. Kill criterion for **Pay’s** compose: that string is absent. Document: do not start One `--profile api` on a machine that still has Hub compose.

### 6.18 Checklist: “Hub compose profile goes”

The prompt’s phrase “when lazuar-api compose profile goes” maps to **this repo’s default `api` service** (not a Compose `profiles:` key — Hub **api is default**, frontends are `full`). Kill when:

1. `docker-compose.yml` `api` service is removed or replaced by focused Pay on **8081**.
2. `docker-compose.ghcr.yml` `api` no longer publishes 8080 as Hub.
3. `deploy/prod/docker-compose.yml` `api` / `hub-api` stopped (Phase D).
4. README does not say `docker compose up` = money API on 8080.

Until (1)–(4), the “profile” has not gone; it has only been ignored on a careful laptop.

---

## 7. What to keep from the old tree after replace vs delete

011/00 and 011/09 already split **judgment** from **folders**. Cutover must not throw away SST rounding because we hate MediatR, and must not keep nine DbContexts because tests are green.

### 7.1 Keep as **oracles** (read, cite, re-implement in the new host — do not ProjectReference)

| Artifact | Path | Why it is an oracle | What not to copy |
|----------|------|---------------------|------------------|
| **SST math** | `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` | Exclusive SST on the **unit**, then × seats. Fail closed: not registered / not type `02` / rate≤0 / net≤0 → type `06` amount 0. `MidpointRounding.AwayFromZero`. NP-MON-003. | Do not import `Modules.Commerce`. Do not keep dual-use tax columns. |
| **SST tests / failed-test papers** | `plans/010-failed-tests/01–03-*-sst.md`; module tests that pin `GrossBreakdown` | They document stub polarity and product type `06` vs `02`. Green Hub CI is not the product; the **judgment** is. | Do not keep `[Ignore]` sandbox as a shipping badge. |
| **Receipt ≠ tax invoice** | Billing document series, `RCPT-` vs `INV-`, 011/01, 011/00 | Number is never a UUID; missing = `PENDING`. Do not title Tax Invoice. VALID is a tax network’s word. | Homemade MyInvois factory, types 03–14, XAdES, consolidation module. |
| **QuestPDF notes** | `Modules/Billing/Infrastructure` PackageReference `QuestPDF`; generate-and-store handler | C# is fine at PDF. Steal **layout rules** (receipt, not tax invoice; QR if you still want). | R2 coupling, Guid-sliced “invoice numbers,” Hub SaaS PDF path that ignored merchant numbering. |
| **Wrap-rails** | Payments adapters; 011/01 honest matrix | Stripe/CHIP may auto-charge if vaulted; Billplz/Xendit/Razorpay-class = reminder + hosted link, never silent debit. Webhook idempotency `(tenant, provider, event_id)`. Empty body = 400. | Stripe Billing `subscription.updated` as SoT. Homemade FPX e-mandate. Five adapters on day one. |
| **Cookie vs Bearer honesty** | `AuthAndCorsExtensions`, `AuthEndpoints.IssueCookie`, `docs/001-gaps/03-api-auth-credentials.md` | Negative oracle: **do not** rebuild this on 8081. | The code itself. |
| **Hub public commerce P0s** | `PublicArrearsEndpoints` bare GUID; 008-evals / 009-bugs | Negative oracle: checkout update-payment must not be a raw GUID. | The routes. |
| **Isolation instinct** | workers `IgnoreQueryFilters` empty tenant; parked events | Negative oracle: one Pay DB, one tenant SoT = One org id, no module walls. | Outbox between folders in one process. |
| **Module tests as scenario lists** | `tests/Lazuar.ModuleTests`, Billing tests | Use as a **checklist of cases** (refund reverse once, dunning, seats × unit) when writing Pay tests. | Architecture tests that freeze MediatR / nine contexts. |
| **Plans 007–010** | feature inventory, evals, bugs, failed tests | Why we left; what money bugs cost. | Implementing 261–334 on Hub. |
| **006 sample fulfillment rule** | mark paid **only** after verified webhook; never on `success_url` | Still true on Pay `/v1`. | Hub provision hatch, `sk_test_` prefix, `/webhooks/hub/payments` path. |
| **CHIP/Stripe as HTTP** | adapters | Thin HTTP + signature verify. | Per-module Payments DbContext + Commerce dual-write. |

`SstTaxMath` in full (the thing to steal, not the namespace):

```csharp
// Exclusive SST on netAmount (one unit). Callers with seats pass unit net.
// NotApplicable "06"; ServiceTax "02". Fail closed if merchant is not SST-registered.
```

Re-home as `Lazuar.Pay` code (or a small Pay package) when money exists. **Do not** `ProjectReference` `Modules.Commerce.Application`. IsolationTests already ban `Modules.`, `BuildingBlocks`, `MediatR`, `lazuar-api`, `apps/lazuar-api`.

### 7.2 Keep in **git** but stop **shipping**

| Artifact | Why keep in history | When it leaves `HEAD` |
|----------|---------------------|------------------------|
| `apps/lazuar-api` | Legal/forensics; oracle browsing | Phase D delete or `archive/hub/` |
| Hub GHCR tags | Rollback (§9) | Stop `latest`; keep immutable sha tags |
| `issues/001–334` | Defect catalog | Remain as markdown; do not reopen as Hub PRs |
| ADRs 014–023 | Why CaaS pivot | Historical |
| `packages/lhdn-sdk-*` | If a **provider** extract ever happens | Not Pay v1 |

### 7.3 Delete (do not grow; remove when kill criteria fire)

| Artifact | Why delete |
|----------|------------|
| `Modules/One` | Homemade IdP. One sibling exists. |
| `Modules/Lhdn` + honesty VALID files as a **product** | Wrong extract (011/00). |
| `BuildingBlocks` + MediatR + per-module `DbContext` | Cathedral tax. New host is one csproj. |
| `lazuar-ops` / `lazuar-portal` / `lazuar-admin` | Replaced by 5178 / 5179 / One 5173. |
| Hub Caddy path map as **Pay prod** | Wrong topology. |
| Hub cookie JWT + `Jwt__Secret` in Pay env | NP-ONE-020 list does not include it. |
| `/public/commerce` in `pay-spec` | Forbidden by pay-spec README. |
| Dual-run 8090 Taskfile as **Pay DX** | OpenFGA owns 8090 next to One. |
| Root README Hub Getting Started | Recreates F1. |
| turbo `dev` including `lazuar-api` | F9. |

### 7.4 Do not “keep” by copying into Pay

- Nine migration trains.
- `INTEGRATOR_PROVISION_SECRET` as Pay’s public hatch without a One-shaped equivalent.
- Portal magic-link tokens whose subject is the wrong aggregate (009-bugs).
- TypeSpec honesty allowlists as a second product.
- `Apps never talk to vendors` as a hard rule that forces a Notify service (011/06).
- Mega-merge of One into Pay “to make a Linux kernel” (011/13).

Linux is the **room** (one Pay binary: money, mail, audit). Bezos is the **door** (`/v1`). One stays the other team.

---

## 8. Customers / integrators of Hub `/v1` and the sample cashier

Hub has **two** public-ish doors. Replace must not collapse them, and must not keep either as the long-term Pay door.

### 8.1 Three Hub surfaces integrators actually call

| Surface | Path prefix | Auth | Who | New Pay analogue |
|---------|-------------|------|-----|------------------|
| **Payments cashier (M2M)** | `POST /api/v1/integrations/payments/checkouts` | Hub `sk_…` Bearer + scopes | Aura, `examples/hub-cashier-next`, docs quickstart | **Pay `POST /v1/checkouts`** (fixture today; real money later) with One **`lzr_sk_`** or user JWT |
| **Commerce public** | `/public/commerce/*` | Often **none** or magic-link; tenant slug on some routes | Hub-hosted catalog / portal | **Pay hosted checkout origin 5179**, not these routes |
| **Hub One-module provision** | `POST /api/v1/one/integrations/workspaces/provision` | `X-Lazuar-Provision-Key` | Sample, Aura Connect | **One** `POST /tenants` + One API keys. Pay does not become a second provisioner |

`docs/payments-integration-quickstart.md` already tells integrators **not to mix** product lines. That honesty stays; the **Payments** row’s URL changes from Hub `/api/v1/integrations/payments/*` to **Pay `/v1`**.

### 8.2 Bezos door on **new Pay `/v1`**, not Hub `/public/commerce`

011/08: anything you will sell is a **versioned HTTP API**. No “just join the ledger table.” Your own UI is a client of that door. One is already the other team (Pay must not read One tables).

Consequences for replace:

1. **Merchant Vite 5178** calls Pay `:8081/v1/…` (and One for roster/invites), not Hub `/admin/commerce`.
2. **Checkout Vite 5179** calls Pay `/v1` (public/hosted session), not Hub `/public/commerce/checkout`.
3. **Sample cashier** becomes a client of Pay `/v1` (server-side `lzr_sk_`), webhook verify on a **Pay** signature, fulfillment still “webhook only, never `success_url`.”
4. **Aura** (first-party Hub cashier consumer) needs an explicit later program: stay on Hub until sunset, or migrate to Pay `/v1`. This paper does not pretend Aura is already a Pay client.
5. **Do not** keep `/public/commerce` as a compatibility shim on 8081. That is a strangler of the worst Hub P0s (bare GUID arrears). If a buyer link exists in the wild, sunset it on **Hub** or issue new Pay links — do not reimplement the GUID.

Path prefix honesty (012/04): Hub is **`/api/v1`**. Focused Pay is **`/v1`**. One is **`/api/v1`**. Integrator env vars must not reuse `LAZUAR_HUB_BASE_URL` for Pay. Prefer `PAY_API_URL=http://localhost:8081` (client appends `/v1/...`) or `http://localhost:8081/v1` with documented slash rules. Never `http://localhost:8081/api/v1` unless Pay deliberately maps that (it does **not** today).

### 8.3 Sample cashier cutover (concrete)

Today (`examples/hub-cashier-next/.env.example`):

```text
LAZUAR_HUB_BASE_URL=http://localhost:8080/api/v1
LAZUAR_SK_TEST_KEY=sk_test_replace_me
LAZUAR_WEBHOOK_SECRET=whsec_replace_me
NEXT_PUBLIC_APP_URL=http://127.0.0.1:3020
```

After replace (target shape, not this SHA):

```text
PAY_API_URL=http://localhost:8081          # /v1/checkouts
ONE_API_URL=http://localhost:8080/api/v1   # mint lzr_sk_ as a One human, not Hub provision
PAY_API_KEY=lzr_sk_…                       # not sk_test_
PAY_WEBHOOK_SECRET=…                       # Pay’s HMAC, not Hub whsec_ unless you keep the name as a generic
NEXT_PUBLIC_APP_URL=http://127.0.0.1:3020
```

Provision script today curls Hub `$HUB/one/integrations/workspaces/provision`. After replace: create One tenant (or use Ada’s), mint `lzr_sk_` with explicit scopes, register webhook on **Pay** (when P30/money exists). **Do not** implement Hub provision on 8081 to keep the sample unchanged.

Port **3020** can stay (no collision with One). Hub CORS lists 3020; Pay CORS today does **not**. When the sample calls 8081 from a **browser**, add 3020 **if** the browser talks to Pay; the current sample’s Hub calls are **server-side** `fetch` (no CORS). Keep it server-side.

### 8.4 Webhook receivers in the wild

| Receiver | Today | After replace |
|----------|-------|----------------|
| Sample `/webhooks/hub/payments` | Hub `payment.completed` HMAC | Pay event name/signature (paper 06). Path should drop `hub`. |
| Aura `/api/v1/webhooks/hub/payments` | Hub | Aura-side change; not a Pay silent dual-write |
| Billplz/Stripe → Hub `App__ApiBaseUrl/webhooks/payments/{gateway}/{tenantId}` | Hub public URL / `pay-local.lazuar.dev` | **Pay** public URL. Cut provider dashboard. Dual-posting to Hub and Pay **double-journals** if both live. |

Bezos: providers and second apps call **one** door. Dual-run of **webhooks** is not dual-run of **ports**; it is a money bug. Phase C must cut provider URLs in the same window as DNS, or Hub must ignore provider posts (fail closed).

### 8.5 First-party vs stranger

011/06: a platform exists when a second consumer integrates from **docs + SDK**, not by opening the database. First consumer is **you** (merchant Vite + checkout Vite + sample). Aura is a **second** consumer of **Hub** today. After replace, Aura is either a consumer of **Pay `/v1`** or a leftover Hub client. There is no world where Aura keeps calling `/public/commerce` and you still claim replace.

---

## 9. Rollback if the new stack fails

Rollback is **Hub on again, Pay off (or Pay still on 8081 for engineering)**, without violating One’s ownership of 8080 on laptops that still dogfood One.

### 9.1 What rollback is not

- Not “point Caddy `/api` back to Hub **and** leave Pay on 8080.” Pay never had 8080.
- Not “start Hub on 8080 while One is on 8080.” That is F1. Production Hub VPS is a **different machine** than One; local rollback on a **shared laptop** must **stop One API** or **stop Hub API** — never both.
- Not “ops `VITE_API_URL=8081` revert” — that env must never have been set.
- Not dual-writing ledger to Hub and Pay “until we’re sure.”

### 9.2 Rollback artifacts to **keep** until Phase D

| Artifact | Role in rollback |
|----------|------------------|
| GHCR `lazuar-hub-*:sha-…` immutable tags | `VERSION=sha-…` `remote-deploy.sh` on `/root/lazuar-hub-prod` |
| `deploy/prod/*` as last known Hub Caddy + compose | rsync still works if files remain on `main` or an `archive/hub-prod` tag |
| Neon / Hub `lazuar_mvp` backup | Paper 09. If production money was **only** on Hub, rollback is restore Hub DB + Hub process |
| Provider dashboard screenshots of webhook URLs | Flip Billplz/Stripe endpoints back to Hub `App__ApiBaseUrl` |
| DNS records for `hub.lazuar.com` | If Phase C pointed the **customer** hostname at Pay, lower TTL **before** cutover so rollback is not a 24h cache |

### 9.3 Local rollback (engineer laptop)

```text
# Stop focused Pay
# (kill task pay:dev / merchant / checkout)

# If you need Hub museum:
#   STOP One API first (free 8080)
#   STOP One postgres if you need 5432 for Hub (or leave One on 5432 and put Hub on a remap — not default)
#   task infra:up && task dev && task fe

# If you need One+Pay dogfood again:
#   STOP Hub (docker stop lazuar-api lazuar-db; no task dev)
#   One 8080 + Pay 8081
```

Never document “rollback = `task dev`” without the One-off step. 012/05 fingerprints first.

### 9.4 Staging rollback

Staging Pay DNS → previous Pay image **or** (if staging never had Hub) accept staging down. Do **not** deploy Hub compose onto Pay staging hostname.

If a separate Hub staging still exists, it can stay up as Hub. Rollback of **Pay staging** is not “turn Hub staging into Pay.”

### 9.5 Production rollback (Phase C failure)

Ordered:

1. **Stop provider webhooks into Pay** (or make Pay webhook handler no-op / 503) so you do not double-charge when Hub comes back.
2. **Point provider webhooks at Hub** again if Hub will take money.
3. **DNS / Caddy:** customer hostname back to Hub VPS (`hub.lazuar.com` stack) **or** static downtime page if Hub data was already abandoned (paper 09 greenfield — then rollback is “fix Pay,” not “revive Hub”).
4. **Deploy last good Hub `VERSION=sha-…`** via existing `ghcr.yml` / `remote-deploy.sh` if Hub VPS still has the files.
5. **Do not** start Hub on the **One** production host.

**Greenfield vs migrate** changes rollback:

| Data choice (paper 09) | If new Pay fails |
|------------------------|------------------|
| Greenfield (no Hub money in prod, or Hub never had real GMV) | Rollback = previous **Pay** image. Hub dark can stay dark. |
| Migrate (Hub had real merchants) | Rollback = Hub process + Hub DB backup **from before migrate**. Dual-write is forbidden; pick a cut timestamp. |

This paper does not choose migrate vs greenfield. It requires that rollback **runbook names the choice**.

### 9.6 Rollback vs dual-run

A rollback window where **both** Hub and Pay accept charges is not safety; it is F-money. Allowed dual-run in production is **read-only Hub** (docs, export) + **write Pay**, or **write Hub** + **Pay 8081 engineering-only**. Not both writers.

### 9.7 What to page on

Until Phase C, pages are Hub `/health` on `hub.lazuar.com` **and/or** Pay staging `/health`. After Phase C, page **Pay**. Do not page Hub Caddy `/health` that might be a leftover reverse_proxy to a stopped `api`.

---

## 10. Anti-goals

These are stop-ships, not taste.

### 10.1 Strangler via ops/portal `VITE_API_URL` / `NEXT_PUBLIC_API_URL` → 8081

**Why it looks attractive:** one PR, old UI, new host.

**Why it fails:** P60; ops cookie IdP; `@repo/api-types-ts` Hub paths; Pay CORS test forbids 3003; Pay `/v1` is not `/api/v1`; hundreds of Hub routes missing. You will “fix” it by implementing `POST /one/auth/login` on 8081 — the thing 011/02 forbids.

**Detect:** git grep `VITE_API_URL=http://localhost:8081`; CORS allowlist growing 3003–3005; `credentials: include` against Pay.

### 10.2 Sharing Hub cookie `lazuar_auth` / `lazuar_admin_auth` with One or Pay

Hub cookie is HMAC JWT issuer `lazuar-api`. One accepts Zitadel access_token or `lzr_sk_`. Pay forwards Bearer to One. Cookies are **not port-scoped** on localhost (012/05); a Lax cookie from 3003 *can* ride to 8080. One **ignores** it. A confused Pay middleware that reads `lazuar_auth` becomes a second IdP.

**Detect:** `OnMessageReceived` in `apps/lazuar-pay`; cookie names in Pay code; `Jwt__Secret` in Pay env.

### 10.3 Running both APIs on 8080

Hard bind or silent winner. `GET /health` lies. `One__BaseUrl` becomes Hub. C99 whoami is undefined.

**Detect:** `applicationUrl` 8080 on `Lazuar.Pay`; compose Pay service `8080:8080`; README “Pay is 8080 now that Hub is gone” **while One still exists**. After Hub is dead, **One still owns 8080**. Pay stays 8081 locally even in Phase D.

### 10.4 Shipping merchants to `:5173` or `:3005`

One admin is staff. Hub admin is Hub staff. Login V2 is break-glass. Merchant origin is **5178**.

### 10.5 Focused Pay binding 8080 “because Hub is gone”

Hub gone ≠ 8080 free for Pay. One is the identity plane. 012 lock: Pay never binds 8080.

### 10.6 Default compose still starting Hub `api` after README says “new Pay”

Muscle memory `docker compose up` is F2. Kill the service or change the default.

### 10.7 Caddy 9080 / `hub.lazuar.com` path map as the Pay production shape

`/` = ops, `/portal` = portal, `/api` = whoever is `api:8080` is **Hub’s** product packaging. Pay is separate origins + One.

### 10.8 `pnpm dev` turbo pile-up as “full stack”

F9. Root turbo must stop starting `lazuar-api` once Phase A is default.

### 10.9 Importing `/public/commerce` or Hub `docs-one.tsp` into `packages/pay-spec`

012/04. Contract strangler.

### 10.10 Hub `sk_test_` / `whsec_` as Pay machine credentials

One keys are `lzr_sk_`. Different pepper, different table. Sample `.env.example` must not be copied into Pay.

### 10.11 Dual-run Hub 8090 next to One OpenFGA

Aura folklore. Not Consumer-0.

### 10.12 Mega-merge One into Pay, or five services (Notify/Media/Audit) as the replace plan

011/06, 07, 13, 14. Replace Hub with **one Pay binary + existing One**, not with a mesh.

### 10.13 Implementing Hub issues 261–334 on the cathedral as a gate to delete it

011/09: the 260 fixes made it honest enough to **leave**. Parity is not the bar (paper 01).

### 10.14 Buyer as Zitadel human; Pay password form; second org table

011/03 fail locks. Still true at cutover.

### 10.15 Calling Hub `/public/commerce` from new checkout “for compatibility”

Bezos door is Pay `/v1`. Compatibility shims reimport P0 GUID arrears.

### 10.16 Pay holding Zitadel PAT / login-client PAT / OpenFGA admin “so staging is easier”

`NP-ONE-020`. 012/02 fail mode 10.3.

### 10.17 Renaming Hub images to `lazuar-pay-*` without changing the process

OCI name is not replace. If the Dockerfile is still `apps/lazuar-api`, you shipped Hub.

---

## 11. Open questions

These are **not** invitations to strangler. They are decisions later papers / humans must close before Phase C.

1. **Public hostnames.** What DNS names do Pay API, merchant SPA, and checkout SPA get? `hub.lazuar.com` path-routing is the wrong shape. Candidates (`pay.lazuar.com`, `api.pay.lazuar.com`, `checkout.lazuar.com`, …) are **not** in this repo. Who owns TLS?

2. **Aura.** Is Aura migrating to Pay `/v1` in the same program as Hub dark, or is Aura a leftover Hub customer with its own sunset? Dual writers are forbidden (§9.6).

3. **Migrate vs greenfield** (paper 09). Rollback runbook depends on it. Org ids: One tenant id is Pay `org_id` — Hub workspace Guid mapping is a data question, not a port question.

4. **Pay Postgres in compose.** Confirm **5435** as the published port (this paper’s recommendation). Database name. Image pin (16 vs 17 vs 18). Staging uses Neon-like hosted PG — then 5435 is **local only**, still useful so laptops do not fight One.

5. **One compose `container_name: lazuar-api`.** Ask One to rename if Pay ever containerizes an API on the same engine. Pay should not take that name.

6. **Docs home.** VitePress 5180 collision. Does Pay keep `apps/lazuar-docs` rewritten, fold integrator guides into One docs, or a third origin? Until decided, **do not** run both docs:dev.

7. **Scalar / developers app.** New Pay OpenAPI from `pay-spec` — where is it hosted? Not Hub `/docs` forever.

8. **`pay-local.lazuar.dev`.** Retire vs retarget to 8081. OpenFGA must keep 8090. Cloudflare named tunnel currently shared with Aura (`aura-025-fulfillment`).

9. **GHCR namespace.** New images under `ghcr.io/proxeon/lazuar-pay*` vs keep `proxeon` Hub names. Labels still say `github.com/proxeon/lazuar-hub`.

10. **When root README is rewritten.** Recommend: **before** S1 money, because F1 onboarding is cheaper to prevent than to debug. Owner?

11. **turbo `pnpm dev` filter date.** When to `--filter=!lazuar-api --filter=!lazuar-ops --filter=!lazuar-portal --filter=!lazuar-admin`? Recommend Phase A default DX.

12. **Hub VPS reuse.** Does `/root/lazuar-hub-prod` become a Pay VPS (new compose, new Caddyfile, new container names) or is Pay a new machine? Reusing the VPS without rewriting Caddy is F20.

13. **Sample package name.** `hub-cashier-next` after replace is a lie. Rename vs new `examples/pay-cashier-next`.

14. **CI Postgres 5432.** Hub CI job publishes 5432. When Pay CI adds a DB, do not also publish 5432 on the same GitHub job matrix as Hub if both run. Prefer Pay CI **without** Hub job, or Pay service on **5435**.

15. **VIEWER / NP-ONE-021.** Still todo. Not a cutover blocker for turning Hub **off**, but a dogfood blocker for “MEMBER sees ops, VIEWER cannot charge.” One membership is `owner|admin|member` only.

16. **P10 OIDC.** Merchant 5178 exists; redirects not on One app or login `REDIRECT_ALLOWLIST`. Phase B enter needs this. Checkout 5179 must **not** be added as a Zitadel SPA.

17. **Webhook dual-cut.** Provider dashboards, sample, Aura — single cut window. Who runs the checklist on cut day?

18. **Legal/ops retention** of Hub DB and GHCR. Dark vs delete lag.

19. **Taskfile `task dev` alias.** After replace, should `task dev` mean `pay:dev` (breaking Hub muscle memory on purpose) or stay removed so tab-complete cannot start Hub? Recommendation: **do not** silently retarget `task dev` at 8081 while Hub code still exists; that makes logs unreadable. After Hub delete, `task dev` → `pay:dev` is reasonable.

20. **Production Caddy for Pay.** One site vs three (API, merchant, checkout). Checkout and merchant are **different** origins by product law (buyers ≠ One humans). Do not collapse them on one `handle` with Hub-style `/` vs `/portal` unless there is a new, documented path map that is **not** ops-at-root.

---

## 12. Evidence index (paths opened)

### Pay repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`) at `6f866ff0`

- `plans/013-prods/README.md` — program index; this slice.
- `plans/012-one-to-pay/05-local-topology.md` — port bible, dual-run matrix, fingerprints, Caddy 9080 mis-route.
- `plans/012-one-to-pay/02-one-authn-tokens.md` — 8080 collision, 3005, 5432, cookies vs Bearer.
- `plans/012-one-to-pay/04-pay-spec-contract.md` — `/v1` vs `/api/v1`; P60 contract argument.
- `plans/012-one-to-pay/10-dogfood-and-tests.md` — connected vs S1; anti-goals.
- `plans/012-one-to-pay/checklists/{p60-old-frontends,p10-spa-oidc,c99-connected-done,decisions}.md`
- `plans/011-new-lazuar-pay/{00-why-leave,01-product,02-one-integration,03-first-slice,06-platforms,07-separate-vs-one-binary,08-bezos-door,09-old-pay,11-checklist,13-monolith-vs-services,14-google-aws-microsoft}.md`
- `plans/003-dev-caddy/{00-analysis,01-done}.md`
- `plans/006-sample/{03-sample-app-architecture,08-docs-information-architecture,09-hub-vs-diy-docs}.md`
- `Taskfile.yml` — `pay:*` vs `api:*` vs `infra:*` vs `proxy` vs `tunnel:*` vs `docker:*`
- `docker-compose.yml`, `docker-compose.ghcr.yml`, `docker-compose.dev-proxy.yml`, `docker-bake.hcl`
- `deploy/dev/{Caddyfile,README.md}`, `deploy/prod/{Caddyfile,docker-compose.yml,env.example,README.md}`
- `mprocs-dev.yaml`, `scripts/remote-deploy.sh`, `.github/workflows/{ci.yml,ghcr.yml}`
- Root `README.md`, `package.json`, `pnpm-workspace.yaml`, `turbo.json`
- `apps/lazuar-api` launchSettings; `appsettings.Development.json` CORS + demo users + 5432
- `apps/lazuar-ops/{package.json,vite.config.ts,src/lib/api-client.ts,Dockerfile}`
- `apps/lazuar-portal/package.json` + `NEXT_PUBLIC_API_URL` / `API_URL` call sites
- `apps/lazuar-admin/{package.json,vite.config.ts}`
- `apps/lazuar-developers/package.json`, `apps/lazuar-docs/package.json`
- `apps/lazuar-pay/{README.md,.env.example,src/Lazuar.Pay/{Program.cs,One/*,Checkouts/*},tests/Lazuar.Pay.Tests/{CorsTests,IsolationTests}.cs}`
- `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout` README / vite / `.env.example` / `App.tsx`
- `packages/pay-spec/main.tsp`
- `examples/{README.md,hub-cashier-next/{README.md,.env.example,lib/env.ts}}`
- `docs/payments-integration-quickstart.md`
- `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs`
- `apps/lazuar-api/Modules/Billing/Infrastructure/Modules.Billing.Infrastructure.csproj` (QuestPDF)

### One repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`) at `0f79fe4`

- `docker-compose.yml` — `name: lazuar-one`, `lazuar-postgres` 5432, `zitadel-login` 3005, `openfga` 8090, profile `api` `container_name: lazuar-api`, network `lazuar-one-network`
- `deploy/dev/README.md` — 5432 often taken by lazuar-pay → `POSTGRES_PUBLISHED_PORT=5433`

---

## 13. What “done” looks like for this slice (analysis bar)

This paper is done if a later implementer can, without re-deriving topology:

1. List every Hub surface that still claims to be Pay (API, four UIs, three compose files, two Caddyfiles, GHCR, tunnels, docs, sample).
2. Bind **One on 8080** and **Pay on 8081** (and later Pay DB on **5435**) without Hub 8080/5432/3005/8090.
3. Refuse strangler (`VITE_API_URL` → 8081), refuse shared Hub cookie, refuse both APIs on 8080, refuse merchants on 5173/3005.
4. Walk Phase A → D and know **when** ops may be deleted, when Hub `api` compose goes, when portal `/portal*` leaves Caddy.
5. Steal `SstTaxMath` / QuestPDF / wrap-rails / tests-as-oracles without copying the cathedral.
6. Point integrators at **Pay `/v1`**, not Hub `/public/commerce`.
7. Roll back Hub **on a Hub VPS** without stealing One’s 8080.

Implementation of compose swaps, DNS, and deletes is a **later program** (013 checklists, not this file). Do not flip 011/11 cells from this paper. Do not edit `Taskfile.yml` from this paper.

**Replace means the new stack takes the job and the old stack is turned off.** Anything else is dual-run folklore or a strangler.
