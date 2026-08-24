# Q12 — Stop saying checkout is a fixture

**Track:** Q · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.6; live `pay-spec/main.tsp` line 7  
**IDs:** —  
**Goal:** Hub-README disease on the new stack.

---

## Q12.1

- [x] `packages/pay-spec/main.tsp` service comment: checkout is persisted Postgres, paid via webhook — not “fixture (open session), not a charge”
- [x] `packages/pay-spec/README.md` “when POST checkouts exists” is stale — fix
- [x] GET checkout status includes `paid`

## Q12.2 Exit

- [x] No “in-memory fixture” in pay-spec
