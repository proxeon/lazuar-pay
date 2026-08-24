# Y11 — Checkout with null Provider is not payable by a random rail

**Track:** Webhook rail bind · **Depends:** Y10  
**Analysis:** Checkout create does not set Provider (015: set at start)  
**IDs:** —  
**Goal:** A webhook that arrives before start, or with a forged checkout id, cannot pick a rail.

---

## Y11.1

- [ ] If `checkout.Provider` is null/whitespace → 400 (same as mismatch)
- [ ] Start is what stamps provider. Plane B without a start is not cash

## Y11.2 Must not

- [ ] Do not infer provider from `active_provider` for fulfill
- [ ] `active_provider` remains the **start** dispatch key only

## Y11.3 Exit

- [ ] Hermetic: mint checkout, **no** start, POST signed Stripe completed with that checkout id → 400, zero documents
