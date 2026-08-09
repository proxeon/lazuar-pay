# 003 — Dev Caddy + pinned ports (analysis & implement plan)

**Status:** Analysis only — **do not implement from this file alone without following the checklist**  
**Date:** 2026-08-09  
**Branch:** `chore/dev-caddy-and-pinned-ports`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Goal:** (1) Pin Vite ports with `strictPort` so apps never steal each other’s ports; (2) keep Next on fixed `-p`; (3) add a local Caddy gateway on `:9080` with prod-like path routing.

---

## 1. Decisions locked

| App | Stack | Dev port | Prod public path | Local gateway path (`:9080`) | Base-path env (when path-routed) |
|-----|-------|----------|------------------|------------------------------|----------------------------------|
| `lazuar-api` | .NET | **8080** | `/api/*`, `/health` | same | n/a |
| `lazuar-developers` | Next | **3002** | `/docs` | `/docs*` | `NEXT_BASE_PATH=/docs` |
| `lazuar-ops` | Vite | **3003** | `/` | `/` (catch-all) | unset / `/` |
| `lazuar-portal` | Next | **3004** | `/portal` | `/portal*` | `NEXT_BASE_PATH=/portal` |
| `lazuar-admin` | Vite | **3005** | `/admin` | `/admin*` (`handle_path`) | `VITE_BASE_PATH=/admin/` |
| **Caddy edge** | Caddy | **9080** | n/a (prod is 80/443) | `http://localhost:9080` | n/a |

**Why 9080:** unprivileged, no collision with macOS AirPlay (5000) or common alt HTTP (8080 already API). Prod stays on 80/443 via `deploy/prod/Caddyfile` — **untouched**.

**Design choice for this PR (recommended):** Path-based gateway matching prod + enable existing basePath envs in mprocs. Do **not** invent host-based `*.localhost` routing in v1.

---

## 2. Problem statement (current state)

### 2.1 Port drift (the pain)

| App | Port pin today | Strict? | Risk |
|-----|----------------|---------|------|
| `lazuar-ops` | CLI only: `vite --port=3003 --host=0.0.0.0` | **No** | If CLI flag missing or port busy, Vite picks next free port (often **3004/3005**) → “wrong app” confusion |
| `lazuar-admin` | CLI only: `vite --port=3005 --host=0.0.0.0` | **No** | Same |
| `lazuar-portal` | `next dev -p 3004` | Yes (Next exits if busy) | OK |
| `lazuar-developers` | `next dev -p 3002` | Yes | OK |
| `lazuar-api` | `launchSettings.json` → `http://localhost:8080` | Yes | OK |

Vite configs today have **no** `server.port` / `server.strictPort`:

- [`apps/lazuar-ops/vite.config.ts`](../../apps/lazuar-ops/vite.config.ts) — only `base: process.env.VITE_BASE_PATH || "/"`
- [`apps/lazuar-admin/vite.config.ts`](../../apps/lazuar-admin/vite.config.ts) — only `base: process.env.VITE_BASE_PATH || "/"`

### 2.2 Prod path routing already exists; local does not

[`deploy/prod/Caddyfile`](../../deploy/prod/Caddyfile):

```
/health, /api/*  → api:8080
/portal*         → portal:3000   (Next basePath=/portal)
/docs*           → developers:3000 (Next basePath=/docs)
/admin/*         → superadmin:3000 via handle_path (Vite base=/admin/)
/                → ops:3000
```

Locally, each app is reached only on its own port. No single edge URL for smoke-testing path layout.

### 2.3 BasePath support already in code (no app rewrite needed)

| App | Config | Already reads |
|-----|--------|---------------|
| portal | `next.config.ts` | `basePath: process.env.NEXT_BASE_PATH \|\| ""` |
| developers | `next.config.ts` | `basePath: process.env.NEXT_BASE_PATH \|\| ""` |
| admin | `vite.config.ts` + `main.tsx` `BrowserRouter basename` | `VITE_BASE_PATH` → `import.meta.env.BASE_URL` |
| ops | same pattern | defaults `/` |

Dockerfiles / `docker-bake.hcl` already bake these for prod images. Local mprocs currently does **not** set them → local Next/Vite serve at `/` on their ports.

### 2.4 CORS will block browser calls from the gateway origin

FE clients default to absolute API host:

- ops/admin: `VITE_API_URL || "http://localhost:8080/api/v1"`
- portal: `NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1"`

`App:CorsOrigins` in `appsettings.json` + `appsettings.Development.json` lists 3000–3005, 3020/3021, 8080/8090 — **no** `http://localhost:9080`.

Program.cs: `WithOrigins(...).AllowCredentials()` — exact origin match required.

---

## 3. Non-goals (do **not** do in this PR)

