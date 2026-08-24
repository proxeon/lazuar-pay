# R21 — Ignore Razorpay JSON tax and fee

**Track:** Razorpay · **Depends:** T13, R17  
**Analysis:** [00](../00-what-must-be-done.md) §4  
**IDs:** NP-MON-002  
**Goal:** Processor GST on MDR is not our SST. `unknown ≠ 0`.

---

## R21.1

- [x] Entity may contain `tax` and `fee` — **do not** add journal lines
- [x] Two-line cash/revenue for checkout.Amount only
- [x] Do not port Hub `TaxAmount` into Fulfillment

## R21.2 Exit

- [x] Paid test journal still two lines even if fixture includes `"tax": 12`
