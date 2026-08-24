# W18 — `tenant.suspended` sets ChargesPaused

**Track:** One HMAC · **Depends:** W17  
**Analysis:** live handler already has this branch; HMAC never reached it  
**IDs:** —  
**Goal:** After W11–W16, this branch is reachable.

---

## W18.1 Live today (keep)

- [ ] `type == "tenant.suspended"` + org id → insert/update `OrgSettings.ChargesPaused = true`
- [ ] Delivery id dedup on `OneWebhookEvents`

## W18.2

- [ ] Confirm JSON `type` is what One sends (`tenant.suspended`). If live One uses another string, steal **that** string in A00 notes — do not guess
- [ ] Replay same delivery → `{ duplicate: true }`, pause stays true

## W18.3 Must not

- [ ] Do not fulfill or cancel open checkouts in this handler
- [ ] Do not seed `SstRegistered`

## W18.4 Exit

- [ ] W23 happy path sets `ChargesPaused`
