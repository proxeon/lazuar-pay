# Environments & public URLs

Two network hops must work for real money:

```text
1) Billplz/Stripe  →  Hub inbound webhook URL   (must be public to the processor)
2) Hub             →  Your app webhook URL      (must be reachable from Hub)
```

Browser opening a checkout is **not** enough.

## Bases to configure

| Hop | Config (typical) | Example local | Example staging |
|-----|------------------|---------------|-----------------|
| Hub API (public) | `App:ApiBaseUrl` / `App__ApiBaseUrl` | Tunnel → `https://abc.ngrok.app/api/v1` | `https://hub-staging.example/api/v1` |
| Your webhook | Registered at provision / Ops | Tunnel → your app, or same host | `https://app-staging.example/webhooks/hub/payments` |

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

- Hub API on host `:8090`  
- Your app on host `:8081`  
- Hub → app can use `http://127.0.0.1:8081/...`  
- Billplz → Hub still needs **public** Hub URL (tunnel)  

### Pattern B — tunnel Hub

```bash
# example
ngrok http 8090
# set Hub App:ApiBaseUrl to https://xxxx.ngrok-free.app/api/v1
# re-create bills after changing base (old bills keep old callbacks)
```

Full ops runbook (Aura dual-stack): monorepo `idea/022-remaining/RUNBOOK-local-full-fulfillment.md` if present, or Hub Taskfile `tunnel:api`.

## Staging / production

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
