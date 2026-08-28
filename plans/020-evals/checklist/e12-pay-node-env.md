# E12 — Sample env

**Track:** E · **Depends:** E11, M22  
**Goal:** Second app holds **its** key. Pay holds none of it.

**Why:** Mixing `VITE_` (browser) with `lzr_sk_` leaks the key. Pay `.env` must not grow the merchant’s secret.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/.env.example` | Pay process env — no merchant key |
| M22 README hatch | One mint curl |
| W14 / W24 | Register loopback URL |
| `examples/hub-cashier-next` env | Hub `sk_` — do not copy names |

**Current (`6d730d15`):** N/A.

---

## E12.1

- [x] `.env.example`: `PAY_API_URL=http://localhost:8081`, `PAY_ORG_ID=`, `PAY_API_KEY=lzr_sk_…`, `PAY_WEBHOOK_SECRET=whsec_…`
- [x] Never `VITE_` for the key
- [x] Document One mint curl (or “paste from lazuar-app”)
- [x] Document PUT Pay `/v1/orgs/{orgId}/webhooks` with sample’s public URL (Testing loopback)

## E12.2 Exit

- [x] Unblocked for E13
