# G10 — First rail (Stripe XOR CHIP)

**Track:** Rails · **Depends:** B00  
**Analysis:** [06](../06-money-rails.md) §3 / §10, [011/01](../../011-new-lazuar-pay/01-product.md)  
**IDs:** NP-GW-002 XOR NP-GW-003  
**Goal:** Name one dogfood rail. `NP-GW-002` XOR `NP-GW-003`.  
**No product code.**

---

## G10.1 Evidence

- [ ] 011/01 dogfood: merchant pastes **CHIP or Stripe** keys — Billplz is not named
- [ ] Billplz is **reminder-only** (hosted link, never silent debit). Not first rail unless you accept that
- [ ] [06](../06-money-rails.md) §10: do not default to Billplz because Hub cashier did; do not implement both “just in case”

## G10.2 Write the name

- [ ] [`decisions.md`](./decisions.md) **First rail** = `Stripe` or `CHIP` (one word, not “XOR”, not “CHIP/Billplz”)
- [ ] Human: we picked ____
- [ ] If Stripe: cards; Checkout `mode=payment` (not `subscription`; not setup-as-paid)
- [ ] If CHIP: purchases API + hosted `checkout_url`
- [ ] Do **not** implement both Stripe and CHIP in Bar B
- [ ] Do **not** add Razorpay, Xendit, Billplz, Fiuu, or a factory of five

## G10.3 Tracker

- [ ] `NP-GW-002` XOR `NP-GW-003` — CHIP hosted cards do **not** tick Stripe
- [ ] Do not flip either ID from this file (adapter is G16)

## G10.4 Exit

- [ ] First rail is a single name in `decisions.md`
- [ ] Unblocked for G11 and G15
