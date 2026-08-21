# G22 — Setup / zero / skip_capture is not paid

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §0.1 / §2.3 / §2.4  
**IDs:** NP-GW-008  
**Goal:** Never fulfill vault noise. `NP-GW-008`.

---

## G22.1 Do not call fulfill

- [ ] Stripe `setup_intent` / Checkout `mode=setup` → **not** paid
- [ ] `amount <= 0` → **not** paid (no `RCPT-`, no seat)
- [ ] CHIP `skip_capture` **without** a token → **not** paid
- [ ] CHIP `purchase.preauthorized` (even with token) is **vaulted**, not captured — do not copy Hub `PAYMENT_COMPLETED`

## G22.2 HTTP

- [ ] Verified setup/vault → **200** with `vaulted` / ignored — **not** 400 retry storm
- [ ] Do not mint session `paid`. Do not call F10 fulfill

## G22.3 Test

- [ ] Fixture payload for setup-intent **or** amount 0 **or** skip_capture-without-token: fulfill **not** called
- [ ] Hermetic. This commit or G25

## G22.4 Exit

- [ ] `NP-GW-008` may move when the test is green
- [ ] Unblocked for G25 / F10 (F17 still owns zero-amount `RCPT-`)
