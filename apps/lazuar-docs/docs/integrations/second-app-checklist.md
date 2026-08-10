# Second-app checklist

Prove Hub works **without** the Aura monorepo.

## Goal

A second backend (or sample) can:

1. Provision a workspace  
2. Configure BYOK (human once)  
3. Create checkout  
4. Receive signed webhook  
5. Mark **its own** domain object paid  

**Preferred executable path:** monorepo sample `examples/hub-cashier-next` (port **3020**). Runbook: [Run sample app](/integrations/run-sample-app).

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

Running the sample is necessary for a **demoable** multi-app cashier story. Independence checks still apply for honest multi-product claims (sample defaults use `external_product: sample-shop`, not `aura`).

## Proof sequence

```text
Developer
   |
   |-- 1. Provision non-aura product (external_product != aura)
   |-- 2. Store sk_ + whsec_ once
   |-- 3. Human BYOK on workspace (Ops)
   |-- 4. POST checkouts (Bearer sk_)
   |-- 5. Pay sandbox on checkout_url  OR  fake signed webhook (handler path)
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

- [ ] Stable `external_product` string (not `aura` if this is not Aura) — sample default: `sample-shop`  
- [ ] Stable `external_org_id` — sample default: `local-dev-1`  
- [ ] Webhook receiver with signature verify + idempotency — sample path: `POST /webhooks/hub/payments` on **:3020**  
- [ ] Public webhook URL Hub can reach — local hop 2: `http://127.0.0.1:3020/webhooks/hub/payments`  

### Flow

- [ ] `POST …/workspaces/provision` with `webhook_url` ([Provision](/integrations/provision))  
- [ ] Store `api_key.plain_key` and `webhook.secret_key` once  
- [ ] Ops: set Billplz/Stripe test keys for workspace  
- [ ] `POST …/integrations/payments/checkouts` ([Create checkout](/integrations/create-checkout))  
- [ ] Pay in sandbox **or** inject signed `payment.completed` ([Webhooks](/integrations/webhooks))  
- [ ] Webhook received → domain unlocked  
- [ ] Replay webhook → no double unlock  

### Independence

- [ ] Zero imports from Aura repo  
- [ ] No shared database with Aura  
- [ ] Secrets only in this app’s secret store  
- [ ] No Billplz/Stripe SDK in this app’s dependencies  

## Harness

| Path | Role |
|------|------|
| **Sample (preferred)** | `examples/hub-cashier-next` — Next.js App Router, port **3020** |
| **Runbook** | [Run sample app](/integrations/run-sample-app) |
| **Curl harness** | monorepo `plans/006-sample/harness/second-app-proof.md` |
| **Engineer quickstart** | monorepo `docs/payments-integration-quickstart.md` |
| **Evidence template** | monorepo `plans/006-sample/evidence/local-e2e.md` |

The old singular path `script/second-app-proof.md` is **removed** — do not treat it as the primary harness.

Start sample from monorepo root:

```bash
pnpm example:cashier
# or
pnpm --filter @examples/hub-cashier-next dev
# → http://localhost:3020
```

Fake signed webhook (handler + unlock, not full hop-1 sandbox):

```bash
cd examples/hub-cashier-next
ORDER_ID=<uuid> CHECKOUT_ID=<id> node scripts/send-fake-webhook.mjs
```

## Related

- [Architecture: who does what](/guide/architecture-who-does-what)  
- [Payment flow](/integrations/payment-flow)  
- [Run sample app](/integrations/run-sample-app)  
- [Payments cashier](/integrations/payments-cashier)  
- [Provision](/integrations/provision) · [Create checkout](/integrations/create-checkout) · [Webhooks](/integrations/webhooks)  

## When this is green

You may honestly say Hub is a **multi-app cashier**, not only an Aura dependency. Until then, market Hub as cashier infrastructure with Aura as reference client.
