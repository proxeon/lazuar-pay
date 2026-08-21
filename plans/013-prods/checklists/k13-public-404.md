# K13 — Unknown token is 404 (not an auth wall)

**Track:** Buyer page · **Depends:** K10  
**Analysis:** [05](../05-checkout-frontend.md) §4.6  
**Goal:** Public resource: missing and “unauthorized-missing” are the **same** 404. No existence oracle.

---

## K13.1 Status

- [x] Unknown token on `GET /v1/pay/{token}` → **404**
- [x] Unknown token on `POST /v1/pay/{token}/start` → **404** (same class)
- [x] **No 401** (that implies an auth wall / “please log in”)
- [x] **No 403** (that implies the buyer should log in as a member)

## K13.2 Oracle

- [x] Do not fork 401 (exists) vs 404 (missing) on the **public** door
- [x] Merchant `GET /v1/checkouts/{id}` may keep its own 401/404 matrix — do not change it here

## K13.3 Copy / body

- [x] Boring 404 JSON (title/detail). Do not say “sign in” or “use the magic link emailed to you”
- [x] Do not render Hub lock-icon landing as the API error

## K13.4 Exit

- [x] Tests: unknown token GET/POST → 404, never 401/403
- [x] Unblocked for K14 (CORS) and K16 missing pixel
