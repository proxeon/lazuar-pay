# API keys & scopes

## Machine keys

| Property | Value |
|----------|--------|
| Prefix | `sk_test_` / `sk_live_` |
| Auth header | `Authorization: Bearer sk_…` |
| Bound to | One workspace (tenant) |
| Reveal | **Once** at create / first provision |

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

Omit scopes on mint → may default to **LHDN** scopes (legacy behavior). Always pass **explicit** payment scopes for cashier apps.

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
