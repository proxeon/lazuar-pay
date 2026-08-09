# 003 — Dev Caddy + pinned ports (done)

**Date:** 2026-08-09  
**Branch:** `chore/dev-caddy-and-pinned-ports`  
**Status:** Implemented

## Summary

Pinned Vite dev ports with `strictPort`, added a local Caddy gateway on `:9080` with prod-like path routing, wired mprocs basePath envs for path-proxied apps, and opened CORS for the gateway origin.

## Changes

| File | Change |
|------|--------|
| `apps/lazuar-ops/vite.config.ts` | `server: { host: true, port: 3003, strictPort: true }` |
| `apps/lazuar-admin/vite.config.ts` | `server: { host: true, port: 3005, strictPort: true }` |
| `apps/lazuar-ops/package.json` | Unchanged — dual pin via CLI `--port=3003 --host=0.0.0.0` |
| `apps/lazuar-admin/package.json` | Unchanged — dual pin via CLI `--port=3005 --host=0.0.0.0` |
| `deploy/dev/Caddyfile` | **New** — `:9080` → localhost 8080/3002–3005 path map |
| `deploy/dev/README.md` | **New** — install caddy, `task proxy`, path table |
| `Taskfile.yml` | `proxy` + `proxy:validate` tasks |
| `mprocs-dev.yaml` | basePath shell envs for portal/docs/admin; optional `caddy` proc (`autostart: false`) |
| `apps/lazuar-api/.../appsettings.Development.json` | CORS `http://localhost:9080` |
| `apps/lazuar-api/.../appsettings.json` | CORS `http://localhost:9080` |
| `README.md` | Gateway row, path map, mprocs basePath direct URLs, strictPort note |

## Untouched (as planned)

- `deploy/prod/Caddyfile` and prod compose
- Next `package.json` `-p` pins
- App renames / Dockerfiles / bake

## How to use

```bash
task infra:up && task dev   # API :8080
task fe                     # frontends (basePath envs set)
task proxy                  # optional gateway :9080  (brew install caddy)
```

- Gateway: `http://localhost:9080/` · `/portal` · `/docs` · `/admin/` · `/api/*`
- Direct with mprocs: `3002/docs`, `3003/`, `3004/portal`, `3005/admin/`
- Plain `pnpm dev` outside mprocs: no basePath unless you export it

## Verification notes

- `caddy validate` / `task proxy:validate` — run if caddy is installed
- `git diff -- deploy/prod/` — empty
- Vite busy-port: expect strictPort failure, not silent bind to next free port
