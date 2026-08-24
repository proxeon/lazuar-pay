# U10 — Staff provider select on :5178

**Track:** Merchant UI · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §6.1  
**IDs:** NP-GW-009  
**Goal:** Owner/admin picks **one** rail. Not a buyer dropdown. Not ops PaymentSettingsPage clone.

---

## U10.1

- [x] `WorkspacePage.tsx` (or a small child) provider select: `stripe | chip | billplz | xendit | razorpay`
- [x] Changing select shows that rail’s field set (U11–U15)
- [x] Submit PUT with `provider` + fields
- [x] Do not import `@repo/api-types-ts`
- [x] Do not copy `lazuar-ops` modules

## U10.2 Must not

- [x] Do not put this select on `:5179` (K10)
- [x] Do not show five logos as “we take all wallets”

## U10.3 Exit

- [x] Select exists for writers
- [x] Unblocked for U11–U15
