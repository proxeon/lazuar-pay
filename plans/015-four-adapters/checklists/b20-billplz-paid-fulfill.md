# B20 — paid=true or state=paid fulfills

**Track:** Billplz · **Depends:** B18, B16, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-FUL-001  
**Goal:** Same Fulfillment as Stripe/CHIP.

---

## B20.1

- [x] `paid` equals `true` (case-insensitive) **or** `state` equals `paid`
- [x] `paid_amount` cents / 100 vs checkout (H14)
- [x] Currency: Billplz is MYR for this program — still do not invent if you later add others; checkout currency must be MYR
- [x] `FulfillPaidAsync(checkoutId, "billplz", billId)`
- [x] Official Receipt, two-line journal, no tax

## B20.2 Test

- [x] Signed/HMACed form paid → one `RCPT-`
- [x] Replay still one document (B28)

## B20.3 Exit

- [x] Paid path green
