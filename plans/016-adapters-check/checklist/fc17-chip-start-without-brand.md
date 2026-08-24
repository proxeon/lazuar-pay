# fc17 — CHIP start without Brand ID is 503

**Track:** Fill CHIP · **Depends:** A00  
**Analysis:** 09 method 17; C31 start  
**Goal:** `RailTests.Chip_start_without_brand_id_is_503`

---

## fc17.1

- [ ] PUT chip **with** brand, then DB-clear `PublicMerchantId`
- [ ] Start with email → 503, body rail not configured, Psp not called
- [ ] PUT without brand is already 400 (`Chip_put_requires_brand_id`) — keep that; this is start-time

## fc17.2 Exit

- [ ] Green
