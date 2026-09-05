# Lazuar Pay (focused host)

Checkout-as-a-service money host.

- One solution, one host, one test project.
- Listen on **8081**. Never bind 8080 (One and old Hub use it).
- Merchants come from **lazuar-one**. Local One API: `One__BaseUrl=http://localhost:8080/api/v1` (see `.env.example`). Do not copy `Modules/One`.
- Do not add MediatR or per-module DbContexts.

## Source layout

One `Lazuar.Pay.csproj`, one `PayDbContext`. Folders are jobs, not Hub modules. Namespaces follow folders. `Program.cs` is the composition root (`Map*`, DI). Do not add `IEnumerable<IHostedRail>`.

| Folder | Job |
|--------|-----|
| `Hosting/` | `/health`, unversioned `/ready` (Postgres CanConnect), problem JSON |
| `Identity/` | One HTTP client, whoami, org ready, One webhooks |
| `Credentials/` | PUT/GET `/v1/orgs/{id}/gateway`, list `GET /v1/orgs/{id}/gateways` |
| `Rails/` | one folder per PSP (`CreateHostedUrl` + webhook parse) |
| `Webhooks/` | shared Plane B pipeline (verify → unique event → fulfill TX) |
| `PublicPay/` | buyer GET/start (no Bearer) |
| `Money/` | fulfill + Official Receipt; `Queries/` merchant reads |
| `Catalog/`, `Checkouts/`, `Secrets/`, `Data/` | products, merchant mint, wrap, EF |

A sixth hosted rail is `Rails/Foo/` plus two switch arms and tests under `tests/.../Rails/Foo/`. New verbs (refunds, pause mail, PDF) get their own folder; they do not hang extra methods on `IHostedRail`. Tests mirror `src/` except `IsolationTests.cs` at the test root.

```bash
task pay:test
task pay:dev          # :8081 health, whoami, checkouts
task pay:merchant     # :5178 staff shell
task pay:checkout     # :5179 hosted pay page
# or
pnpm --filter lazuar-pay dev
```

TypeSpec: [`packages/pay-spec`](../../packages/pay-spec/) (`task pay:spec`).

Pay images live in `docker-compose.pay.yml` (`--profile apps` for 8081 + two Vite apps) and `docker buildx bake pay`. Production must set `Pay__CorsOrigins` and `VITE_PAY_API_URL` / `VITE_CHECKOUT_ORIGIN` to public HTTPS.

## Live Solana Pay (devnet, not CI)

Not `task pay:test`. Not mainnet.

1. Set `Pay__Solana__Cluster=devnet` and `Pay__Solana__RpcUrl` to a Helius (or equivalent) **devnet** HTTPS URL in gitignored `.env`.
2. Merchant Processor (`:5178`): paste a **devnet** receive address, environment `devnet`.
3. Mint a checkout or pay link `provider=solana` `currency=USDC`.
4. Open `pay_url` on `:5179`. Occupancy starts on Pay. Scan the Solana Pay QR.
5. Wallet on **devnet**. Pay Circle devnet USDC (`4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU`).
6. Host poller or `POST /v1/pay/{token}/confirm` with the signature. GET `status=paid`, Official Receipt `RCPT-`, Plane C, `examples/pay-node` unlock.
7. `rg @solana examples/pay-node` stays empty.

### Devnet proof

Five devnet USDC payments through the full loop (Solana Pay QR → poller → Official Receipt `RCPT-` → `payment.completed` HMAC to `examples/pay-node`):

- sig-1
- sig-2
- sig-3
- sig-4
- sig-5

Explorer: `https://explorer.solana.com/tx/<signature>?cluster=devnet`.

The Solana confirm poller is in-process. It pages open QRs and claims rows (`WatchClaimedAt`, `FOR UPDATE SKIP LOCKED` on Postgres). Still prefer one replica: two processes can both run the worker.

Receive-only: Pay cannot claw back USDC. Merchant refunds are `refund not supported on this rail`. Late pay does not book a fake succeeded refund. An open Solana checkout older than the 30-minute reservation TTL is marked `failed` (`watch_timeout`), not paid.

## Live whoami (not CI)

Run **One** (API 8080, login 5175) and **Pay** (8081).

Fingerprint One: `GET http://localhost:8080/api/v1/` should name `lazuar-one-api` (Hub `/health` can also look like `{status:ok}`).

Log in at `http://localhost:5175` (product login). Demo user is whatever One README lists (often `ada@acme.test` / `Password1!`). Copy the **access_token**, not the `id_token`.

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami
# no header → 401
```

Create a workspace in **lazuar-app** (`:5174`) first if `tenants` is empty, then:

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"org_id":"'"$ORG_ID"'","amount":10.00,"currency":"MYR","provider":"stripe","success_url":"https://example.test/ok","cancel_url":"https://example.test/no"}' \
  http://localhost:8081/v1/checkouts
# GET /v1/checkouts/{id} with the same Bearer
```

Second apps mint a One API key (shown once; never `VITE_*`; never git) and send it as Pay Bearer. Scopes `tenant:read` and `authz:check`. Not Stripe `sk_live_`, not `whsec_`. `$ORG_ID` is the One tenant id. Merchant SPA still sends a human JWT only.

