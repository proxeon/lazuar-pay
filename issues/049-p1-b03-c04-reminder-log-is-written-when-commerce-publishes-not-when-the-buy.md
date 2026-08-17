---
number: "049"
id: B03-C04
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/049-reminder-log-after-publish
---

# 049 — B03-C04 — Reminder log is written when Commerce publishes, not when the buyer is emailed

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/049-reminder-log-after-publish`

Missing CRM email skips publish and does not consume the DayOffset, so the next tick can retry.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C04 — P1 — Reminder log is written when Commerce publishes, not when the buyer is emailed

**Evidence.** Processor records the offset in the same unit of work that publishes `FulfillmentRequested`. Communications hydrate **throws** on missing `client_profile_id`, missing CRM profile, empty email, or empty EMAIL body (`FulfillmentRequestedIntegrationEventHandler.cs` 67–75, 78–86, 89–96, 193–201). Inbox can retry Communications; Commerce will not re-dispatch because the unique log exists.

**Repro.** PAST_DUE sub whose CRM profile has no email. Hourly tick: reminder log day 0 written, Communications throws, no Resend. Later ticks: `PastDue_Day0Email_SecondRunIsIdempotent` behaviour — silence.

**Blast.** Entire dunning timeline can be “green” in ops (`LastCompletedDayOffset` advances) with zero inbox. Terminal CANCEL still fires on grace (`Cancel_WhenNoPastDueSteps_OnGraceDay` is the empty-timeline cousin).

**Tests.** Job tests mock `IEventBus` and never run Communications. Add an integration that a thrown hydrate does **not** leave a reminder log, or a dead-letter that re-opens the offset.

**Fix direction.** Write the log only after Communications acks, **or** use a PENDING dispatch row, **or** do not consume on publish failure (outbox + inbox in one Commerce transaction with a delivery receipt).

---

