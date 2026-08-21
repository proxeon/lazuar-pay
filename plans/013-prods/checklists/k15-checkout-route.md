# K15 — Vite `/c/{token}`

**Track:** Buyer page · **Depends:** K11  
**Analysis:** [05](../05-checkout-frontend.md) §2, §8  
**Goal:** Hosted cash register route on `:5179`. Loads public GET. NP-CHK-005/006 pixel.  
**011:** NP-CHK-005, NP-CHK-006

---

## K15.1 Route

- [x] `apps/lazuar-pay-checkout` route `/c/{token}` loads `GET /v1/pay/{token}` (K11 DTO)
- [x] `VITE_PAY_API_URL` default `http://localhost:8081` — **not** Hub `/api/v1`
- [x] Document title stays **Lazuar Pay — checkout** (`index.html`)
- [x] Port **5179** `strictPort` (package.json + vite.config dual-pin). Never steal 5178/5175

## K15.2 Fetch

- [x] No `Authorization` header
- [x] No `credentials: "include"`
- [x] Missing token / 404 → missing state (K16 may flesh copy)

## K15.3 Must not

- [x] Not `lazuar-portal` `:3004`, not `/{tenantSlug}/checkout/{productSlug}`
- [x] Not `GET /v1/whoami` as a required step
- [x] Not retarget portal `NEXT_PUBLIC_API_URL` at 8081

## K15.4 Exit

- [x] Opening `/c/{token}` for a real token paints amount/status from public GET
- [x] Unblocked for K16, K17, K21, K22
