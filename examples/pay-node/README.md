# @examples/pay-node

Second-app hatch against focused Pay. Default `PAY_API_URL` is C# `:8081`.
Against the Rust port: `PAY_API_URL=http://localhost:8095`. Not Hub `hub-cashier-next`.

1. Mint a One `lzr_sk_` (`tenant:read`, `authz:check`) — Pay merchant **Developers → API keys**, or One Settings → API keys.
2. `PUT http://localhost:8081/v1/orgs/$ORG_ID/webhooks` with `{"url":"http://127.0.0.1:3021/hook"}` (Testing loopback). Copy `webhook_secret`.
3. Copy `.env.example` → `.env` (`PAY_API_KEY`, `PAY_WEBHOOK_SECRET`, `PAY_ORG_ID`).
4. `pnpm --filter @examples/pay-node start`
5. `POST /mint` → use `pay_url`. Start pay (Test) or open checkout. `POST /hook` verifies One-dialect HMAC (full `whsec_`, `{unix}.{raw body}`). The **host** keeps ±300s timestamp skew; this sample does not enforce skew. `GET /unlocked/:checkoutId` is the toy row.

Solana dogfood: `PAY_PROVIDER=solana` and `PAY_CURRENCY=USDC`. This process still does not import `@solana/*` or talk to an RPC. Unlock stays `payment.completed` + `checkout_id`.

Never put `lzr_sk_` in `VITE_*`. Pay does not POST One `/members`.
