# API keys & scopes

## Machine keys

| Property | Value |
|----------|--------|
| Prefix | `sk_test_` / `sk_live_` (**Prefix decision = B** — not `lpk_`) |
| Auth header | `Authorization: Bearer sk_…` |
| Bound to | One workspace (tenant) |
| Reveal | **Once** at create / first provision |
| Introspect | `GET /api/v1/integrations/payments/me` |

Stripe merchant secrets use the same `sk_` prefix. A value that only matches `sk_live_` is **not** a valid Lazuar Pay key — probe `/me`. 401/403 means mint a Pay key, do not paste a Stripe secret.

## Mint a Payments integrator key (AuraBook Guest payments)

1. Sign in to Lazuar Pay Ops → **Developer → API Keys**.
2. **Create Key**. Name is required (e.g. `AuraBook guest`).
3. Environment: **Test** (`sk_test_…`) or **Live** (`sk_live_…`).
4. Click preset **Payments integrator** (scopes: `payments.checkouts:write`, `payments.checkouts:read`, `webhooks.endpoints:manage`). Do not add LHDN scopes.
5. Create. Copy the secret **once**. It is a **Lazuar Pay** secret, not a Stripe secret — same `sk_` prefix, different system.
6. Paste into AuraBook **Guest payments → Lazuar Pay**. Aura will `GET /integrations/payments/me` then `POST /one/workspaces/{workspace_id}/webhooks`. You can probe first:

```bash
curl -sS "$HUB/integrations/payments/me" -H "Authorization: Bearer $SK"
```

### Default bootstrap scopes (Payments integrator)

- `payments.checkouts:write`  
- `payments.checkouts:read`  
- `webhooks.endpoints:manage`  

### Do not grant by default

- Key mint / revoke for machines  
- Payment-config **write** (BYOK secrets) — human OrgAdmin only  
- Superadmin / cross-tenant powers  

## Mint / list / revoke

Human OrgAdmin (Ops → API Keys) or:

```http
POST /api/v1/one/api-keys
GET  /api/v1/one/api-keys
DELETE /api/v1/one/api-keys/{id}
```

`scopes` is **required**. Omitting it or sending `[]` returns **400**. There is no implicit LHDN default. Ops and provision always send an explicit array.

Commerce subscription admin (list / get / cancel): `commerce.subscriptions:read` and `commerce.subscriptions:write` (write implies read).

## Rotation (today)

1. Mint new key.  
2. Deploy new secret to your app.  
3. Revoke old key.  

Dual-key rotation window and `LastUsedAt` are still maturing — treat as single-cut rotate.

## Provision secret

Separate from machine keys:

- Env: `INTEGRATOR_PROVISION_SECRET`  
- Header: `X-Lazuar-Provision-Key`  
- Can create workspaces + first key — protect like a root credential  

Prefer per-integrator secrets later; today often one env secret per Hub deployment.

## Security checklist

- [ ] Keys only on servers  
- [ ] Rotate if leaked  
- [ ] Least scopes  
- [ ] Never log full `sk_` / `whsec_`  
- [ ] Revoke unused keys  

## Related

- [Architecture: who does what — M3 secrets](/guide/architecture-who-does-what#m3--who-holds-which-secrets)
- [Provision a workspace](/integrations/provision)
