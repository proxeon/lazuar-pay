---
number: "018"
id: PAY-HOST-001
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 018 — Connection-string “password keep” replaces the whole CS

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B6
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

018 intended: keep the local Postgres password already in the CS when loading `.env`. Live: if the configured CS is empty **or** does not contain the substring `Password=`, replace the **entire** string with `Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres`.

A CS that uses `Pwd=`, `postgres://`, or trust/peer auth is treated as “no password” and thrown away, including `Host=`. The host does not load `.env` itself; `ConnectionStrings__Pay` without that substring silently talks to localhost/postgres/postgres.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` **49–54** — substring check and wholesale replace.
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json` **7–9** — default CS with `Password=postgres`.
- Commit `84a3ee24` — “keep local Postgres password when loading `.env`”.

## Reproduction

Set `ConnectionStrings__Pay=Host=db.internal;Port=5432;Database=lazuar_pay;Username=pay;Pwd=secret` (or a URL without `Password=`). Boot Development. Process connects to localhost:5435 postgres/postgres instead of `db.internal`.

## Blast radius

Wrong database instead of a connect error. Staging-shaped env vars that use `Pwd=` or URLs.

## Suggested fix

If CS is non-empty, **use it**. If connect fails, 503 `/ready` and log. Laptop default only when CS is null/whitespace — not when `Password=` is missing. Accept `Pwd=`. Do not rewrite `Host`.

## Tests

- Missing: non-empty CS without `Password=` substring is **not** replaced (can assert the options object / a probe). HealthTests must not depend on the replace.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B6
