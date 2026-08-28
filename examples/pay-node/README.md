# @examples/pay-node

Second-app hatch against **focused Pay :8081**. Not Hub `hub-cashier-next`.

1. Mint a One `lzr_sk_` (`tenant:read`, `authz:check`) for the workspace.
2. `PUT http://localhost:8081/v1/orgs/$ORG_ID/webhooks` with `{"url":"http://127.0.0.1:3021/hook"}` (Testing loopback). Copy `webhook_secret`.
3. Copy `.env.example` → `.env` (`PAY_API_KEY`, `PAY_WEBHOOK_SECRET`, `PAY_ORG_ID`).
4. `pnpm --filter @examples/pay-node start`
5. `POST /mint` → use `pay_url`. Start pay (Test) or open checkout. `POST /hook` verifies One-dialect HMAC (full `whsec_`, `{unix}.{raw body}`). `GET /unlocked/:checkoutId` is the toy row.

Never put `lzr_sk_` in `VITE_*`. Pay does not POST One `/members`.
