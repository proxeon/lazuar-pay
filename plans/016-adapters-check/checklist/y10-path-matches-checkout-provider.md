# Y10 — Webhook path provider must match checkout.Provider

**Track:** Webhook rail bind · **Depends:** A00  
**Analysis:** [`../01-new-host-seams.md`](../01-new-host-seams.md) §15.2 leftover credentials  
**IDs:** H13 family  
**Goal:** Switching active rail must not leave the old Plane B able to pay that checkout.

---

## Y10.1 Live today

- [ ] Handler binds `checkout.OrgId == path orgId`
- [ ] Does **not** compare `checkout.Provider` to path `{provider}`
- [ ] PUT always flips `active_provider` but old `gateway_credentials` rows remain
- [ ] Start writes `checkout.Provider`

## Y10.2 Change

- [ ] After loading checkout, if `checkout.Provider` is non-whitespace and ≠ path `name` → 400 `"checkout not found"` or `"provider mismatch"` (pick one string and test it)
- [ ] Do this **before** amount match and **before** unique insert
- [ ] Org bind stays

## Y10.3 Must not

- [ ] Do not delete old credential rows on PUT
- [ ] Do not fulfill using leftover Stripe events for a CHIP-started checkout

## Y10.4 Exit

- [ ] Unblocked for Y11, Y12
