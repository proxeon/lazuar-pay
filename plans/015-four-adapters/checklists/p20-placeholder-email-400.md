# P20 — customer@example.com is 400

**Track:** Provider door · **Depends:** P19  
**Analysis:** [00](../00-what-must-be-done.md) §5; Hub `GatewayCommon.PlaceholderEmail`  
**IDs:** —  
**Goal:** Never send Hub’s placeholder to a processor as a real buyer.

---

## P20.1

- [x] Treat `customer@example.com` (trim, case-insensitive) as unusable
- [x] CHIP / Billplz / Xendit / Razorpay start → 400 `"Customer email is required."` (or same as missing)
- [x] Do not substitute a generated email
- [x] Steal the **decision** from `GatewayCommon.IsUsableBuyerEmail`, not the class file

## P20.2 Must not

- [x] Do not ProjectReference Hub
- [x] Do not send the placeholder “to get a URL”

## P20.3 Exit

- [x] Hermetic 400
- [x] Unblocked for C30
