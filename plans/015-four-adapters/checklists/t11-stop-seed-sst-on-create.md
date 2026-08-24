# T11 — Stop seeding SstRegistered on checkout create

**Track:** Tax · **Depends:** T10  
**Analysis:** [00](../00-what-must-be-done.md) §3.1  
**IDs:** NP-MON-004 (out)  
**Goal:** Creating a checkout is not a tax registration decision.

---

## T11.1 Live

- [x] Open `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- [x] Find `new OrgSettingsRow { OrgId = orgId, SstRegistered = false }`
- [x] If org_settings is created for pause/currency, **omit** `SstRegistered` (leave null) and **do not read it** on pay
- [x] Do not keep `false` as a fake “known unregistered” signal

## T11.2 Must not

- [x] Do not add a merchant SST yes/no field in this program (T15)
- [x] Do not fail checkout create because SST is null

## T11.3 Exit

- [x] Checkout create no longer writes SST as a business signal
- [x] Unblocked for T12
