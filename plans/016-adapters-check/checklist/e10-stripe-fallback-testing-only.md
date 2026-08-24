# E10 — Stripe process `whsec_` fallback is Testing only

**Track:** Env secrets · **Depends:** A00  
**Analysis:** live `StripeWebhook.ResolveSecret` `if (!env.IsProduction())`; P0-E  
**IDs:** H11  
**Goal:** Development must not verify every empty-ciphertext org with the platform secret.

---

## E10.1 Live today

- [ ] Row ciphertext if present
- [ ] Else if **not Production** → `Pay:StripeWebhookSecret`
- [ ] Else null → 503

## E10.2 Change

- [ ] Else if `env.IsEnvironment("Testing")` → process env
- [ ] Development / Staging / anything else → null (503) when row empty
- [ ] Production unchanged (already 503)

## E10.3 Must not

- [ ] Do not add process fallback for CHIP PEM / Billplz / Xendit / Razorpay
- [ ] Do not read `Pay:StripeWebhookSecret` in Production

## E10.4 Exit

- [ ] Unblocked for E11, E12, E16
