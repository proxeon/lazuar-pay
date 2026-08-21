# Parked — P60 old ops / portal (refuse)

**Do not retarget in 013.**  
**Analysis:** [012 P60](../../012-one-to-pay/checklists/p60-old-frontends.md), [01](../01-production-ready-bar.md) §5, [04](../04-merchant-frontend.md), [05](../05-checkout-frontend.md)

---

- [ ] `lazuar-ops` `VITE_API_URL` stays Hub `http://localhost:8080/api/v1`
- [ ] `lazuar-portal` same
- [ ] Do not add `:3003` or `:3004` to Pay CORS
- [ ] `@repo/api-types-ts` stays Hub’s spec
- [ ] New UIs are `:5178` and `:5179` only
