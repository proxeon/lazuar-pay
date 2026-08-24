# X15 — Invoice status PAID fulfills

**Track:** Xendit · **Depends:** X14, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.3; Hub `MapStatus`  
**IDs:** NP-FUL-001  
**Goal:** PAID (and `invoice.paid`) only. Same Fulfillment.

---

## X15.1

- [ ] Parse JSON; Hub also checks `data` wrapper
- [ ] `status` PAID (case-insensitive) or event `invoice.paid`
- [ ] Checkout id from metadata / `external_id`
- [ ] `FulfillPaidAsync(..., "xendit", invoiceId)`
- [ ] Amount from `paid_amount` else `amount` (Hub) vs checkout (H14)

## X15.2 Must not

- [ ] Do not fulfill SETTLED as a second paid (X16)
- [ ] Do not book `fees_paid_amount` as a journal fee line

## X15.3 Exit

- [ ] Paid fixture → `RCPT-`
