# X15 — Invoice status PAID fulfills

**Track:** Xendit · **Depends:** X14, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.3; Hub `MapStatus`  
**IDs:** NP-FUL-001  
**Goal:** PAID (and `invoice.paid`) only. Same Fulfillment.

---

## X15.1

- [x] Parse JSON; Hub also checks `data` wrapper
- [x] `status` PAID (case-insensitive) or event `invoice.paid`
- [x] Checkout id from metadata / `external_id`
- [x] `FulfillPaidAsync(..., "xendit", invoiceId)`
- [x] Amount from `paid_amount` else `amount` (Hub) vs checkout (H14)

## X15.2 Must not

- [x] Do not fulfill SETTLED as a second paid (X16)
- [x] Do not book `fees_paid_amount` as a journal fee line

## X15.3 Exit

- [x] Paid fixture → `RCPT-`
