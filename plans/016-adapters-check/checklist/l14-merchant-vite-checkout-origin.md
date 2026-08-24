# L14 — Merchant `VITE_CHECKOUT_ORIGIN`

**Track:** Checkout origin · **Depends:** L10  
**Analysis:** `WorkspacePage` `http://localhost:5179/c/${token}`  
**IDs:** —  
**Goal:** Minted share link uses the same origin buyers open.

---

## L14.1

- [ ] `VITE_CHECKOUT_ORIGIN` default `http://localhost:5179` (no trailing slash)
- [ ] Pay URL = `${origin}/c/${public_token}`
- [ ] `.env.example` documents it
- [ ] Not a secret

## L14.2 Must not

- [ ] Do not put `VITE_*` API keys
- [ ] Do not point at Hub `:3004`

## L14.3 Exit

- [ ] Unblocked for M18
