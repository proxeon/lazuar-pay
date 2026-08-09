# Local dev gateway (Caddy)

Prod-like path routing on a single local origin: **http://localhost:9080**

Mirrors [`../prod/Caddyfile`](../prod/Caddyfile) with upstreams on pinned host ports (not Docker service names).

## Prerequisites

```bash
# macOS
brew install caddy
```

Also run the usual stack so upstreams are listening:

| Upstream | Port | Role |
|----------|------|------|
| API | 8080 | `/health`, `/api/*` |
| developers | 3002 | `/docs*` |
| ops | 3003 | `/` (catch-all) |
| portal | 3004 | `/portal*` |
| admin | 3005 | `/admin/*` |

```bash
task infra:up
task dev          # API
task fe           # frontends via mprocs (sets basePath envs)
```

## Run the gateway

```bash
task proxy
# equivalent:
caddy run --config deploy/dev/Caddyfile --adapter caddyfile
```

Or enable the optional `caddy` proc in `mprocs-dev.yaml` (autostart is off by default).

Validate config:

```bash
task proxy:validate
```

## Path map

| Path | Upstream |
|------|----------|
| `/health`, `/api/*` | `127.0.0.1:8080` |
| `/portal*` | `127.0.0.1:3004` |
| `/docs*` | `127.0.0.1:3002` |
| `/admin`, `/admin/*` | `127.0.0.1:3005` (`handle_path` strips `/admin`) |
| `/` | `127.0.0.1:3003` (ops) |

`mprocs-dev.yaml` sets `NEXT_BASE_PATH` / `VITE_BASE_PATH` so path prefixes match prod. Direct app ports then use those prefixes too (e.g. `http://localhost:3004/portal`).

Plain `pnpm dev` outside mprocs leaves base paths unset unless you export them yourself.
