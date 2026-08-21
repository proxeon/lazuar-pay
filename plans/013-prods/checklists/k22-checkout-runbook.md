# K22 — Checkout runbook (docs only)

**Track:** Buyer page · **Depends:** K15, K17  
**Analysis:** [05](../05-checkout-frontend.md) §8; host README pattern (C19)  
**Goal:** A human can open `/c/{token}` without One login. Hub portal **3004** is not involved.

---

## K22.1 Write into `apps/lazuar-pay-checkout/README.md` (and/or host README)

- [x] Open `http://localhost:5179/c/{token}` with **no** One account
- [x] Merchant mints the token via Bearer `POST /v1/checkouts` (curl or `:5178`) — buyer does not
- [x] Pay host **8081**; checkout **5179** `strictPort`; Hub `lazuar-portal` **3004** **off** / not in the path
- [x] Fail lock: if the page asks for Zitadel / `:5175` / password, the slice failed

## K22.2 Must not document

- [x] Do not tell buyers to log in on `:5175` or `:5173`
- [x] Do not point portal `NEXT_PUBLIC_API_URL` / ops `VITE_API_URL` at 8081
- [x] Do not use Hub `/{slug}/checkout/{product}` as the Bar B URL

## K22.3 Exit

- [x] README updated
- [x] Buyer track complete pending B99 (G/F still own capture + `paid`)
- [x] Unblocked for B99 K cells
