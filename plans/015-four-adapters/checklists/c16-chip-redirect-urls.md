# C16 — CHIP success / failure / cancel redirects

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-CHK-002  
**Goal:** Buyer returns to `:5179`, not a Pay “paid” lie.

---

## C16.1

- [x] `success_redirect` = checkout `SuccessUrl` or default `http://localhost:5179/c/{publicToken}?status=verifying` (same idea as StripeHosted)
- [x] `cancel_redirect` and `failure_redirect` = `CancelUrl` or `/c/{token}`
- [x] Success URL is **not** fulfillment (K14)

## C16.2 Must not

- [x] Do not point success at Hub `/api/v1/...`
- [x] Do not mark paid on redirect

## C16.3 Exit

- [x] URLs in mocked body
