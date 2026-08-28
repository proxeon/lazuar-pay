# M23 — Merchant SPA still JWT-only

**Track:** M · **Depends:** M14  
**Analysis:** [`../08-headless-vs-spa.md`](../08-headless-vs-spa.md); pickApiBearerToken  
**Goal:** Staff chrome does not start stuffing `lzr_sk_` into sessionStorage.

**Why:** Merchant OIDC `pickApiBearerToken` already rejects non-JWT. A “helpful” M2M PR must not add `VITE_PAY_API_KEY` or send opaque tokens. Staff stay humans.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay-merchant/src/auth/bearerToken.ts` | `isJwtLike` / `pickApiBearerToken` |
| `apps/lazuar-pay-merchant/src/pages/HomePage.tsx` | Uses picker |
| `apps/lazuar-pay-merchant/src/lib/payApi.ts` | `Authorization` header |
| Merchant vitest next to `bearerToken` (add if missing) | Must reject `lzr_sk_…` |

**Current (`6d730d15`):** JWT-only picker. No env key.

---

## M23.1

- [x] `pickApiBearerToken` (or equivalent) still rejects non-JWT / `lzr_sk_`-shaped strings
- [x] Vitest: opaque token not sent as Authorization
- [x] No `VITE_PAY_API_KEY`

## M23.2 Must not

- [x] Do not add an API-keys page that mints Hub `payments.checkouts:*`
- [x] A later “open One settings” link is allowed; store stays One

## M23.3 Exit

- [x] Track M complete when M10–M23 checked
