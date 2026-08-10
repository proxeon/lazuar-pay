# S60 — Polish: root README / optional mprocs

**Track:** Polish · **Analysis:** `../10` D06  
**Depends on:** S31  
**Goal:** Discoverability without auto-starting sample for everyone.

---

## S60.1 Root README

- [x] Project structure bullet: `examples/hub-cashier-next` — integrator sample (port 3020)
- [x] Optional: link plans/006-sample README

## S60.2 mprocs (optional)

- [ ] If added: sample proc **autostart false** — **not added** this wave (optional residual)
- [ ] Document how to start manually — covered in `examples/README.md` + root port table (`pnpm example:cashier`)

## S60.3 CORS

- [x] Confirm API CORS allows `http://localhost:3020` (appsettings already may list it)
- [x] Add if missing (minimal API config change only) — **already present** in `appsettings.json` + Development

## S60.4 Exit

- [x] Sample remains optional for day-to-day monorepo work
