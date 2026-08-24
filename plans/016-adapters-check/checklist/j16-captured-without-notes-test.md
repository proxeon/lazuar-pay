# J16 — `Razorpay_captured_without_notes` hermetic

**Track:** Razorpay join · **Depends:** J11, J12  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) T5  
**IDs:** —  
**Goal:** P0-C is a test, not a comment.

---

## J16.1 Method `RailTests.Razorpay_captured_without_notes`

- [ ] PUT razorpay, start **with** email so `ProviderSessionId` is stub `plink_1` (FakePsp must return `"id":"plink_1"`)
- [ ] POST `payment.captured` HMAC, **omit** notes, include payment_link id `plink_1` **or** rely on session lookup as implemented in J11
- [ ] If join works: 200, one `RCPT-`
- [ ] Sibling `Razorpay_captured_without_notes_or_plink_is_400`: omit notes **and** plink, FakePsp id ignored — 400, zero docs, no event row

## J16.2 Must not

- [ ] Do not only test the notes-present path (`Razorpay_captured` already does)

## J16.3 Exit

- [ ] Both cases green
