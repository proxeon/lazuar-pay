# M18 — Minted pay URL uses VITE_CHECKOUT_ORIGIN

**Track:** Merchant · **Depends:** L14  
**Analysis:** hardcoded `http://localhost:5179/c/`  
**IDs:** —  
**Goal:** Deployed merchant does not copy laptop links.

---

## M18.1

- [ ] `const checkoutOrigin = import.meta.env.VITE_CHECKOUT_ORIGIN ?? 'http://localhost:5179'`
- [ ] `setPayUrl(`${checkoutOrigin.replace(/\/$/, '')}/c/${body.public_token}`)`

## M18.2 Must not

- [ ] Do not use `VITE_PAY_API_URL` as the pay link origin (8081 is not the SPA)

## M18.3 Exit

- [ ] Source uses the env
