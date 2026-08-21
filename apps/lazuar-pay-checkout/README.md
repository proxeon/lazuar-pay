# Lazuar Pay — checkout

Hosted buyer pay page for focused Pay. Not `lazuar-portal` (`:3004`).

| | |
|---|---|
| Origin | `http://localhost:5179` (`strictPort`) |
| API | focused Pay `http://localhost:8081` (`VITE_PAY_API_URL`) |

Buyers have **no** One account. Fail if this page asks for Zitadel login. Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.

```bash
task pay:checkout
# or
pnpm --filter lazuar-pay-checkout dev
```
