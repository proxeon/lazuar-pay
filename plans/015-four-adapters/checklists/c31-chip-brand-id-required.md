# C31 — CHIP Brand ID required

**Track:** CHIP · **Depends:** C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** —  
**Goal:** Hub: `"MerchantId (Brand ID) is required for CHIP Collect."`

---

## C31.1

- [ ] PUT chip without `public_merchant_id` → 400
- [ ] Start with chip row whose Brand ID is empty → 503 rail not configured (treat as incomplete creds)
- [ ] Do not call CHIP API without `brand_id`

## C31.2 Exit

- [ ] Tests green
