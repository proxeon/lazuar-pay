# P19 — Email required on start for CHIP / Billplz / Xendit

**Track:** Provider door · **Depends:** P17  
**Analysis:** [00](../00-what-must-be-done.md) §5; Hub `GatewayCommon.TryResolveEmail`  
**IDs:** NP-BUY-001  
**Goal:** Those APIs refuse placeholder buyers. Stripe may keep optional email.

---

## P19.1

- [x] After resolving provider, if `chip` | `billplz` | `xendit`: require non-empty trimmed email
- [x] Missing → 400 `"email is required"`
- [x] Razorpay: follow R24 (Hub sent customer on payment link — require email)
- [x] Stripe: email optional (live today)
- [x] Persist `PayerEmail` / `PayerName` on the checkout row (already)

## P19.2 Checkout UI

- [x] K11 disables Pay or blocks submit when the rail needs email
- [x] Public GET may include a hint `email_required: true` so `:5179` does not guess — add if cheap

## P19.3 Exit

- [x] Hermetic: chip start without email 400 (when C17 exists)
- [x] Unblocked for P20, K11
