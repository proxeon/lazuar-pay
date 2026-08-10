# S61 — Docs port honesty (8080 vs 8090)

**Track:** Polish · **Analysis:** `../06` port drift  
**Depends on:** S00 lock 8080  
**Goal:** Integrator guides stop defaulting to wrong local port.

---

## S61.1 Grep

- [ ] Find `8090` in lazuar-docs and payments-integration-quickstart
- [ ] Find sample docs using wrong base

## S61.2 Fix policy

- [ ] Default local Hub: **8080** / `http://localhost:8080/api/v1`
- [ ] If 8090 kept anywhere: label as “alternate mapping only”
- [ ] Align create-checkout / provision curl examples

## S61.3 Exit

- [ ] No unexplained 8090 as primary default in integrator guides
- [ ] Docs build green
