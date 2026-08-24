# K14 — success_url / ?status=verifying is not paid

**Track:** Checkout UI · **Depends:** K13  
**Analysis:** NP-CHK-002; 011 fail lock  
**IDs:** NP-GW-008  
**Goal:** Fulfillment is the webhook. The browser is not SoT.

---

## K14.1

- [x] Query `status=verifying` → verifying UI, not paid
- [x] Paid UI only when GET returns `status=paid`
- [x] Copy already: completing on the processor ≠ success URL — keep

## K14.2 Exit

- [x] Cannot get paid UI from query alone
