# P19 — Email required on start for CHIP / Billplz / Xendit

**Track:** Provider door · **Depends:** P17  
**Analysis:** [00](../00-what-must-be-done.md) §5; Hub `GatewayCommon.TryResolveEmail`  
**IDs:** NP-BUY-001  
**Goal:** Those APIs refuse placeholder buyers. Stripe may keep optional email.

---

## P19.1

- [ ] After resolving provider, if `chip` | `billplz` | `xendit`: require non-empty trimmed email
- [ ] Missing → 400 `"email is required"`
- [ ] Razorpay: follow R24 (Hub sent customer on payment link — require email)
- [ ] Stripe: email optional (live today)
- [ ] Persist `PayerEmail` / `PayerName` on the checkout row (already)

## P19.2 Checkout UI

- [ ] K11 disables Pay or blocks submit when the rail needs email
- [ ] Public GET may include a hint `email_required: true` so `:5179` does not guess — add if cheap

## P19.3 Exit

- [ ] Hermetic: chip start without email 400 (when C17 exists)
- [ ] Unblocked for P20, K11
