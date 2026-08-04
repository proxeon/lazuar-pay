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

## Background workers / multi-instance (Phase C.5)

The API process hosts domain `BackgroundService` workers (billing, dunning, LHDN submit/poll,
broadcast fan-out, outbound webhooks, B2C consolidation, plus outbox/inbox publishers).

**Deploy rule:** keep a **single API replica** (or a dedicated worker deployment with `replicas: 1`)
unless every worker you run has claim isolation. Do not scale the API horizontally for load without
either:

1. **replica=1** for the process that runs workers, or  
2. splitting workers into a separate compose service that stays at replica 1 while stateless API
   replicas only serve HTTP (not yet split in this stack).

### Claimed-safe (multi-instance OK via `FOR UPDATE SKIP LOCKED` / leases)

| Worker | Claim mechanism |
|--------|-----------------|
| Outbox / Inbox publishers (all modules) | `SKIP LOCKED` + attempt/backoff |
| `OutboundWebhookDispatcherJob` | `SKIP LOCKED` + `ClaimLease` on `NextAttemptAt` |
| `LhdnSubmissionJob` / `LhdnStatusPollingJob` | `SKIP LOCKED` + `ClaimProcessingLease` on `NextPollAt` |
| `BroadcastFanoutJob` | `SKIP LOCKED` then `MarkSending` (status claim) |
| `BillingEngineJob` / `DunningEngineJob` | `SKIP LOCKED` per batch; per-subscription save isolation |

### Still prefer single replica (calendar / idempotency only)

| Worker | Notes |
|--------|--------|
| `B2cConsolidationJob` | Catch-up for closed months; per-org saves + period ref idempotency — safe enough, but schedule is calendar-based |
| `SystemGenesisBootstrapperJob` | Boot-time only |
| `LhdnReferenceDataSeederJob` | Boot-time seed |

Configurable intervals: `Workers` section in appsettings (see `BackgroundWorkerOptions`).

## First-time

1. VPS + Docker + UFW 22/80/443  
2. DNS `hub.lazuar.com` A → VPS (grey cloud)  
3. `rsync deploy/prod/` → `/root/lazuar-hub-prod/`  
4. `cp env.example .env` — Neon Npgsql strings + Jwt  
5. `docker login ghcr.io`  
6. `VERSION=latest /root/lazuar-hub-remote-deploy.sh`  

## Secrets (GitHub Actions)

`SSH_HOST`, `SSH_USER`, `SSH_PRIVATE_KEY`, optional `HUB_ENV_FILE`, `GHCR_PULL_TOKEN`.

## Secrets & BYOK (Phase C.7)

**Never commit real secrets** into `appsettings*.json`. Production injects env vars via `.env` on the VPS
(see `env.example`). Local API uses:

| Source | Use |
|--------|-----|
| `dotnet user-secrets` (`UserSecretsId` on Lazuar.Api) | Local Jwt/Kms/Resend/AI keys |
| Environment variables (`Jwt__Secret`, `Kms__MasterKey`, …) | Docker / CI / prod |
| Azure Key Vault (optional `KeyVault:Uri` in config) | Production secret store |
| Tenant BYOK rows (encrypted at rest) | Payment gateways, LHDN client secret/PFX, Resend |

### Required platform secrets

| Key | Purpose |
|-----|---------|
| `Jwt__Secret` | Auth tokens (min 32 chars) |
| `Kms__MasterKey` | AES master key for tenant secrets at rest (falls back to Jwt if unset) |
| `ConnectionStrings__*` | Postgres |
| `Resend__ApiKey` | Optional platform email |

### Tenant secrets (BYOK)

Stored in DB encrypted with `AesSecretVault` / LHDN `CertificateVaultService` (AES-256-CBC, IV prepended).

- Payments: API key + webhook secret encrypted; soft-disable via `IsActive` without deleting credentials
- LHDN: MyInvois client secret encrypted; PFX bytes + password encrypted
- Communications: Resend API key encrypted (Phase C.4)

GET endpoints return `has_*` flags and last-4 hints only — never ciphertext or plaintext secrets.
