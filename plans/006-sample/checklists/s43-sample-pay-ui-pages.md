# S43 — Sample pay / success / cancel UI

**Track:** Sample app · **Analysis:** `../03`, `../04`  
**Depends on:** S42  
**Goal:** Human-visible demo path that teaches anti-patterns.

---

## S43.1 Pay page (`/pay`)

- [ ] Form or defaults: amount, email, description
- [ ] Submit → `POST /api/checkout` then redirect browser to `checkout_url`
- [ ] Loading / error display for Hub failures
- [ ] Link to orders list

## S43.2 Success page (`/pay/success`)

- [ ] Read order id from query and/or cookie
- [ ] **Do not** set status paid on page load
- [ ] Copy: waiting for webhook / processing; success_url is not fulfillment
- [ ] Optional poll local order status (or Hub GET checkout with disclaimer)
- [ ] Show paid only when store says paid

## S43.3 Cancel page (`/pay/cancel`)

- [ ] Clear cancelled messaging
- [ ] Does not mark paid
- [ ] Optional: mark local order cancelled if still draft/open

## S43.4 Teaching UI

- [ ] Visible “Sample · not production” badge
- [ ] Short note: domain stays here; money rails on Hub

## S43.5 Exit

- [ ] Manual browser walk-through of pay → cancel without Hub pay works
- [ ] Success page alone never unlocks
