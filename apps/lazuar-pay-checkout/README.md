# Lazuar Pay — checkout

Hosted buyer pay page for focused Pay. Not `lazuar-portal` (`:3004`).

| | |
|---|---|
| Origin | `http://localhost:5179` (`strictPort`) |
| API | focused Pay `VITE_PAY_API_URL` |

`VITE_PAY_API_URL` is the public Pay origin (8081 locally). It is **not** a secret. Dev falls back to `http://localhost:8081` when the env is unset. Production `pnpm build` **fails** if it is missing — do not default a shipped pixel to localhost.

Copy `.env.example` for laptop dogfood. Strip trailing slashes; a trailing `/` would double-slash `/v1/pay`.

Buyers have **no** One account. Fail if this page asks for Zitadel login. Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.

Do not commit `dist/`. `vite preview` only after a fresh `pnpm build` with `VITE_PAY_API_URL` set.

```bash
task pay:checkout
# or
pnpm --filter lazuar-pay-checkout dev
```
