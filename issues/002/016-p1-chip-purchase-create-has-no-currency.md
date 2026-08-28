---
number: "016"
id: PAY-CHIP-002
severity: P1
status: resolved
source: plans/019-evals/10-honesty-bugs-gaps.md
head: "9f04ad58"
---

# 016 — CHIP purchase create has no currency field

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/10-honesty-bugs-gaps.md` 016 P1-4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`ChipHosted` builds products/price only. It does not send a purchase-level currency. CHIP may default the brand currency. If the brand is not MYR (or CHIP expects an explicit ISO), amount on their page can diverge from `checkout.Amount`. Webhook then mismatch-400s (015) or, worse, matches a wrong amount.

016 listed this OPEN. Still open on `9f04ad58`.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` **43–54** (approx.) — products price, no purchase currency.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs` / `HostedSession.cs`.
- CHIP rail tests: `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Chip/ChipRailTests.cs` — assert body does not contain `force_recurring`; currency field not asserted as present.

## Reproduction

Inspect FakePsp / recorded CHIP create body for a MYR 10 checkout. No purchase `currency` (or equivalent CHIP field). Brand default applies.

## Blast radius

Non-MYR brands, or CHIP accounts whose default is not MYR. Bar B is MYR — dogfood of a MYR brand hides it.

## Suggested fix

Send CHIP’s documented currency field on create (steal Hub HTTP **judgment** only — `ChipCollectGatewayAdapter` currency placement). Keep amount in CHIP’s unit (sen). One FakePsp assertion: body contains the ISO and RM10 as 1000. Do not add `force_recurring`. Do not copy Hub types.

## Tests

- Missing: CHIP start body includes currency; amount 1000 for 10.00 MYR.

## Source reports

- `plans/019-evals/10-honesty-bugs-gaps.md` §016 P1-4
- `plans/019-evals/06-rails-webhooks-fulfillment.md` §G8
