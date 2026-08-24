# R24 — Customer on payment link

**Track:** Razorpay · **Depends:** P19, R13  
**Analysis:** Hub `BuildPaymentLinkRequest` customer object  
**IDs:** NP-BUY-001  
**Goal:** Require email if Hub required a customer block.

---

## R24.1

- [x] Open Hub `BuildPaymentLinkRequest` and match whether `customer` / email is required
- [x] If Hub sent customer email → we require email (400 if missing / placeholder)
- [x] Include name when present

## R24.2 Exit

- [x] Start test
