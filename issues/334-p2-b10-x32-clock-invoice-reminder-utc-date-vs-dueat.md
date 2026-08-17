---
number: "334"
id: B10-X32
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 334 — B10-X32 — Clock: invoice reminder UTC date vs `DueAt`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X32 — P2 — Clock: invoice reminder UTC date vs `DueAt`

Quoted in §2.8. Offsets `[-3, 0, 3]` compare UTC calendar dates. A quote due “2026-08-20” stored as `2026-08-19T16:00:00Z` (00:00 MYT on the 20th) has `DueAt.Date == 2026-08-19` UTC. Day-0 mail goes out on the 19th UTC, i.e. the afternoon of the 19th in Malaysia — one local day early. The unique log then blocks a correct day-0 on the 20th.

No test uses a non-midnight `DueAt`. The three tests use in-process “today.”

---

