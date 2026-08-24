# I17 — Remove Start `_ => stripe`

**Track:** Idempotent start · **Depends:** A00  
**Analysis:** [`../01-new-host-seams.md`](../01-new-host-seams.md) §15.2  
**IDs:** —  
**Goal:** A sixth allow-list name must not silently charge Stripe.

---

## I17.1 Live today

- [ ] `PublicPayEndpoints.Start` switch ends `_ => stripe`
- [ ] Unreachable while `TryNormalize` only allows five names — still a footgun

## I17.2 Change

- [ ] Default arm throws `InvalidOperationException("rail not configured")` **or** is omitted because the switch is exhaustive on `PayProviders.*`
- [ ] Do not add a sixth name here (parked-sixth-rail)

## I17.3 Exit

- [ ] Grep of `PublicPayEndpoints.cs` has no `_ => stripe`
