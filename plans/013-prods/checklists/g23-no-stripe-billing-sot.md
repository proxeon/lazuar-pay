# G23 — No Stripe Billing as source of truth

**Track:** Rails · **Depends:** G16  
**Analysis:** [06](../06-money-rails.md) §0.1 / §2.3 / §9  
**IDs:** NP-XX-012 (refuse)  
**Goal:** Keep `NP-XX-012` refuse. Checkout payment mode only.

---

## G23.1 Grep (must stay clean)

- [ ] Do **not** subscribe `customer.subscription.updated` (or `invoice.paid`) as paid
- [ ] Do **not** create Stripe Checkout `mode=subscription`
- [ ] Paid events: `checkout.session.completed` and/or `payment_intent.succeeded` for **`mode=payment` only**
- [ ] `setup_intent.succeeded` is G22 vault, not cash

## G23.2 If G10 is CHIP

- [ ] Still grep: no Stripe Billing strings treated as fulfill
- [ ] CHIP `purchase.paid` is money; `preauthorized` is not (G22)

## G23.3 Must not

- [ ] No Stripe Billing Portal as v1
- [ ] Pay’s billing job (later) mints a checkout or off-session charge — **not** Stripe Subscription objects

## G23.4 Exit

- [ ] `NP-XX-012` stays **refuse** (do not flip to done)
- [ ] Unblocked for G24
