# B20 — paid=true or state=paid fulfills

**Track:** Billplz · **Depends:** B18, B16, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-FUL-001  
**Goal:** Same Fulfillment as Stripe/CHIP.

---

## B20.1

- [ ] `paid` equals `true` (case-insensitive) **or** `state` equals `paid`
- [ ] `paid_amount` cents / 100 vs checkout (H14)
- [ ] Currency: Billplz is MYR for this program — still do not invent if you later add others; checkout currency must be MYR
- [ ] `FulfillPaidAsync(checkoutId, "billplz", billId)`
- [ ] Official Receipt, two-line journal, no tax

## B20.2 Test

- [ ] Signed/HMACed form paid → one `RCPT-`
- [ ] Replay still one document (B28)

## B20.3 Exit

- [ ] Paid path green
