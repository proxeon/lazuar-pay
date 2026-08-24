# T15 — No merchant SST registration field

**Track:** Tax · **Depends:** T11  
**Analysis:** [00](../00-what-must-be-done.md) §3.1  
**IDs:** NP-MON-004 (out)  
**Goal:** Do not build the SST UI/API 013 F18 wanted. Tax is out.

---

## T15.1 Must not add

- [x] No `PUT /v1/orgs/{orgId}/tax` (or SST boolean on gateway PUT)
- [x] No SST checkbox on `:5178`
- [x] Do not port `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs`
- [x] Leave `org_settings.sst_registered` column in the DB if it already exists (no drop required this program)
- [x] Stop **reading** `SstRegistered` on the pay path (T10)

## T15.2 Exit

- [x] No new SST API or UI
- [x] Unblocked for T17
