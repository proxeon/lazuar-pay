# C19 — purchase.paid amount>0 fulfills

**Track:** CHIP · **Depends:** C18, H12, H13, C14  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-FUL-001, NP-GW-003  
**Goal:** Same `Fulfillment.FulfillPaidAsync` as Stripe. Rail does not journal.

---

## C19.1

- [x] After RSA verify, parse JSON `event_type`
- [x] `purchase.paid` **and** amount > 0 → resolve checkout id from metadata (C14) → H13 org bind → H12 TX → `FulfillPaidAsync(id, "chip", purchaseId)`
- [x] Amount from `purchase.total` cents / 100 (C13 / H14)
- [x] Title still Official Receipt (T14)
- [x] No tax line (T13)

## C19.2 Must not

- [x] Do not emit `GatewayPaymentCompletedIntegrationEvent`
- [x] Do not treat `purchase.preauthorized` as this path (C21)
- [x] Do not book CHIP `payment.fee_amount` as a fee line (`unknown ≠ 0` if you skip it)

## C19.3 Test

- [x] Signed `purchase.paid` → one `RCPT-`, balanced journal, checkout paid
- [x] Replay C25

## C19.4 Exit

- [x] Paid path green
- [x] Unblocked for C20–C25
