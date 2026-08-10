# S43 — Sample pay / success / cancel UI

**Track:** Sample app · **Analysis:** `../03`, `../04`  
**Depends on:** S42  
**Goal:** Human-visible demo path that teaches anti-patterns.

---

## S43.1 Pay page (`/pay`)

- [x] Form or defaults: amount, email, description
- [x] Submit → `POST /api/checkout` then redirect browser to `checkout_url`
- [x] Loading / error display for Hub failures
- [x] Link to orders list

## S43.2 Success page (`/pay/success`)

- [x] Read order id from query and/or cookie
- [x] **Do not** set status paid on page load
- [x] Copy: waiting for webhook / processing; success_url is not fulfillment
- [x] Optional poll local order status (or Hub GET checkout with disclaimer)
- [x] Show paid only when store says paid

## S43.3 Cancel page (`/pay/cancel`)

- [x] Clear cancelled messaging
- [x] Does not mark paid
- [x] Optional: mark local order cancelled if still draft/open

## S43.4 Teaching UI

- [x] Visible “Sample · not production” badge
- [x] Short note: domain stays here; money rails on Hub

## S43.5 Exit

- [x] Manual browser walk-through of pay → cancel without Hub pay works
- [x] Success page alone never unlocks
