# F18 — SST fail closed (do not undercharge)

**Track:** Fulfillment · **Depends:** F13  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-MON-004  
**Goal:** Unknown merchant SST registration must not book tax=0. Qty=1 dogfood stays honest.

---

## F18.1 Judgment

- [x] Read `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` `Compute` — **read, do not copy project**. No ProjectReference
- [x] Type `02` = service tax; `06` = not applicable. Exclusive round on the **unit**
- [x] Known **not registered** (`sst_registered = false`): coerce `06` / tax 0 (allowed)
- [x] **Unknown** (missing settings, null with no default, load failed): **throw**. Do not book. Do not undercharge
- [x] Do not guess 8% on everyone (overcharge is not fail-closed)

## F18.2 Qty=1 dogfood

- [x] Bar B qty=1: either known false (`06`) **or** fail closed
- [x] Do not persist tax=0 because “we will look it up later”
- [x] Seats × unit (`NP-MON-003`) is **Bar C** — still do not undercharge at qty=1
- [x] Do not book SST-inclusive cash as all `revenue_gross`

## F18.3 Exit

- [x] Unknown SST cannot commit a GMV journal as tax=0
- [x] Unblocked for F22 (replay still honest)
