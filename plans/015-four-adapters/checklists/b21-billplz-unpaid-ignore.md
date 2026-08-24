# B21 — Unpaid Billplz callback does not fulfill

**Track:** Billplz · **Depends:** B20  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-GW-008 analogue  
**Goal:** Verified due/unpaid is not a receipt.

---

## B21.1

- [x] HMAC valid, `paid=false` and state not paid → 200 `{ ignored: "unpaid" }`
- [x] No `RCPT-`, checkout `open`
- [x] Unique grain if inserted must not be `paid:{billId}` (use `unpaid:{billId}` or do not insert)

## B21.2 Exit

- [x] Test green
