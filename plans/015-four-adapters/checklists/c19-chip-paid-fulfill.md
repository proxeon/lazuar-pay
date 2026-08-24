# C19 — purchase.paid amount>0 fulfills

**Track:** CHIP · **Depends:** C18, H12, H13, C14  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-FUL-001, NP-GW-003  
**Goal:** Same `Fulfillment.FulfillPaidAsync` as Stripe. Rail does not journal.

---

## C19.1

- [ ] After RSA verify, parse JSON `event_type`
- [ ] `purchase.paid` **and** amount > 0 → resolve checkout id from metadata (C14) → H13 org bind → H12 TX → `FulfillPaidAsync(id, "chip", purchaseId)`
- [ ] Amount from `purchase.total` cents / 100 (C13 / H14)
- [ ] Title still Official Receipt (T14)
- [ ] No tax line (T13)

## C19.2 Must not

- [ ] Do not emit `GatewayPaymentCompletedIntegrationEvent`
- [ ] Do not treat `purchase.preauthorized` as this path (C21)
- [ ] Do not book CHIP `payment.fee_amount` as a fee line (`unknown ≠ 0` if you skip it)

## C19.3 Test

- [ ] Signed `purchase.paid` → one `RCPT-`, balanced journal, checkout paid
- [ ] Replay C25

## C19.4 Exit

- [ ] Paid path green
- [ ] Unblocked for C20–C25
