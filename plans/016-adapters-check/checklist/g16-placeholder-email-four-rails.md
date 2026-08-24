# G16 — Placeholder email 400 on four rails (index)

**Track:** Prove Beat 1 · **Depends:** A00  
**Analysis:** P20; 09 fc16/fb17/fx17/fr18; grep tests for `customer@example.com` is empty  
**IDs:** P20  
**Goal:** Host already refuses. Tests must send the Hub placeholder.

---

## G16.1 Methods live in F

- [ ] `RailTests.Chip_placeholder_email_is_400` (fc16)
- [ ] `RailTests.Billplz_placeholder_email_is_400` (fb17)
- [ ] `RailTests.Xendit_placeholder_email_is_400` (fx17)
- [ ] `RailTests.Razorpay_placeholder_email_is_400` (fr18)
- [ ] Each: start `{"name":"Ada","email":"customer@example.com"}` → 400, `Psp.LastUri` null

## G16.2 Must not

- [ ] Do not invent a different placeholder
- [ ] Stripe may stay optional — do **not** 400 Stripe on placeholder unless you also refuse it in `BuyerEmail` for Stripe (out of scope)

## G16.3 Exit

- [ ] Four methods green (tick when those F files exit)
