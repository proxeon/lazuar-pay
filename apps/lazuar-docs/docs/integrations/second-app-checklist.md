# Second-app checklist

Prove Hub works **without** the Aura monorepo.

## Goal

A second backend (or script) can:

1. Provision a workspace  
2. Configure BYOK (human once)  
3. Create checkout  
4. Receive signed webhook  
5. Mark **its own** domain object paid  

## Checklist

### Hub setup

- [ ] Hub API running (local/staging)  
- [ ] `INTEGRATOR_PROVISION_SECRET` set  
- [ ] Public Hub base for processor callbacks (tunnel if local)  

### App setup

- [ ] Stable `external_product` string (not `aura` if this is not Aura)  
- [ ] Stable `external_org_id`  
- [ ] Webhook receiver with signature verify + idempotency  
- [ ] Public webhook URL Hub can reach  

### Flow

- [ ] `POST …/workspaces/provision` with `webhook_url`  
- [ ] Store `api_key.plain_key` and `webhook.secret_key` once  
- [ ] Ops: set Billplz/Stripe test keys for workspace  
- [ ] `POST …/integrations/payments/checkouts`  
- [ ] Pay in sandbox  
- [ ] Webhook received → domain unlocked  
- [ ] Replay webhook → no double unlock  

### Independence

- [ ] Zero imports from Aura repo  
- [ ] No shared database with Aura  
- [ ] Secrets only in this app’s secret store  

## Harness

Engineer curl notes live in monorepo:

```text
script/second-app-proof.md
```

Fill evidence (when you run it):

```text
# if you track evidence in Aura product notes
idea/022-remaining/evidence/PHASE22-second-app.md
```

## When this is green

You may honestly say Hub is a **multi-app cashier**, not only an Aura dependency. Until then, market Hub as cashier infrastructure with Aura as reference client.
