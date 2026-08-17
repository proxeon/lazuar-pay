---
number: "218"
id: B03-C30
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 218 — B03-C30 — No HTTP test that missing token is 401

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C30 — P2 — No HTTP test that missing token is 401

`PublicArrearsEndpointsBoundaryTests` only forbids `crm."` / `one."` SQL. A future refactor that makes `token` optional would not go red. This is a test hole that protects B03-C01’s cousin (008 P0-2 regression).

---

