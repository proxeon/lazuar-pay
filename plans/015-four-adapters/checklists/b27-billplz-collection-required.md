# B27 — Collection ID required

**Track:** Billplz · **Depends:** B11  
**Analysis:** Hub `"MerchantId (Collection ID) is required for Billplz."`  
**IDs:** —  
**Goal:** Do not POST bills without `collection_id`.

---

## B27.1

- [x] PUT without `public_merchant_id` → 400
- [x] Start with empty Collection ID → 503 incomplete rail
- [x] Create JSON always includes `collection_id`

## B27.2 Exit

- [x] Tests green
