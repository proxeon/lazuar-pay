# P13 — PUT sets org_settings.active_provider

**Track:** Provider door · **Depends:** S13, P11  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-GW-007  
**Goal:** The rail you just saved is the rail buyers hit.

---

## P13.1

- [x] After successful PUT, set `OrgSettings.ActiveProvider = provider`
- [x] Create org_settings if missing (do **not** set `SstRegistered` — T11)
- [x] Same SaveChanges as credentials (H23 audit too)
- [x] Switching from stripe to chip updates active_provider; old stripe row may remain (rotation) but start uses chip

## P13.2 Must not

- [x] Do not leave active_provider=stripe after a successful chip PUT
- [x] Do not require a second “activate” endpoint in this program

## P13.3 Test

- [x] PUT chip (when C11 exists) then GET gateway describes chip
- [x] Until C exists, PUT stripe sets `active_provider=stripe`

## P13.4 Exit

- [x] Active provider follows last successful PUT
- [x] Unblocked for P14, P17
