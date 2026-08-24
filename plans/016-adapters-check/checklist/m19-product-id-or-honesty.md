# M19 — Product id or honesty copy

**Track:** Merchant · **Depends:** A00  
**Analysis:** POST product then POST checkout without `product_id`; interval always `one_off`  
**IDs:** —  
**Goal:** Stop demoing “this SKU” when the amount field is independent.

---

## M19.1 Pick one (not both half-done)

- [ ] **A:** Checkout create sends `product_id` from the created product (host must persist `CheckoutRow.ProductId` if not already)
- [ ] **B:** UI copy: “Amount is typed here. Catalog row is a label, not this charge.”

## M19.2 Must not

- [ ] Do not start subscriptions / `mo`/`yr` in this program (parked-offsession adjacent)
- [ ] Do not claim the pay link is the product SKU if you pick B

## M19.3 Exit

- [ ] A or B shipped; PR says which
