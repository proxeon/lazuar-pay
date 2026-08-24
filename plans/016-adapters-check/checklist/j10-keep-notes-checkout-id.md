# J10 — Keep notes.checkout_id stamp and read

**Track:** Razorpay join · **Depends:** A00  
**Analysis:** live `RazorpayHosted` notes; `RazorpayWebhook` reads `payment.entity.notes`  
**IDs:** —  
**Goal:** Do not delete the Hub join. Add a fallback (J11). Do not replace notes.

---

## J10.1 Live today (keep)

- [ ] Create payload `notes.checkout_id` + `notes.org_id`
- [ ] Parse reads `payload.payment.entity.notes.checkout_id`

## J10.2

- [ ] Keep both
- [ ] Do not move notes-only to metadata Hub folklore

## J10.3 Exit

- [ ] Existing `Razorpay_captured` still injects notes and pays
- [ ] Unblocked for J11
