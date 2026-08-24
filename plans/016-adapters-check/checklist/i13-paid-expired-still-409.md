# I13 — Paid / expired start is still 409

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** live Start already 409s `Checkout is not open`  
**IDs:** —  
**Goal:** Idempotency is not a way to reopen a paid checkout.

---

## I13.1 Live today (keep)

- [ ] `session.Status is "paid" or "expired"` → 409 **before** rail create
- [ ] Keep that order: status check **before** the stored-URL resume

## I13.2

- [ ] Even if `PspRedirectUrl` is still on the row, paid/expired → 409
- [ ] Do not return the old hosted URL for a paid checkout (buyer would see a processor page that may still be payable)

## I13.3 Exit

- [ ] Existing paid-checkout start 409 remains (add a hermetic if missing: `PublicPayTests.Start_paid_is_409`)
- [ ] Unblocked for I14
