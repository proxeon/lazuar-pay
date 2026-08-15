# Server TODO — Lazuar Pay / Hub after `main` (024 + 025)

**VPS compose:** `/root/lazuar-hub-prod`  
**Public host:** `https://hub.lazuar.com` (`/api/*` → API)  
**CI:** push to `main` builds GHCR then SSH-deploys (`.github/workflows/ghcr.yml`).

Local named hosts (`pay-local.lazuar.dev`) are **laptop-only**. Do not put them in production `.env`.

Keep a **single API replica** (workers live in the API process). See `deploy/prod/README.md`.

---

## 1. Ship images

- [ ] Wait until GitHub Actions **GHCR + deploy** is green for this `main` SHA.
- [ ] If CD did not run, on the VPS:

```sh
cd /root/lazuar-hub-prod
# VERSION=sha-<short>   # optional pin
docker compose pull
docker compose up -d --remove-orphans
# or: VERSION=sha-<short> /root/lazuar-hub-remote-deploy.sh
```

- [ ] `docker compose ps` — `hub-api` healthy.
- [ ] `GET https://hub.lazuar.com/health` → 200.

---

## 2. Migration (Pay DB / Neon)

API applies EF migrations on boot. This branch adds Commerce `20260814184123_AddSubscriptionAndCheckoutMetadataJson`:

| Schema | Table | Column |
|--------|-------|--------|
| `commerce` | `Subscriptions` | `MetadataJson` jsonb |
| `commerce` | `CheckoutSessions` | `MetadataJson` jsonb |

- [ ] After API is healthy:

```sql
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'commerce'
  AND column_name = 'MetadataJson';
```

025 itself adds **no** extra Payments/One tables. `CALLBACK_BASE_NOT_PUBLIC` is code-only.

---

## 3. Update server `.env`

Edit `/root/lazuar-hub-prod/.env` (from `deploy/prod/env.example`). Then recreate API.

| Key | Production value | Notes |
|-----|------------------|--------|
| `App__ApiBaseUrl` | `https://hub.lazuar.com/api/v1` | **Hop A.** Stamped on every new Billplz `callback_url`. Must be public https. |
| `App__ClientUrl` | `https://hub.lazuar.com/portal` | Already in env.example. |
| `App__CorsOrigins` | include `https://hub.lazuar.com` and the Aura origin if browsers call Hub | |
| `App__AllowInsecureBillplzCallback` | unset / `false` | Never `true` in prod. |
| `App__BillplzEnvironment` | `production` only when using live Billplz | Empty + host `hub.lazuar.com` also selects **production** Billplz (exact hosts: `api.lazuar.com`, `pay.lazuar.com`, `hub.lazuar.com`). Staging must use a non-prod host **or** `App__BillplzEnvironment=sandbox`. |
| `INTEGRATOR_PROVISION_SECRET` | **same** as Aura `HUB_PAYMENTS_PROVISION_SECRET` | Required for Connect / provision. |
| `Kms__MasterKey` | already set; **do not rotate** | Decrypts tenant Billplz/Stripe keys. |
| `Jwt__Secret` | already set; **do not rotate** casually | |

`localhost`, `lazuar-local-dev.com`, ngrok, and trycloudflare are refused as Billplz callback bases (`422 CALLBACK_BASE_NOT_PUBLIC`).

Development-only demo seed (`founder@acme.test`) does **not** run in Production.

---

## 4. Recreate API and prove hop A

```sh
docker compose up -d --force-recreate --no-deps api
```

- [ ] Process / inspect shows `App__ApiBaseUrl=https://hub.lazuar.com/api/v1`
- [ ] Empty inbound webhook is reachable (400 empty body is OK):

```sh
curl -sS -o /dev/null -w '%{http_code}\n' \
  -X POST 'https://hub.lazuar.com/api/v1/webhooks/payments/billplz/00000000-0000-0000-0000-000000000000'
```

- [ ] New Billplz bill `callback_url` starts with  
  `https://hub.lazuar.com/api/v1/webhooks/payments/billplz/`  
  and does **not** contain localhost / `:8080` / `:8090`.

Old bills keep the callback from create time. Create **new** checkouts after this change.

---

## 5. Hop B (Aura’s door, stored on Pay)

Pay POSTs to whatever URL is on `one.TenantWebhookEndpoints`.

- [ ] After Aura sets `HUB_PAYMENTS_PUBLIC_API_BASE_URL`, re-Connect / re-paste so the active Acme (or live salon) row is  
  `https://<aura-public-host>/api/v1/webhooks/hub/payments`
- [ ] `IsActive=true`, events include `payment.completed` and `payment.failed`.
- [ ] Disable leftover ngrok / localhost rows.
- [ ] Outbound dispatcher can reach that URL from **this** VPS (not just your laptop).

---

## 6. Do not

- [ ] Do not scale `hub-api` to multiple replicas in this stack.
- [ ] Do not point Billplz at Aura `/webhooks/gateway/*`.
- [ ] Do not expect existing sandbox/prod bills to retarget after changing `App__ApiBaseUrl`.
- [ ] Do not commit `.env`, `sk_`, `whsec_`, or Billplz keys.
