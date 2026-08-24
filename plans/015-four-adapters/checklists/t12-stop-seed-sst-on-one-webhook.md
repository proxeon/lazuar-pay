# T12 — Stop seeding SstRegistered on One webhook insert

**Track:** Tax · **Depends:** T11  
**Analysis:** [00](../00-what-must-be-done.md) §3.1  
**IDs:** NP-ONE-018 (pause only)  
**Goal:** `tenant.suspended` may pause charges. It is not a tax filing.

---

## T12.1 Live

- [x] Open `apps/lazuar-pay/src/Lazuar.Pay/One/OneWebhookEndpoints.cs`
- [x] On new `OrgSettingsRow` for `tenant.suspended`, remove `SstRegistered = false`
- [x] Keep `ChargesPaused = true` on suspend
- [x] Keep `ChargesPaused = false` on `tenant.reactivated`
- [x] Do not set SST on either event

## T12.2 Must not

- [x] Do not mix Plane A pause with tax
- [x] Do not “fix” One HMAC dialect in this tax track (out of T)

## T12.3 Exit

- [x] One webhook insert does not write SST
- [x] Unblocked for T13
