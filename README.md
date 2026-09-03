# Lazuar Pay

Checkout-as-a-service for **other apps**. Your frontend stays yours. Pay mints a hosted link, takes the money, books an Official Receipt, and POSTs a signed webhook. Identity is sibling **lazuar-one** (private).

Built by [Akmal Firdaus](https://x.com/akmalfirdxus) — solo, Malaysia.

![Lazuar Pay merchant — the Processor page, one card per rail: Test, Stripe, CHIP, Billplz, Xendit, Razorpay, Solana](assets/merchant-processor.png)

**Focused Pay** (this repo’s cashier) is `apps/lazuar-pay` on **8081**, merchant **:5178**, checkout **:5179**. One API is **8080**.

Integrators: [`apps/lazuar-pay/README.md`](apps/lazuar-pay/README.md) and [`examples/pay-node`](examples/pay-node).

## For app developers

A second app does not pick a PSP, import a wallet SDK, or talk to Stripe/Solana directly.

1. Mint a One workspace key (`lzr_sk_`) with `tenant:read` + `authz:check` — Pay merchant **Developers → API keys**, or One Settings. Shown once. Never `VITE_*`.
2. `PUT /v1/orgs/{orgId}/webhooks` with the HTTPS URL Pay will POST to. Copy `whsec_` once. Verify `X-Lazuar-Signature`.
3. `POST /v1/orgs/{orgId}/payment-links` (or `/v1/checkouts`) with `provider` and amount. Send `Authorization: Bearer lzr_sk_…`.
4. Send the buyer `pay_url` (`{CheckoutBaseUrl}/c/{token}`).
5. Unlock on Plane C `payment.completed` (also `payment.failed` / `checkout.expired` / `refund.created` when those writers run).

`$ORG_ID` is the One tenant id. The merchant SPA still uses a human JWT only. Pay does not mint `lzr_sk_` and does not mint Hub/Stripe `sk_`. Sample: `examples/pay-node` (port **3021**).

## Rails

| Provider | Today | Buyer |
|----------|--------|--------|
| `test` | Dev/Testing. No secrets. Pay marks the link paid. | Hosted `:5179` |
| `stripe` | Hosted Checkout. Cards on Stripe. | Stripe page |
| `chip` | Hosted CHIP (FPX/wallets if enabled on the brand). Paste PEM. | CHIP page |
| `billplz` | Hosted bill. Public **https** callback. | Billplz page |
| `xendit` | Hosted invoice. | Xendit page |
| `razorpay` | Hosted payment link. Not e-mandate. | Razorpay page |
| **`solana`** | Solana Pay QR, USDC only. Same mint / `pay_url` / HMAC as the rows above. The integrating app still does not import a wallet SDK. Production needs `Pay__Solana__Cluster=mainnet-beta` and a paid HTTPS RPC. | Checkout QR |

Saving a vault does **not** pick a default rail. Mint with an explicit `provider` that already has keys. Occupancy (how many people can start Pay) is a pay-link field, not a rail. Buyers have no One account.

**Honest capability today:** BYOK hosted PSP links + Test, Official Receipt `RCPT-…` (not a MyInvois tax invoice), two-line journal, Plane C HMAC to your app. No proven sandbox MyInvois `VALID`. No FPX e-mandate. No Bitcoin. No Pay.sh / x402 agent CLI.

## Ports

| Process | Port | Job |
|---------|------|-----|
| One API | 8080 | Identity. Not Pay. Fingerprint: `GET /api/v1/` names `lazuar-one-api`. |
| Pay | **8081** | Money host |
| One login | 5175 | Staff sign-in |
| One app | 5174 | Workspace, full API-key catalog |
| Pay merchant | **5178** | Processor, pay links, Developers (API keys + webhooks) |
| Pay checkout | **5179** | Buyer `/c/{token}` |
| `examples/pay-node` | 3021 | Second-app hatch |
| Pay Postgres | 5435 | `lazuar_pay` |

```bash
task pay:test
task pay:dev          # :8081
task pay:merchant     # :5178
task pay:checkout     # :5179
```

TypeSpec: [`packages/pay-spec`](packages/pay-spec/) (`task pay:spec`). Generated TS: `packages/pay-types-ts`.

Pay images: `docker-compose.pay.yml`. Production must set `Pay__CorsOrigins` and `VITE_PAY_API_URL` / `VITE_CHECKOUT_ORIGIN` to public HTTPS.

## Layout

```
apps/lazuar-pay/              focused host (.NET, :8081)
apps/lazuar-pay-merchant/     staff SPA (:5178)
apps/lazuar-pay-checkout/     buyer page (:5179)
examples/pay-node/            integrator sample (:3021)
packages/pay-spec/            Pay TypeSpec
packages/pay-types-ts/        generated from pay-spec
```

## Solana Pay (USDC)

`provider=solana` mints a Solana Pay transfer-request QR on the hosted checkout. Receive-only: funds land on the merchant receive address; Pay cannot claw back. Confirm is a reference-key + memo match at `finalized` commitment, then Official Receipt + the same Plane C `payment.completed` HMAC as every other rail. Devnet dogfood and proof signatures: [`apps/lazuar-pay/README.md`](apps/lazuar-pay/README.md).
