# 06 — Provision & environment setup

**Status:** analysis complete 2026-08-10  
**SSoT:** TypeSpec `packages/api-spec/modules/one/models/provision.tsp`; docs `apps/lazuar-docs/docs/integrations/provision.md`; handler/endpoints under One module; engineer twin `docs/payments-integration-quickstart.md`.

---

## 1. What you need before first checkout

| # | Prerequisite | How |
|---|--------------|-----|
| 1 | Hub API running | `task dev` / local API on **:8080** |
| 2 | Provision auth | `INTEGRATOR_PROVISION_SECRET` on Hub **or** SUPER_ADMIN session |
| 3 | Active BYOK | Human in Hub Ops → Payment settings (Billplz/Stripe/CHIP/Razorpay) for workspace |
| 4 | Public Hub base (Hop 1) | Tunnel if using real sandbox processor callbacks |
| 5 | Your webhook URL (Hop 2) | Sample `APP_BASE_URL` + `/api/webhooks/hub` reachable from Hub |

Email/Resend is **not** required for M2M Payments.

---

## 2. Ports: 8080 vs 8090 (canonical)

| Source | Port | Role |
|--------|------|------|
| `launchSettings.json`, root README, compose, `App:ApiBaseUrl` default | **8080** | **Canonical Hub API** |
| Some docs examples (`provision.md`, `create-checkout.md`) | **8090** | **Stale/alternate** — treat as historical |
| `lazuar-docs` VitePress | **5180** | Docs only |
| Sample app (proposed) | **3005** | Next sample |
| `lazuar-developers` | 3002 | Scalar |
| `lazuar-portal` | 3004 | Portal |

**Lock for 006:**

```bash
export HUB=http://localhost:8080/api/v1
```

Docs PRs should normalize 8090 → 8080 when touching those pages. Environments page may note: “If your fork maps 8090, substitute.”

CORS already lists 8080 and 8090 in `appsettings*.json`.

---

## 3. How to get `sk_` and `whsec_`

### Path A — Provision API (preferred for second-app / sample)

```http
POST /api/v1/one/integrations/workspaces/provision
X-Lazuar-Provision-Key: <INTEGRATOR_PROVISION_SECRET>
Content-Type: application/json
```

Also accepted: `Authorization: Bearer <same secret>` or SUPER_ADMIN JWT.

#### Preferred multi-product body

```json
{
  "external_product": "demo-app",
  "external_org_id": "tenant-001",
  "display_name": "Demo App Tenant 001",
  "is_test_mode": true,
  "webhook_url": "http://127.0.0.1:3005/api/webhooks/hub",
  "owner_email": "you@example.com"
}
```

| Field | Notes |
|-------|--------|
| `external_product` | Default `aura` if omitted; sample should use **`demo-app`** or `hub-cashier-sample` |
| `external_org_id` | Stable id; alias `aura_org_id` |
| `aura_org_id` | For product `aura` must be GUID |
| `is_test_mode` | Default true → `sk_test_` bootstrap |
| `webhook_url` | Absolute URL; creates/heals endpoint |
| `webhook_enabled_events` | Default payment.completed + payment.failed |
| `owner_email` | Attaches existing user only — **does not create users** |

#### Response secrets (first materialization only)

```json
{
  "workspace_id": "…",
  "created": true,
  "api_key": {
    "prefix": "sk_test_",
    "scopes": [
      "payments.checkouts:write",
      "payments.checkouts:read",
      "webhooks.endpoints:manage"
    ],
    "plain_key": "sk_test_…"
  },
  "webhook": {
    "url": "http://127.0.0.1:3005/api/webhooks/hub",
    "secret_key": "whsec_…"
  }
}
```

Map:

| Response field | Sample env |
|----------------|------------|
| `api_key.plain_key` | `HUB_API_KEY` |
| `webhook.secret_key` | `HUB_WEBHOOK_SECRET` |
| `workspace_id` | optional `HUB_WORKSPACE_ID` for logs |

