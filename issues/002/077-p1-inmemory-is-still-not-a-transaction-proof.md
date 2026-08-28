---
number: "077"
id: PAY-TEST-005
severity: P1
status: resolved
source: plans/019-evals/09-tests-inventory.md
head: "9f04ad58"
---

# 077 — InMemory is still not a transaction proof

- **Severity:** P1 (honesty / CI)
- **Status:** resolved
- **Source:** `plans/019-evals/09-tests-inventory.md` (also `06-rails-webhooks-fulfillment.md` G3, `10-honesty-bugs-gaps.md` P1-6)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`PayApiFactory` uses EF InMemory and **explicitly ignores** transactions (`InMemoryEventId.TransactionIgnoredWarning`). `BeginTransactionAsync` is a no-op. 016 P0-2 (event committed before fulfill) is **coded closed** on Npgsql (one TX, no SaveChanges between Add and fulfill; FillTests probe throws **before** inner SaveChanges). It is **not** a Postgres CI property.

Occupancy unique index is Npgsql-only (001). Same-slot unique (013) too. Concurrent fulfill (010) cannot be proven here.

Do not sell “one transaction” as proven by `task pay:test`.

## Related files

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` **27–54**.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **143–154**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` — `Fulfill_throw_returns_5xx_event_not_committed_retry_pays`.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **43–48**.

## Reproduction

Read PayApiFactory. Transactions ignored. FillTests still pass.

## Blast radius

False confidence on money TX, occupancy, unique RCPT.

## Suggested fix

One Testcontainers (or SQLite with real TX) fixture for: fulfill-throw → event absent, retry pays; concurrent occupancy (001); unique RCPT (010). Keep InMemory for the rest of the hermetic suite. Do not call live PSP.

## Tests

This issue **is** the Postgres proof. T4 in `10-honesty-bugs-gaps.md`.

## Source reports

- `plans/019-evals/09-tests-inventory.md` InMemory limitations
- `plans/019-evals/06-rails-webhooks-fulfillment.md` §G3
- `plans/019-evals/10-honesty-bugs-gaps.md` §016 P0-2 OPEN as CI
