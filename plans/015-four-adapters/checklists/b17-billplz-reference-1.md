# B17 — reference_1 is checkout id

**Track:** Billplz · **Depends:** B13  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Do not stuff Hub `subscription_id` / `tenant_id` into reference_1.

---

## B17.1

- [x] `reference_1` = `checkout.Id`
- [x] `reference_1_label` = `Checkout` or `Reference`
- [x] Optional `reference_2` unused or `one_off` — do not invent Hub `type=commerce_subscription`
- [x] Webhook may use `reference_1` as fallback join (B16)

## B17.2 Must not

- [x] Do not put One tenant id as the only reference (org is in the path)

## B17.3 Exit

- [x] Create body has checkout id