**Idempotent re-call:** `created: false`, `plain_key` null, `secret_key` null (unless webhook heal path issues new secret — treat re-call carefully).

### Path B — Ops UI mint

1. Create/login workspace as OrgAdmin.  
2. Ops → API Keys → mint with explicit payment scopes.  
3. Ops → Webhooks → create endpoint URL → copy secret once.  
4. Ops → Payment settings → paste gateway test keys → activate.

### Path C — Manual paste into sample `.env`

For workshops: operator provisions once, pastes secrets into learner `.env`.

---

## 4. Sample `.env.example` (authoritative draft)

```bash
# examples/hub-cashier-next/.env.example

# Hub API base INCLUDING /api/v1 (canonical local port 8080)
HUB_API_BASE_URL=http://localhost:8080/api/v1

# Machine API key — from provision api_key.plain_key (once) or Ops mint
HUB_API_KEY=sk_test_replace_me

# Outbound webhook signing secret — from provision webhook.secret_key (once)
HUB_WEBHOOK_SECRET=whsec_replace_me

# Public base URL of this sample (success/cancel + what you register as webhook_url)
APP_BASE_URL=http://localhost:3005

DEFAULT_CURRENCY=MYR

# Optional diagnostics
# HUB_WORKSPACE_ID=
```

`.gitignore` must include `.env` / `.env.local`.

---

## 5. BYOK (human step — cannot skip)

After provision, checkout fails with `PAYMENTS_NOT_CONFIGURED` until:

1. Open Hub Ops for the new workspace.  
2. Payment settings → choose gateway.  
3. Enter **test** credentials.  
4. Ensure configuration **IsActive**.  

Sample README must state this as a **hard prerequisite** with the error code name.

Hub is BYOK: money settles on **merchant** processor account. Sample never holds Billplz/Stripe secrets.

---

## 6. Test vs live

| Mode | Machine key | Gateway credentials | provision `is_test_mode` |
|------|-------------|---------------------|---------------------------|
| Local / demo | `sk_test_` | Sandbox/test keys | `true` |
| Staging | usually test | test | `true` |
| Production | `sk_live_` | live | `false` |

Rules:

- Never point sample default docs at live keys.  
- Separate `.env.production.local` if needed.  
- Webhook URLs must be HTTPS in real deploys (local HTTP to 127.0.0.1 OK).  

---

## 7. Provision script outline

`examples/hub-cashier-next/scripts/provision-and-print-env.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

HUB="${HUB_API_BASE_URL:-http://localhost:8080/api/v1}"
PROVISION_SECRET="${INTEGRATOR_PROVISION_SECRET:?set INTEGRATOR_PROVISION_SECRET}"
PRODUCT="${EXTERNAL_PRODUCT:-hub-cashier-sample}"
ORG_ID="${EXTERNAL_ORG_ID:-local-dev-1}"
WEBHOOK_URL="${WEBHOOK_URL:-http://127.0.0.1:3005/api/webhooks/hub}"
DISPLAY_NAME="${DISPLAY_NAME:-Hub Cashier Sample}"

RESP=$(curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $PROVISION_SECRET" \
  -d "$(jq -n \
    --arg p "$PRODUCT" \
    --arg o "$ORG_ID" \
    --arg n "$DISPLAY_NAME" \
    --arg w "$WEBHOOK_URL" \
    '{external_product:$p, external_org_id:$o, display_name:$n, is_test_mode:true, webhook_url:$w}')")

echo "$RESP" | jq .

PLAIN=$(echo "$RESP" | jq -r '.api_key.plain_key // empty')
WHSEC=$(echo "$RESP" | jq -r '.webhook.secret_key // empty')
WS=$(echo "$RESP" | jq -r '.workspace_id // empty')

if [[ -z "$PLAIN" ]]; then
  echo "WARN: plain_key empty (idempotent re-call?). Keep existing HUB_API_KEY." >&2
else
  echo
  echo "# paste into examples/hub-cashier-next/.env.local"
  echo "HUB_API_BASE_URL=$HUB"
  echo "HUB_API_KEY=$PLAIN"
  [[ -n "$WHSEC" ]] && echo "HUB_WEBHOOK_SECRET=$WHSEC"
  echo "APP_BASE_URL=http://localhost:3005"
  echo "HUB_WORKSPACE_ID=$WS"
fi
```

