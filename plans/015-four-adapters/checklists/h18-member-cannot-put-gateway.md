# H18 — Member 403 on PUT gateway

**Track:** Harden · **Depends:** H17  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-GW-009, NP-ONE-021  
**Goal:** Writer gate on keys is tested, not only implied by catalog tests.

---

## H18.1 Live

- [x] `GatewayEndpoints.Put` already calls `RequireWriterAsync`
- [x] Add hermetic test: role `member` PUT `/v1/orgs/t1/gateway` → **403**
- [x] Owner PUT still 200
- [x] Member GET still 200 metadata (S18)

## H18.2 Must not

- [x] Do not invent One role `viewer`
- [x] Do not check `authz/check` relation that One does not have

## H18.3 Exit

- [x] Test green
- [x] Unblocked for U16
