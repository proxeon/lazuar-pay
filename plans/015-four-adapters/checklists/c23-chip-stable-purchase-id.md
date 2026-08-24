# C23 — Stable CHIP purchase id

**Track:** CHIP · **Depends:** C19  
**Analysis:** Hub `ReadStablePurchaseId`  
**IDs:** NP-GW-006  
**Goal:** Nested `purchase.id` then root `id`. Missing → unusable, no fulfill.

---

## C23.1

- [ ] Steal Hub order: `purchase.id` if object, else root `id`
- [ ] Missing → 400 (or 200 ignored **without** unique-as-paid — prefer 400 unusable so CHIP retries)
- [ ] Persist as `ProviderRef` / `ProviderSessionId`

## C23.2 Must not

- [ ] Do not invent a Guid event id
- [ ] Do not use `EventId = checkout.Id`

## C23.3 Exit

- [ ] Helper covered by C19 fixture using nested id
