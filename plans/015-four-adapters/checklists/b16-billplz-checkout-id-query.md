# B16 — Join via query checkout_id

**Track:** Billplz · **Depends:** B14, P21  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Webhook handler reads `checkout_id` from query, then form, then `reference_1`.

---

## B16.1

- [ ] Hub checked `Query-checkout_id` header map then form `checkout_id`
- [ ] ASP.NET: read `request.Query["checkout_id"]` first
- [ ] Then form field `checkout_id`
- [ ] Then `reference_1` (B17)
- [ ] Then `ProviderSessionId` match on bill id if still missing
- [ ] Then 400 unusable — do not fulfill a random open checkout

## B16.2 Exit

- [ ] Fixture with query param pays the right checkout (B20)
