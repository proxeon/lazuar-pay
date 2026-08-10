# S60 — Polish: root README / optional mprocs

**Track:** Polish · **Analysis:** `../10` D06  
**Depends on:** S31  
**Goal:** Discoverability without auto-starting sample for everyone.

---

## S60.1 Root README

- [ ] Project structure bullet: `examples/hub-cashier-next` — integrator sample (port 3020)
- [ ] Optional: link plans/006-sample README

## S60.2 mprocs (optional)

- [ ] If added: sample proc **autostart false**
- [ ] Document how to start manually

## S60.3 CORS

- [ ] Confirm API CORS allows `http://localhost:3020` (appsettings already may list it)
- [ ] Add if missing (minimal API config change only)

## S60.4 Exit

- [ ] Sample remains optional for day-to-day monorepo work
