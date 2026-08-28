---
number: "026"
id: PAY-VAULT-001
severity: P1
status: resolved
source: plans/019-evals/04-processors-vault-test.md
head: "9f04ad58"
---

# 026 — 016-era open checkouts with null `Provider` can no longer start

- **Severity:** P1 (cutover)
- **Status:** open
- **Source:** `plans/019-evals/04-processors-vault-test.md` B7 (also `10-honesty-bugs-gaps.md` P1-7)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

018 binds rail at mint and stores `checkout.Provider`. Start uses `row.Provider ?? link?.Provider`. Independent vault **stopped reading** `OrgSettings.ActiveProvider` (column kept, unused). 016-era open checkouts with `Provider` null used to fall through to the org’s active rail. They now 503 `"rail not configured"`.

`Never_started_checkout_webhook_is_400` synthetically nulls `Provider` — that is a test fixture, not a backfill.

Laptop DBs that created Stripe checkouts before `82e387b7` can still have null provider + leftover `ActiveProvider = stripe`.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **140–144**.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` **9–11** — `ActiveProvider` unused; comment says do not read on the pay path.
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` — PUT does not write `ActiveProvider`.
- Tests that null `Provider` for webhook 400.

## Reproduction

Row: `checkouts.Provider` null, `org_settings.active_provider` stripe, vault on file. `POST /start` → 503.

## Blast radius

In-flight 016 checkouts on a persisted laptop/staging DB. New mints set provider. Production empty DBs are fine.

## Suggested fix

One-shot backfill: `UPDATE checkouts SET provider = org_settings.active_provider WHERE provider IS NULL`. Never read `ActiveProvider` on the hot start path after that. Do not revive it as the mint default (018 law). Then drop the column in a later hygiene issue (gap G1 in 04).

## Tests

- Missing: null provider + leftover ActiveProvider still 503 **after** you decide “no backfill on start” — or 200 after backfill migrator. Name the choice.

## Source reports

- `plans/019-evals/04-processors-vault-test.md` §B7 §G1
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-7
