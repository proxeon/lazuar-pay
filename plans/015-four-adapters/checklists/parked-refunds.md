# Parked — Refunds

**Do not start in 015.**  
**Analysis:** [00](../00-what-must-be-done.md) §9; Hub `IssueRefundAsync`

---

- [ ] Full refund: call gateway, then reverse the journal **once**
- [ ] Billplz has no bill-refund API (B24) — mark-refunded later, not Payment Order
- [ ] Stripe/CHIP/Xendit/Razorpay API refunds exist in Hub; steal HTTP **after** hosted_link is boring
- [ ] Disputes: do not double-reverse
- [ ] `NP-MON-005` / `NP-MON-006` stay later
