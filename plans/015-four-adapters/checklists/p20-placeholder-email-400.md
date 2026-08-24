# P20 — customer@example.com is 400

**Track:** Provider door · **Depends:** P19  
**Analysis:** [00](../00-what-must-be-done.md) §5; Hub `GatewayCommon.PlaceholderEmail`  
**IDs:** —  
**Goal:** Never send Hub’s placeholder to a processor as a real buyer.

---

## P20.1

- [ ] Treat `customer@example.com` (trim, case-insensitive) as unusable
- [ ] CHIP / Billplz / Xendit / Razorpay start → 400 `"Customer email is required."` (or same as missing)
- [ ] Do not substitute a generated email
- [ ] Steal the **decision** from `GatewayCommon.IsUsableBuyerEmail`, not the class file

## P20.2 Must not

- [ ] Do not ProjectReference Hub
- [ ] Do not send the placeholder “to get a URL”

## P20.3 Exit

- [ ] Hermetic 400
- [ ] Unblocked for C30
