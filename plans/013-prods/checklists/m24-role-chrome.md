# M24 — Role chrome

**Track:** Merchant · **Depends:** M17  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Hide write chrome unless `owner`/`admin`. Do not fake VIEWER.  
**011:** NP-ONE-021, NP-ONE-022

---

## M24.1 Hide writes

- [x] Hide paste-keys / refund / create-product-write unless role is **`owner` or `admin`**
- [x] `member` can navigate the shell and later see payments (F21)
- [x] Chrome hide is **not** authorization — Pay APIs still 403

## M24.2 Honesty

- [x] Do not treat whoami role `viewer` (it will not appear)
- [x] Do not ship a Hub Viewer chip or invite `<option value="VIEWER">`
- [x] Do **not** mark NP-ONE-021 done from MemberGate `check(member)` (`/ready` is “has the tenant”)

## M24.3 Exit

- [x] Owner/admin see write affordances; member sees the shell without key paste
- [x] NP-ONE-021 stays open until money routes enforce role + `authz`
- [x] Unblocked for M25
