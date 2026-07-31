# Lazuar Hub — production deploy (dedicated VPS)

Path-based host: **https://hub.lazuar.com**

| Path | Service |
|------|---------|
| `/` | ops |
| `/portal` | portal (Next) |
| `/docs` | developer API docs (Next + Scalar) |
| `/api/*` | .NET API |
| `/admin/*` | superadmin |
| `/health` | API liveness |

See also: repo root workflows `.github/workflows/ghcr.yml`.

## First-time

1. VPS + Docker + UFW 22/80/443  
2. DNS `hub.lazuar.com` A → VPS (grey cloud)  
3. `rsync deploy/prod/` → `/root/lazuar-hub-prod/`  
4. `cp env.example .env` — Neon Npgsql strings + Jwt  
5. `docker login ghcr.io`  
6. `VERSION=latest /root/lazuar-hub-remote-deploy.sh`  

## Secrets (GitHub Actions)

`SSH_HOST`, `SSH_USER`, `SSH_PRIVATE_KEY`, optional `HUB_ENV_FILE`, `GHCR_PULL_TOKEN`.