- Edit [`deploy/prod/Caddyfile`](../../deploy/prod/Caddyfile) or prod compose routing
- Rename apps / GHCR images / compose service keys
- Force all FE API clients to relative `/api/v1` (optional follow-up)
- Change `App:ClientUrl` / checkout return URLs globally (document only unless broken in smoke)
- Host-header multi-site (`portal.localhost`) routing
- VitePress `lazuar-docs` (port 5180) behind Caddy
- HTTPS / local TLS for Caddy
- Dockerizing the local Caddy edge (host `caddy` binary is enough)
- Full HMR perfection as a merge gate (document + optional env; direct ports remain valid)

---

## 4. Recommended design (implementer must follow)

### A. Pin Vite in config (source of truth)

**Files:**

- `apps/lazuar-ops/vite.config.ts`
- `apps/lazuar-admin/vite.config.ts`

**Add `server` block:**

```ts
// ops
server: {
  host: true, // 0.0.0.0 — matches current --host=0.0.0.0
  port: 3003,
  strictPort: true, // fail loudly if 3003 is taken — never steal 3004/3005
},

// admin
server: {
  host: true,
  port: 3005,
  strictPort: true,
},
```

Keep existing `base: process.env.VITE_BASE_PATH || "/"`.

**Optional HMR (nice-to-have, not blocking):**

```ts
// Only when developing primarily through the gateway
// server.hmr = process.env.VITE_DEV_CADDY === "1"
//   ? { clientPort: 9080 }
//   : undefined;
```

Document: if HMR is flaky through `:9080`, use direct app ports for UI work; use gateway for path smoke tests.

### B. package.json scripts (dual pin or simplify)

**Current:**

```json
// ops
"dev": "vite --port=3003 --host=0.0.0.0"
// admin
"dev": "vite --port=3005 --host=0.0.0.0"
```

**Recommendation:** keep CLI flags as dual pin for visibility in `package.json`, **or** simplify to `"dev": "vite"` once config owns port/host. Prefer **dual pin** in this PR (lowest surprise; config is the strictness guarantee).

Do **not** remove Next `-p`:

```json
// developers — unchanged
"dev": "next dev -p 3002"
// portal — unchanged
"dev": "next dev -p 3004"
```

Next already fails if the port is in use; no `strictPort` equivalent needed.

### C. Local Caddyfile (new)

**Create:** `deploy/dev/Caddyfile`

Sketch (mirror prod structure; upstreams = localhost pinned ports):

```caddyfile
# Lazuar Hub — local dev gateway (prod-like path map)
# Listen: http://localhost:9080
# Requires FE/API processes on fixed ports (see README / mprocs).
# Admin: handle_path strips /admin so Vite base=/admin/ asset URLs resolve
#        the same way as deploy/prod/Caddyfile.
# Portal/docs: path prefix preserved (Next basePath).
# Ops: catch-all at /.

{
	# optional: admin off for less noise
	# auto_https off
}

:9080 {
	encode gzip

	handle /health {
		reverse_proxy 127.0.0.1:8080
	}

	handle /api/* {
		reverse_proxy 127.0.0.1:8080
	}

	# Portal (Next basePath=/portal)
	handle /portal* {
		reverse_proxy 127.0.0.1:3004
	}

	# Developer docs (Next basePath=/docs)
	handle /docs* {
		reverse_proxy 127.0.0.1:3002
	}

	# Superadmin (Vite base=/admin/) — strip prefix like prod
	@adminExact path /admin
	redir @adminExact /admin/ permanent
	handle_path /admin/* {
		reverse_proxy 127.0.0.1:3005
	}

	# Ops creator console at /
	handle {
		reverse_proxy 127.0.0.1:3003
	}
}
```

**Notes for implementer:**

1. Use `127.0.0.1` (not Docker service names) — local hybrid stack, not compose network.
2. Match prod’s `handle_path` for admin so `VITE_BASE_PATH=/admin/` works without inventing a second routing model.
3. Do **not** put this under `deploy/prod/`.
4. Optional `deploy/dev/README.md` is **not required** if root README covers usage (prefer README only to avoid doc sprawl).

### D. mprocs — base paths + optional Caddy proc

**File:** `mprocs-dev.yaml`

**Recommended shape:**

```yaml
# mprocs-dev.yaml
procs:
  lazuar-developers:
    shell: cd apps/lazuar-developers && pnpm dev
    env:
      NEXT_BASE_PATH: /docs
    autostart: true
  lazuar-ops:
    shell: cd apps/lazuar-ops && pnpm dev
    # base stays /
    autostart: true
  lazuar-admin:
    shell: cd apps/lazuar-admin && pnpm dev
    env:
      VITE_BASE_PATH: /admin/
    autostart: true
  lazuar-portal:
    shell: cd apps/lazuar-portal && pnpm dev
    env:
      NEXT_BASE_PATH: /portal
    autostart: true
  caddy:
    shell: task proxy
    autostart: false   # opt-in; requires `caddy` on PATH
  ngrok-api-tunnel:
    shell: task tunnel:api
    autostart: false
  ngrok-fe-tunnel:
    shell: task tunnel:fe
    autostart: false
```

