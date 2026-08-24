# M22 — Still no Vite secrets

**Track:** Merchant · **Depends:** L14  
**Analysis:** U20; new `VITE_CHECKOUT_ORIGIN` is an origin, not a key  
**IDs:** U20  
**Goal:** Do not add `VITE_STRIPE_`, PEM, or wrap keys.

---

## M22.1

- [ ] Allowed public: `VITE_PAY_API_URL`, `VITE_ZITADEL_*`, `VITE_CHECKOUT_ORIGIN`
- [ ] Existing locks still forbid password form / Hub types
- [ ] Do not add `sk_live` / `whsec_` defaults

## M22.2 Exit

- [ ] locks.test still green
