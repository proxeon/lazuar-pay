# F09 — TypeSpec Wave B (FW-6)

**Goal:** Remaining contract honesty after Phase 05 P0.  
**Depends on:** none (can parallel Keys/SQL)  
**PR shape:** one concern per PR if large

---

## F09.1 Dual DTOs remaining

- [ ] Inventory local C# request/response types that mirror OpenAPI (esp. commerce products)
- [ ] For each: update TypeSpec if needed → `task gen` → switch endpoint to generated types → delete local
- [ ] Build + tests green

## F09.2 Impl-only / TSP-only honesty

- [ ] Billing signed PDF: add to TSP **or** document internal-only
- [ ] Broadcast preview/status: implement + TSP **or** remove from contract surface
- [ ] Communications public compliance routes: align TSP ↔ Minimal API
- [ ] Payments docs security schemes where auth required

## F09.3 CI (optional but recommended)

- [ ] Add OpenAPI path vs Minimal API honesty check (script or test)
- [ ] Wire into CI contracts job or document manual until automated

## F09.4 Exit

- [ ] No known dual DTO pairs on shipping admin/public surfaces targeted this wave
- [ ] `task gen` clean; clients committed per policy
- [ ] FW-6 updated in FUTURE-WORK.md