**DX impact (accept for this PR):** after basePath envs land, **direct** URLs gain prefixes:

| App | Direct (after change) | Via gateway |
|-----|----------------------|-------------|
| developers | `http://localhost:3002/docs` | `http://localhost:9080/docs` |
| ops | `http://localhost:3003/` | `http://localhost:9080/` |
| portal | `http://localhost:3004/portal` | `http://localhost:9080/portal` |
| admin | `http://localhost:3005/admin/` | `http://localhost:9080/admin/` |

This matches Docker healthchecks (`/portal`, `/docs`) and prod paths. Document clearly in README.

**mprocs `env` support:** confirm mprocs version supports per-proc `env:` (widely supported). If not, wrap shells:

```yaml
shell: cd apps/lazuar-portal && NEXT_BASE_PATH=/portal pnpm dev
```

Prefer shell-prefix form if YAML env is uncertain — zero dependency on mprocs schema.

### E. Taskfile — `proxy` task

**File:** `Taskfile.yml`

Add near `fe:` / tunnel section:

```yaml
  proxy:
    desc: Local Caddy gateway on :9080 (prod-like paths → pinned FE/API ports). Requires caddy on PATH.
    cmds:
      - |
        if ! command -v caddy >/dev/null 2>&1; then
          echo "caddy not found. Install: brew install caddy"
          exit 1
        fi
        caddy run --config deploy/dev/Caddyfile --adapter caddyfile
```

Optional companion:

```yaml
  proxy:validate:
    desc: Validate deploy/dev/Caddyfile syntax
    cmds:
      - caddy validate --config deploy/dev/Caddyfile --adapter caddyfile
```

Do **not** change `task fe` to always start Caddy (opt-in keeps lightweight FE-only workflows).

### F. CORS — add gateway origin

**Files (both, keep in sync):**

- `apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json`
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` (local defaults; prod overrides via `App__CorsOrigins`)

Append: `http://localhost:9080`

Example Development fragment:

```json
"CorsOrigins": "http://localhost:3000,...,http://localhost:3005,...,http://localhost:9080"
```

**Do not** remove legacy 3000/3001/3020/3021 entries in this PR (harmless; out of scope cleanup).

**Optional (not required):** set FE env when using gateway for same-origin API:

```bash
VITE_API_URL=http://localhost:9080/api/v1
NEXT_PUBLIC_API_URL=http://localhost:9080/api/v1
```

With CORS fixed, absolute `http://localhost:8080` continues to work from the `:9080` page origin. Prefer CORS-only for minimal change; mention same-origin env as optional follow-up.

### G. README

**File:** `README.md` — extend “Standardized Port Mapping” / Getting Started.

Add:

1. Row for Caddy gateway `9080`
2. Path map table (mirror prod)
3. Note that mprocs sets basePath envs → direct URLs include `/portal`, `/docs`, `/admin/`
4. How to run proxy: `task proxy` or enable `caddy` proc in mprocs
5. Prerequisite: `brew install caddy` (macOS)
6. Failure mode: if Vite prints “Port 3003 is in use” → kill the other process (expected with `strictPort`)

Example table addition:

| Entry | Port | URL |
|-------|------|-----|
| Gateway (optional) | 9080 | `http://localhost:9080` → `/`, `/portal`, `/docs`, `/admin/`, `/api/*` |

### H. Out of scope file guardrails

| Path | Action |
|------|--------|
| `deploy/prod/Caddyfile` | **Do not modify** |
| `deploy/prod/docker-compose.yml` | **Do not modify** |
| `deploy/prod/env.example` | **Do not modify** |
| App renames / package names | **Do not modify** |

---

## 5. Exact file edit checklist

| # | File | Action |
|---|------|--------|
| 1 | `apps/lazuar-ops/vite.config.ts` | Add `server: { host: true, port: 3003, strictPort: true }` |
| 2 | `apps/lazuar-admin/vite.config.ts` | Add `server: { host: true, port: 3005, strictPort: true }` |
| 3 | `apps/lazuar-ops/package.json` | Keep or dual-pin `dev` script (see §4.B) |
| 4 | `apps/lazuar-admin/package.json` | Keep or dual-pin `dev` script |
| 5 | `apps/lazuar-portal/package.json` | **No change** (`next dev -p 3004`) |
| 6 | `apps/lazuar-developers/package.json` | **No change** (`next dev -p 3002`) |
| 7 | `deploy/dev/Caddyfile` | **Create** (§4.C sketch) |
| 8 | `mprocs-dev.yaml` | Set basePath envs; optional `caddy` proc `autostart: false` |
| 9 | `Taskfile.yml` | Add `proxy` (+ optional `proxy:validate`) |
| 10 | `apps/lazuar-api/.../appsettings.Development.json` | Append `http://localhost:9080` to `App:CorsOrigins` |
| 11 | `apps/lazuar-api/.../appsettings.json` | Same CORS append |
| 12 | `README.md` | Ports + gateway + basePath direct URLs + `task proxy` |

