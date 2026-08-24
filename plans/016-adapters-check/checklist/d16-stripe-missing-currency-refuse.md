# D16 — Stripe missing currency does not skip the compare

**Track:** Units · **Depends:** A00  
**Analysis:** [`../04-stripe-crosscheck.md`](../04-stripe-crosscheck.md) §7.4; live `Currency = null` skips handler check  
**IDs:** H14  
**Goal:** Hub refused to invent MYR. Pay must too.

---

## D16.1 Live today

- [ ] `TryNormalizeCurrency` failure → `Currency = null`
- [ ] Handler: `if (parsed.Currency is not null && …)` — skip

## D16.2 Change

- [ ] Paid `checkout.session.completed` with unusable/missing currency → `PspVerifyException("missing currency")` **or** handler 400 `"currency mismatch"` when `parsed.Currency is null` on a **paid** (non-ignored) parse
- [ ] Setup/zero ignore may still have null currency — those are ignored before the compare

## D16.3 Must not

- [ ] Do not default `myr`
- [ ] Do not skip only for Stripe

## D16.4 Exit

- [ ] `WebhookTests.Currency_missing_does_not_mint_receipt` — signed session without currency / `currency:""` → 400, zero docs, no event row
