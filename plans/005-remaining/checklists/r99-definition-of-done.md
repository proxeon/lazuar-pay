# R99 — Definition of done (remaining-work program)

**Goal:** Close the 005 remaining program honestly  
**Analysis:** `../10-program-sequencing-and-risks.md`  
**Close-out (2026-08-09):** `../r99-notes.md` — **wave closed**; ops residuals listed

---

## R99.1 Per selected track

### Keys (if selected)

- [x] R01–R03 **code done**; R04 **ops pending**; R05 **code on branch, deploy-gated**; R06 **deferred** ≥30d after prod One-only  
- [x] Documented as: **code complete, ops/deploy residual** (`../r99-notes.md`, `../r04-notes.md`, `../r05-notes.md`, `../r06-notes.md`)  
- [ ] R05 One-only **in prod** — residual ops ticket (migrate + deploy)  
- [x] R06 done **or dated** — dated: clock starts after prod One-only

### SQL (if selected)

- [x] R11–R15 P0/P1 fixed  
- [x] R16/R17 handoffs resolved via R35/R05

### TypeSpec (if selected)

- [x] R20–R24 targets for wave done  
- [x] R25 optional CI on or ticketed

### BB (if selected)

- [x] R30–R35 planned moves done

### Webhooks (if selected)

- [x] R43 **code** complete; staging/prod ops residual (R42.4 / migrate)  
- [ ] Product deferred with new date — N/A (shipped code)

### Polish

- [x] Opportunistic items done (R50–R53)

### Extract

- [x] R60 **SKIP** (`../r60-notes.md`)

## R99.2 Docs

- [x] `FUTURE-WORK.md` statuses updated  
- [x] No dual-path lies in One/Lhdn READMEs for closed tracks  
- [x] Residuals are normal tickets, not open “mega remaining program”

## R99.3 Stop

- [x] **Declare wave closed** — residual ops only:
  1. Keys migrate + deploy One-only  
  2. Webhook migrate staging (then prod verify)  
  3. Table drops clocks (R06 ≥30d after prod One-only; optional webhook sub table later)
