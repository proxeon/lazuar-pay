# K10 — `GET /v1/pay/{token}` (no Bearer)

**Track:** Buyer page · **Depends:** D17, B00 (public pay identifier)  
**Analysis:** [05](../05-checkout-frontend.md) §3  
**Goal:** Buyer-safe read. Do **not** ungated merchant GET. NP-CHK-005/006 need this door.  
**011:** NP-CHK-005, NP-CHK-006

---

## K10.1 Route

- [x] `GET /v1/pay/{token}` — **no** `Authorization` required
- [x] Confirm B00 lock: identifier is `token` on `/v1/pay/{token}` (paper 05 option B)
- [x] **Keep** `MemberGate` on `GET /v1/checkouts/{id}` — do **not** drop it to ship the page
- [x] **Not** Hub `GET /public/commerce/...`

## K10.2 Mint

- [x] On merchant `POST /v1/checkouts` (extend fixture): mint `public_token`, persist on D17 checkout row
- [x] Token is **unguessable** (128-bit+ / crypto-random; not sequential, not `org_id`, not product slug)
- [x] Shareable URL shape is later K15 (`/c/{token}`); this phase at least stores the token

## K10.3 Auth

- [x] Missing Bearer is **success path**, not 401
- [x] Do not call One / `MemberGate` for this GET
- [x] Do not call `GET /v1/whoami` as a prerequisite

## K10.4 Must not

- [x] No Pay password, no cookie JWT, no buyer as Zitadel human
- [x] No opening merchant GET “temporarily”

## K10.5 Exit

- [x] GET by token returns 200 for an open session (DTO may still be stubby if K11 is next)
- [x] Unblocked for K11, K12, K13, K14, K20
