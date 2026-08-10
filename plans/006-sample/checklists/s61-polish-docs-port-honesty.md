# S61 — Docs port honesty (8080 vs 8090)

**Track:** Polish · **Analysis:** `../06` port drift  
**Depends on:** S00 lock 8080  
**Goal:** Integrator guides stop defaulting to wrong local port.

---

## S61.1 Grep

- [x] Find `8090` in lazuar-docs and payments-integration-quickstart
- [x] Find sample docs using wrong base

## S61.2 Fix policy

- [x] Default local Hub: **8080** / `http://localhost:8080/api/v1`
- [x] If 8090 kept anywhere: label as “alternate mapping only”
- [x] Align create-checkout / provision curl examples

## S61.3 Exit

- [x] No unexplained 8090 as primary default in integrator guides
- [x] Docs build green
