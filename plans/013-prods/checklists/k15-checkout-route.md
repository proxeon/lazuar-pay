# K15 — Vite `/c/{token}`

**Track:** Buyer page · **Depends:** K11  
**Analysis:** [05](../05-checkout-frontend.md) §2, §8  
**Goal:** Hosted cash register route on `:5179`. Loads public GET. NP-CHK-005/006 pixel.  
**011:** NP-CHK-005, NP-CHK-006

---

## K15.1 Route

- [ ] `apps/lazuar-pay-checkout` route `/c/{token}` loads `GET /v1/pay/{token}` (K11 DTO)
- [ ] `VITE_PAY_API_URL` default `http://localhost:8081` — **not** Hub `/api/v1`
- [ ] Document title stays **Lazuar Pay — checkout** (`index.html`)
- [ ] Port **5179** `strictPort` (package.json + vite.config dual-pin). Never steal 5178/5175

## K15.2 Fetch

- [ ] No `Authorization` header
- [ ] No `credentials: "include"`
- [ ] Missing token / 404 → missing state (K16 may flesh copy)

## K15.3 Must not

- [ ] Not `lazuar-portal` `:3004`, not `/{tenantSlug}/checkout/{productSlug}`
- [ ] Not `GET /v1/whoami` as a required step
- [ ] Not retarget portal `NEXT_PUBLIC_API_URL` at 8081

## K15.4 Exit

- [ ] Opening `/c/{token}` for a real token paints amount/status from public GET
- [ ] Unblocked for K16, K17, K21, K22
