---
number: "057"
id: B03-C12
severity: P1
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 057 — B03-C12 — Failed-handler / Billing `StartPastDueDunningRunAsync` race the hourly claim

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C12 — P1 — Failed-handler / Billing `StartPastDueDunningRunAsync` race the hourly claim

**Evidence.** Processor is invoked from three places: hourly job (row locked), Billing mint path (`BillingEngineJob.cs` 316–317, no dunning claim lock), Commerce fail handler (no lock). Reminder unique index and ChargeAttempt unique index are the only serialisers. Two publishers can both `PublishAsync` EMAIL before either `SaveChanges`. One insert wins; the other tick errors and is retried. Buyer can get two day-0 mails.

**Repro.** Stripe fail webhook and the hourly job on the same due row in the same second (or Billing mint + job).

**Blast.** Duplicate “you’re past due” + two hosted sessions if both minted. Unique-index exception on the job looks like a random dunning error.

**Tests.** All in-memory, single-threaded.

**Fix direction.** Take the same `FOR UPDATE` claim (or an advisory lock on `subscription.Id`) inside the fail handler and Billing start-run before `ProcessAsync`.

---