```bash
curl -sS -X POST "$ONE/tenants/$ORG_ID/api-keys" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"second-app-cashier","scopes":["tenant:read","authz:check"]}'
# copy secret once (lzr_sk_…)

curl -sS -H "Authorization: Bearer $PAY_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"org_id":"'"$ORG_ID"'","amount":10.00,"currency":"MYR","provider":"test"}' \
  http://localhost:8081/v1/checkouts
```

Pay does not mint keys and does not hold the merchant’s `lzr_sk_` in process env. A hosted **job** may set `One:ApiKey` (`lzr_sk_` only) plus `One:WorkerOrgId` for **that one tenant**. Stripe/Hub `sk_live_` in that slot fails boot. Interactive `/v1` doors still 401 without a request Bearer even when the env key is set. `OneClient` never copies the env key onto `DefaultRequestHeaders`.

Lists (`/v1/orgs/{orgId}/checkouts|payment-links|products|payments|receipts|refunds|subscriptions`) return `{ items, next_cursor }` with `limit` (default 50, max 100) and `after`. There is no `/v2`.

Writer `POST /v1/orgs/{orgId}/refunds` reverses the journal and issues `REF-…` (never `RCPT-`). Plane C `refund.created` fires after that writer. Late PSP pay on an expired reservation is refunded at the processor and **not** fulfilled (occupancy). Recurring intervals (`mo`/`yr`) are refused with 400 — recurring billing is not offered (plans/031/01); the subscriptions table and list endpoint remain for a future implementation, and Pay does not emit `subscription.*` webhooks.

Pending settlements self-heal (plans/031/02): a settle worker re-attempts stripe late-pay refunds inside the 24 h idempotency-key window (5 retries, then manual), emits Plane C `refund.created` on settlement, and publishes `refunds_pending` metrics. Rows past the window or on rails without refund APIs exit via writer `POST /v1/orgs/{orgId}/refunds/{id}/resolve`.

Retention (plans/031/03): a daily sweep prunes `psp_webhook_events` / `one_webhook_events` / `org_webhook_deliveries` / `audit_events` older than `Pay:Retention:*Days` (90/90/180/730 defaults, `0` disables a table, `Pay:Retention:BatchSize` bounds each delete). Ledger, documents, and idempotency keys are never pruned.

Writer `PUT /v1/orgs/{orgId}/webhooks` registers the URL Pay POSTs after a paid fulfill (`payment.completed`, plus `payment.failed` / `checkout.expired` / `refund.created` when those writers run). That `whsec_` is **Pay signing for your app** (One dialect: `X-Lazuar-Signature: v1=` + `X-Lazuar-Timestamp`). It is not Stripe’s vault secret and not One inbound `PUT …/one-webhook`. GET never echoes the secret. Merchant **Webhooks** is “Pay will POST here; you verify”; Processor vault is “Stripe signs; Pay verifies”. Testing allows loopback URLs. Sample: `examples/pay-node` (port **3021**, still `fetch`, not `@repo/pay-types-ts`). Generated types: `packages/pay-types-ts` from `packages/pay-spec`.

Checkouts persist in Postgres `lazuar_pay` on **5435**. `owner`/`admin` paste keys **per rail** (stripe, chip, billplz, xendit, razorpay). Saving a vault does not pick a default. Mint a pay link with an explicit `provider` that already has keys. Capability is `hosted_link`. A verified PSP webhook writes an Official Receipt `RCPT-…` and a two-line journal. Pay does not compute SST or file e-invoices. Buyers have no One account (`:5179/c/{token}`).

Per-org `webhook_secret` (Stripe `whsec_`, CHIP PEM, Billplz X-Signature, Xendit callback token, Razorpay HMAC). Process `Pay__StripeWebhookSecret` is a **Testing-only** fallback. Billplz needs `Pay__PublicBaseUrl` as public **https** (localhost callbacks 400). Buyer return URLs use `Pay__CheckoutBaseUrl` (not the Billplz callback). `Pay__WrapKey` is required outside Testing. A second `POST /v1/pay/{token}/start` on an open checkout returns the stored hosted URL (no second processor session). Success URL is not paid; `:5179` polls `?status=verifying`.

Pay never holds a Zitadel PAT. Staff **VIEWER** is not a One tenant role (`owner` / `admin` / `member` only); `/v1/orgs/{orgId}/ready` checks `member` and then whether the shop can take money (not `charges_paused`, plus a vault row or Test in Dev/Testing). `POST /v1/checkouts` requires writer. Unversioned `GET /ready` is a host probe, not org ready. One pause/reactivate HMAC is per-org `PUT /v1/orgs/{orgId}/one-webhook`; process `Pay__OneWebhookSecret` is the one-shop fallback. Ops must register Pay’s public `https://…/v1/one/webhooks` on One (SSRF blocks loopback; use a tunnel on a laptop) and PUT the shown-once `whsec_` into Pay. Pay does not POST One `/tenants/{id}/webhooks`.
