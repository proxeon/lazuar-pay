# D19 — Checkout default MYR is not a webhook default

**Track:** Units · **Depends:** D15, D16  
**Analysis:** `CheckoutTests.Create_defaults_currency_to_myr`  
**IDs:** —  
**Goal:** Do not “fix” Hub-style invent-MYR on Plane B.

---

## D19.1 Keep

- [ ] Checkout create may default currency to MYR when omitted (existing)
- [ ] Merchant SPA always sends MYR

## D19.2 Must not

- [ ] Do not set `parsed.Currency = "MYR"` in any `*Webhook`
- [ ] Do not add Hub `ToMinorUnits` JPY table in this program unless A00 is amended for non-MYR dogfood

## D19.3 Exit

- [ ] Grep `*Webhook.cs` for `"MYR"` assignment is empty after D15