Requirements: `curl`, `jq`. Document that secrets print to stdout — run only on trusted machines.

Optional Node variant for Windows-friendly path later.

---

## 8. Environments & tunnels

### Same-machine local

| Hop | URL |
|-----|-----|
| Sample → Hub | `http://localhost:8080/api/v1` |
| Hub → Sample webhook | `http://127.0.0.1:3005/api/webhooks/hub` |
| Gateway → Hub | **Tunnel** to Hub `:8080` |

### Tunnel Hub (ngrok example)

```bash
ngrok http 8080
# set Hub App:ApiBaseUrl = https://xxxx.ngrok-free.app/api/v1
# re-create bills after changing base (old callbacks stick)
```

### Tunnel sample (if Hub is remote/docker without host access)

```bash
ngrok http 3005
# use https://xxxx/api/webhooks/hub as webhook_url at provision
# APP_BASE_URL=https://xxxx for success/cancel too
```

### False positives (from environments.md)

| Symptom | Cause |
|---------|--------|
| Checkout opens; never paid | Hop 1 or Hop 2 broken |
| Browser success only | App trusts redirect |
| Old bills fail after env change | Callback URL locked on bill |

---

## 9. `run-sample-app` docs outline

New page `apps/lazuar-docs/docs/integrations/run-sample-app.md`:

```markdown
# Run the Hub cashier sample

## Prerequisites
- Hub API :8080
- INTEGRATOR_PROVISION_SECRET or pasted sk_/whsec_
- Active BYOK on workspace
- Node 18+

## 1. Provision
## 2. Configure BYOK (Ops)
## 3. Install sample
## 4. .env.local
## 5. pnpm dev --filter hub-cashier-next
## 6. Create order → Pay → Confirm webhook unlock
## 7. Simulate webhook (python/curl) without gateway
## Troubleshooting
## Security notes
```

Detailed sections:

1. **Prerequisites** — table from §1  
2. **Provision** — script + curl; store secrets  
3. **BYOK** — Ops screenshots optional; error code `PAYMENTS_NOT_CONFIGURED`  
4. **Env file** — paste `.env.example`  
5. **Start** — monorepo from root vs `cd examples/hub-cashier-next`  
6. **Happy path** — browser steps + expected order states  
7. **Webhook simulation** — link 05  
8. **Second-app checklist** — map to checklist boxes  
9. **Teardown** — revoke key optional  

Sidebar entry under Integrations (see 08).

---

## 10. Hub-side env checklist (operator)

| Env / config | Purpose |
|--------------|---------|
| `INTEGRATOR_PROVISION_SECRET` | Provision auth |
| `App:ApiBaseUrl` | Public base for gateway callbacks |
| DB connection | One + Payments schemas |
| Gateway secrets | Per workspace via Ops (encrypted at rest) |
| Outbound webhook worker | `OutboundWebhookDispatcherJob` enabled |

Sample does not set these; developer must run Hub stack.

---

## 11. Mapping provision product slug

| Context | `external_product` |
|---------|-------------------|
| Aura production | `aura` + GUID org |
| Sample default | `hub-cashier-sample` or `demo-app` |
| Learner workshop | `workshop-{name}` |

Avoid using `aura` for sample so evidence proves multi-product.

---

## 12. Implementation checklist

- [ ] Normalize docs ports 8080  
- [ ] `.env.example` in sample  
- [ ] `scripts/provision-and-print-env.sh`  
- [ ] `run-sample-app.md`  
- [ ] README provision section  
- [ ] Document BYOK human step  
- [ ] Document secret one-time reveal  
