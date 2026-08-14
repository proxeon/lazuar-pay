# Environments & public URLs

Two network hops must work for real money. Browser opening a checkout is **not** enough.

```text
1) Billplz/Stripe  →  Hub inbound webhook URL   (must be public to the processor)
2) Hub             →  Your app webhook URL      (must be reachable from Hub)
```

## Network components

Canonical Hub API port for **new** diagrams and samples: **8080**  
(`http://localhost:8080/api/v1`). Older prose may still say **8090** — treat that as drift; prefer **8080**.

```text
                    +---------------------------+
  Guest ----pay---> | Gateway sandbox / live    |
                    +-------------+-------------+
                                  | hop 1 (must be public)
                                  v
                    +---------------------------+
                    | Lazuar Hub API :8080      |
                    | App:ApiBaseUrl (public)   |
                    +------+-------------+------+
           create checkout |             | hop 2 (Hub → your webhook)
                           |             v
                           |  +---------------------------+
                           +->| Your app webhook receiver |
                              | provision webhook_url     |
                              +---------------------------+
```

**Summary:** Hop 1 is processor → Hub public base. Hop 2 is Hub → your app. Local same-machine can use localhost for hop 2; hop 1 still needs a public Hub URL (tunnel). Staging/prod use real TLS hostnames for both.

## Bases to configure

| Hop | Config (typical) | Example local | Example staging |
|-----|------------------|---------------|-----------------|
| Hub API (public) | `App:ApiBaseUrl` / `App__ApiBaseUrl` | Tunnel → `https://abc.ngrok.app/api/v1` | `https://hub-staging.example/api/v1` |
| Your webhook | Registered at provision / Ops | `http://127.0.0.1:…/api/webhooks/hub` or tunnel | `https://app-staging.example/webhooks/hub/payments` |

### Aura-specific aliases

When the client is Aura:

| Config | Purpose |
|--------|---------|
| `HubPayments:BaseUrl` | Aura → Hub API (server-side) |
| `HubPayments:PublicApiBaseUrl` | Aura’s public base used to **build** Hub→Aura webhook URL at Connect |

Do not confuse:

- `APP_API_BASE_URL` on Aura (often messaging/ngrok for Meta) with Hub payments bases.

## Local development

### Pattern A — same machine

```text
Your app (:3020 sample / your port)  <--- hop2 localhost OK ---  Hub API (:8080)
Gateway sandbox  --hop1 public-->  Tunnel  -->  Hub :8080
```

- Hub API on host **`:8080`** (canonical)  
- Your app on host (sample **`:3020`** or your port)  
- Hub → app can use `http://127.0.0.1:…/…`  
- Billplz → Hub still needs **public** Hub URL (tunnel)  

### Pattern B — tunnel Hub

```text
Gateway --> ngrok/cloudflared --> Hub :8080
Hub --> your app (localhost or second tunnel)
```

```bash
# example — map to your Hub listen port (prefer 8080)
ngrok http 8080
# set Hub App:ApiBaseUrl to https://xxxx.ngrok-free.app/api/v1
# re-create bills after changing base (old bills keep old callbacks)
```

**Billplz lock-in:** Old bills keep the callback URL from creation time. After changing `App:ApiBaseUrl` or tunnel host, **create new checkouts/bills** — do not expect old sandbox bills to hit the new public base.

**Billplz sandbox vs live is not the K1 prefix.** Hub calls `https://www.billplz-sandbox.com` unless `App:ApiBaseUrl` contains `lazuar.com`, in which case it calls production Billplz. A `sk_live_` integrator key against a non-prod Hub still hits Billplz **sandbox**. Aura may warn: “Billplz environment follows Hub base URL, not the key prefix.”

Full ops runbook (Aura dual-stack): monorepo `idea/022-remaining/RUNBOOK-local-full-fulfillment.md` if present, or Hub Taskfile `tunnel:api`.

## Staging / production

```text
Gateway live/test  -->  Hub HTTPS  -->  App HTTPS
App                -->  Hub HTTPS  (create checkout, etc.)
```

- Real TLS hostnames  
- Separate `sk_test_` / `sk_live_` and test vs live gateway credentials  
- Rotate webhook secrets carefully (Hub endpoint + your app store)  

## False positives

| Symptom | Likely cause |
|---------|----------------|
| Checkout URL opens, booking never paid | Webhook hop broken (1 or 2) |
| Works in browser “success” only | App trusts redirect |
| Old bills fail after env change | Billplz bill locked old callback URL |

## Checklist

- [ ] Hub public base set for this env  
- [ ] Your webhook URL registered and signed  
- [ ] BYOK active on workspace  
- [ ] Test pay → domain unlock without manual mark-paid  

## Related

- [Payment flow](/integrations/payment-flow)  
- [Webhooks](/integrations/webhooks)  
