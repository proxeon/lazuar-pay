# H14 — PSP amount vs checkout amount

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3  
**IDs:** NP-MON-001  
**Goal:** Do not book `checkout.Amount` if Stripe says a different capture.

---

## H14.1 Stripe

- [ ] Compare `session.AmountTotal` (minor units) to `checkout.Amount` × 100 with `MidpointRounding.AwayFromZero` (same as `StripeHosted`)
- [ ] Mismatch → **do not fulfill**; 400 or 200 `{ ignored: "amount_mismatch" }` — pick 400 so Stripe retries only if we also **do not** consume unique-as-paid
- [ ] Prefer: do not insert paid unique on mismatch (or insert ignored id) so a corrected event can still pay
- [ ] Currency: `session.Currency` vs `checkout.Currency` (case-insensitive). Missing currency → refuse, do not default MYR

## H14.2 Later rails

- [ ] CHIP `purchase.total` cents; Billplz `paid_amount` cents; Xendit `paid_amount`; Razorpay `amount` cents — same rule when those handlers land
- [ ] Do not implement those compares in this phase except as a shared helper if cheap

## H14.3 Must not

- [ ] Do not silently book checkout amount when PSP amount is 0 (H20)
- [ ] Do not convert with truncate for Stripe (Hub `ToMinorUnits` is AwayFromZero)

## H14.4 Exit

- [ ] Hermetic: `amount_total` 999 vs checkout 10.00 does not mint `RCPT-`
- [ ] Unblocked for H19