**No changes expected:** Next `next.config.ts` files (already env-driven), admin/ops `main.tsx` basename logic, Dockerfiles, bake, prod deploy.

---

## 6. Implementation order

1. **Vite pin** (ops + admin configs) — unblocks “wrong port” immediately; can merge alone if needed.
2. **CORS** add `9080`.
3. **mprocs basePath envs** — align local apps with prod path prefixes.
4. **`deploy/dev/Caddyfile` + `task proxy`** — gateway.
5. **Optional caddy proc** in mprocs.
6. **README**.
7. Smoke verification (§7).

---

## 7. Verification / acceptance

### 7.1 strictPort

```bash
# Terminal A
cd apps/lazuar-ops && pnpm dev
# Terminal B
cd apps/lazuar-ops && pnpm dev
# Expect: error that port 3003 is in use / strictPort failure — NOT silent bind to 3004
```

Repeat conceptually for admin on 3005.

### 7.2 Next still fixed

```bash
# busy 3004 → next dev fails (existing behavior)
```

### 7.3 Gateway paths (API + all FEs running)

```bash
task proxy   # or mprocs caddy proc

curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/health          # 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/api/v1/…        # whatever public route exists / 401 ok
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/                 # ops HTML
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/portal           # portal
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/docs             # developers
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:9080/admin/           # admin
```

Browser smoke:

- Open `http://localhost:9080/` → ops loads; assets 200.
- Open `http://localhost:9080/admin/` → admin loads; no infinite redirect; JS chunks 200.
- Open `http://localhost:9080/portal` → portal loads.
- Open `http://localhost:9080/docs` → Scalar/docs load.
- From ops/admin/portal page origin `http://localhost:9080`, a credentialed API call to `http://localhost:8080` does **not** CORS-fail (check Network tab).

### 7.4 Direct ports still usable

| URL | Expect |
|-----|--------|
| `http://localhost:3003/` | ops |
| `http://localhost:3004/portal` | portal (not bare `/` once `NEXT_BASE_PATH` set) |
| `http://localhost:3002/docs` | developers |
| `http://localhost:3005/admin/` | admin |
| `http://localhost:8080/health` | API |

### 7.5 Prod untouched

```bash
git diff -- deploy/prod/
# empty
```

---

## 8. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| `handle_path /admin/*` vs Vite `base=/admin/` mismatch | Copy prod pattern; smoke asset requests under `/admin/assets/*` |
| Next basePath changes bookmarks (`localhost:3004` alone 404) | README table; mprocs always sets env so behavior is consistent |
| HMR websocket broken via Caddy | Optional `VITE_DEV_CADDY` hmr clientPort; else use direct ports for day-to-day UI |
| `caddy` not installed | Task prints install hint; proc `autostart: false` |
| Cookie / magic-link `App:ClientUrl` still `http://localhost:3004` without `/portal` | Follow-up if checkout emails break; out of scope unless smoke fails |
| CORS only on Development but someone runs Production locally | Also update `appsettings.json` defaults; real prod uses env override `App__CorsOrigins=https://hub.lazuar.com` |
| mprocs `env:` unsupported | Fall back to `ENV=value pnpm dev` in shell |

---

## 9. Follow-ups (explicitly later)

1. Same-origin API via gateway: default `VITE_API_URL` / `NEXT_PUBLIC_API_URL` to `http://localhost:9080/api/v1` when using proxy.
2. Align `App:ClientUrl` with `http://localhost:9080/portal` for local checkout/magic links behind gateway.
3. Host-based local aliases if path basePath becomes painful for a specific app.
4. Prune dead CORS origins (3000, 3001, 3020, 3021).
5. Document Windows Caddy install (winget/choco) if contributors need it.

---

## 10. Summary for implementer

**Minimal PR that solves the actual bug:**

1. `strictPort` + fixed `server.port` on ops/admin Vite configs.  
2. CORS `http://localhost:9080`.  
3. `deploy/dev/Caddyfile` + `task proxy`.  
4. mprocs basePath envs so path proxy works for portal/docs/admin.  
5. README.

**Do not** touch prod Caddy/compose. **Do not** rename apps. Prefer shell-inline env if mprocs schema is ambiguous. Dual-pin Vite CLI flags is fine.
