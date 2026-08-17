---
number: "052"
id: B03-C07
severity: P1
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 052 — B03-C07 — `DunningPausedUntil` does not pause pre-dunning

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C07 — P1 — `DunningPausedUntil` does not pause pre-dunning

**Evidence.** Claim SQL pre-dunning (`DunningEngineJob.Claim.cs` 105–116) filters collection pause, not dunning pause. PAST_DUE SQL (118–126) filters dunning pause. `PauseSubscriberDunningCommandHandler` only writes the column.

**Repro.** Pause dunning 14 days on an ACTIVE due in 3 days. Hourly job still sends “renews soon.”

**Blast.** The control ops thinks they have (LP-080) does not stop the mail that is actually going out this week.

**Tests.** `PastDue_PausedUntilFuture_NotClaimed` and `Paused_SkipsTerminal` are PAST_DUE only. Add a pre-dunning twin.

**Fix direction.** Add the same `DunningPausedUntil` predicate to the pre-dunning claim (SQL + in-memory).

---

