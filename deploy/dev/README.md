# Local dev gateway (Caddy via Docker)

Prod-like path routing on a single local origin: **http://localhost:9080**

Uses the official **`caddy:2-alpine`** image (same family as production). No need to `brew install caddy` on the Mac.

Caddy runs in Docker; API + frontends run on the **host** (`task dev` / `task fe`). The Caddyfile proxies to `host.docker.internal` so the container can reach host ports.

## Prerequisites

1. **Docker Desktop** running (same as `task infra:up`).
2. Host stack listening on fixed ports:

| Upstream | Port | Role |
|----------|------|------|
| API | 8080 | `/health`, `/api/*` |
| developers | 3002 | `/docs*` |
| ops | 3003 | `/` (catch-all) |
| portal | 3004 | `/portal*` |
| admin | 3005 | `/admin/*` |

```bash
task infra:up
task dev          # API on :8080
task fe           # frontends via mprocs (sets basePath envs)
```

Vite/Next must bind all interfaces (`host: true` / default Next) so Docker can reach them via `host.docker.internal`.

## Run the gateway

```bash
# Foreground (logs in terminal)
task proxy

# Detached
task proxy:up
task proxy:down

# Or compose directly
docker compose -f docker-compose.dev-proxy.yml up -d
```

Optional: enable the `caddy` proc in `mprocs-dev.yaml` (`autostart: false` by default).

Validate Caddyfile (no host binary required):

```bash
task proxy:validate
```

## Path map

| Path | Upstream (on host) |
|------|--------------------|
| `/health`, `/api/*` | `host.docker.internal:8080` |
| `/portal*` | `host.docker.internal:3004` |
| `/docs*` | `host.docker.internal:3002` |
| `/admin`, `/admin/*` | `host.docker.internal:3005` (`handle_path` strips `/admin`) |
| `/` | `host.docker.internal:3003` (ops) |

Compose file: [`../../docker-compose.dev-proxy.yml`](../../docker-compose.dev-proxy.yml)  
Caddyfile: [`Caddyfile`](./Caddyfile)

`mprocs-dev.yaml` sets `NEXT_BASE_PATH` / `VITE_BASE_PATH` so path prefixes match prod. Direct app ports then use those prefixes too (e.g. `http://localhost:3004/portal`).

Plain `pnpm dev` outside mprocs leaves base paths unset unless you export them yourself.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `connection refused` from Caddy | Host app not running, or not listening on `0.0.0.0` |
| Docker not running | Start Docker Desktop |
| Port 9080 busy | `lsof -i :9080` and free it, or change publish mapping |
| Linux only | `extra_hosts: host.docker.internal:host-gateway` is already set in compose |
