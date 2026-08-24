# T13 — No tax (or fee) journal lines

**Track:** Tax · **Depends:** T10  
**Analysis:** [00](../00-what-must-be-done.md) §3.1 / §4  
**IDs:** NP-MON-001 (two-line GMV only in this program)  
**Goal:** Journal stays cash + revenue. Processor tax/fee are not booked as 0.

---

## T13.1 Live

- [ ] Confirm `Fulfillment` still adds exactly two lines: account `cash` Dc `D` and account `revenue` Dc `C`, both `checkout.Amount`
- [ ] Do not add `tax` / `sst` / `fee` accounts
- [ ] Do not port Hub `taxRate` / `TaxAmount` into `PaidWebhook` or journal
- [ ] Do not book Razorpay webhook JSON `tax` or `fee` as lines (`unknown ≠ 0`)

## T13.2 Honesty

- [ ] A test name or comment may say “GMV two-line; tax out of program”
- [ ] Omitting the fee line is correct; booking fee 0 is not

## T13.3 Must not

- [ ] Do not add a third line “tax 0” to look complete

## T13.4 Exit

- [ ] Paid path still balances at two lines
- [ ] Unblocked for T16
