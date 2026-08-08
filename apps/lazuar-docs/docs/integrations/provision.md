# Provision a workspace

Creates (or reuses) a Hub workspace bound to **your** product tenant, mints a bootstrap API key, optionally registers an outbound webhook.

## Endpoint

```http
POST /api/v1/one/integrations/workspaces/provision
```

### Auth

Either:

- Header `X-Lazuar-Provision-Key: <INTEGRATOR_PROVISION_SECRET>`, or  
- `Authorization: Bearer <same secret>`, or  
- SUPER_ADMIN human session  

## Preferred body (multi-product)

```bash
export HUB=http://localhost:8090/api/v1   # your Hub base + /api/v1

curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $PROVISION_SECRET" \
  -d '{
    "external_product": "demo-app",
    "external_org_id": "tenant-001",
    "display_name": "Demo App Tenant 001",
    "is_test_mode": true,
    "webhook_url": "https://your-app.example/webhooks/hub/payments",
    "owner_email": "you@example.com"
  }'
```

### Field notes

| Field | Required | Notes |
|-------|----------|--------|
| `external_product` | no | Defaults to `aura`. Stable product slug (`demo-app`, `erp`, …). |
| `external_org_id` | * | Your tenant id. Alias: `aura_org_id`. |
| `aura_org_id` | * | Aura-compatible; must be **GUID** when product is `aura`. |
| `display_name` | yes | Human label for Ops UI. |
| `is_test_mode` | no | Prefer `true` outside production. |
| `webhook_url` | no | If set, creates/heals outbound endpoint; returns `webhook.secret_key` once. |
| `webhook_enabled_events` | no | Defaults to `payment.completed`, `payment.failed`. |
| `owner_email` | no | If user already exists, attaches ADMIN (or `owner_role`) membership. **Does not create users.** |
| `owner_role` | no | `ADMIN` (default) or `SUPER_ADMIN` (workspace role only — not global system admin). |

\* Provide `external_org_id` **or** `aura_org_id`.

## Idempotency

Re-call with the same `(external_product, external_org_id)`:

- Returns same `workspace_id`  
- `created: false`  
- Does **not** remint `plain_key` (null on re-call)  
- Webhook: exact URL match → no secret remint; missing URL may **heal** if you pass `webhook_url` again  

## Response (shape)

```json
{
  "workspace_id": "…",
  "slug": "…",
  "created": true,
  "external_product": "demo-app",
  "external_org_id": "tenant-001",
  "api_key": {
    "id": "…",
    "prefix": "sk_test_",
    "scopes": [
      "payments.checkouts:write",
      "payments.checkouts:read",
      "webhooks.endpoints:manage"
    ],
    "plain_key": "sk_test_…"
  },
  "webhook": {
    "id": "…",
    "url": "https://your-app.example/webhooks/hub/payments",
    "secret_key": "whsec_…"
  },
  "owner": {
    "attached": true,
    "status": "attached",
    "role": "ADMIN",
    "email": "you@example.com"
  }
}
```

**Store `plain_key` and `secret_key` immediately** — they are not returned again.

## Aura-compatible body

```json
{
  "aura_org_id": "11111111-1111-1111-1111-111111111111",
  "display_name": "Salon Melati",
  "is_test_mode": true,
  "webhook_url": "https://aura.example/api/v1/webhooks/hub/payments"
}
```

## Next

1. [API keys & scopes](/integrations/api-keys)  
2. Configure BYOK in Hub Ops for the workspace  
3. [Create a checkout](/integrations/create-checkout)  
