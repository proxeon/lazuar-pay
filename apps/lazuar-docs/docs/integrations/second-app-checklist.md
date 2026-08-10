# Second-app checklist

Prove Hub works **without** the Aura monorepo.

## Goal

A second backend (or script) can:

1. Provision a workspace  
2. Configure BYOK (human once)  
3. Create checkout  
4. Receive signed webhook  
5. Mark **its own** domain object paid  

## Independence boundary

```text
  FORBIDDEN                              ALLOWED
  ---------                              -------
  Aura monorepo imports                  Second app / sample
  Shared Aura DB                         HTTPS to Lazuar Hub API
  Billplz/Stripe SDK in your app         Merchant gateway via Hub BYOK only
       |                                      |
       |  no imports / no shared DB           |
       + - - - - - X - - - - - - - - - - - - -+
                                              |
                                     App2 --sk_/whsec_--> Hub --> Gateway
```

**Summary:** The second app talks only to Hub over HTTPS with `sk_` and `whsec_`. No Aura code imports, no shared database, no direct gateway SDKs in the app.

## Proof sequence

```text
Developer
   |
   |-- 1. Provision non-aura product (external_product != aura)
   |-- 2. Store sk_ + whsec_ once
   |-- 3. Human BYOK on workspace (Ops)
   |-- 4. POST checkouts (Bearer sk_)
   |-- 5. Pay sandbox on checkout_url
   |-- 6. Receive signed payment.completed
   |-- 7. Unlock own domain object
   |-- 8. Replay webhook → no double unlock
```

Full narrative diagrams: [Payment flow](/integrations/payment-flow).  
Environments / hops: [Environments](/integrations/environments).

## Checklist

### Hub setup

- [ ] Hub API running (local/staging) — prefer `http://localhost:8080/api/v1`  
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

Runnable sample (when present):

```text
examples/hub-cashier-next
```

Fill evidence (when you run it):

```text
# if you track evidence in Aura product notes
idea/022-remaining/evidence/PHASE22-second-app.md
```

## Related

- [Payment flow](/integrations/payment-flow)  
- [Payments cashier](/integrations/payments-cashier)  
- [Provision](/integrations/provision) · [Create checkout](/integrations/create-checkout) · [Webhooks](/integrations/webhooks)  

## When this is green

You may honestly say Hub is a **multi-app cashier**, not only an Aura dependency. Until then, market Hub as cashier infrastructure with Aura as reference client.
