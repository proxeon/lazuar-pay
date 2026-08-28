---
number: "043"
id: PAY-MERCH-009
severity: P1
status: resolved
source: plans/019-evals/04-processors-vault-test.md
head: "9f04ad58"
---

# 043 — Mint dialog defaults to Test even when a real rail is on file

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/04-processors-vault-test.md` B4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`useState<Rail | ''>('test')`. After GET `/gateways`, `setProvider` keeps `prev` if it is still in the list. Initial `'test'` is always in `withTest(list)`, so **`firstReal` never runs**. Staff with Stripe on file still mint Test unless they open the select.

Copy says “Saving a secret does not pick the rail.” True. The dialog still **pre-picks** Test.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **109**, **134–139**.

## Reproduction

Vault Stripe. Create pay link. Processor select shows Test. Create without touching it → Test link.

## Blast radius

Every mint until someone notices. Combined with 006, fake receipts. Combined with 042, Production 400.

## Suggested fix

Empty initial state; prefer first configured **non-test**. Do not revive `ActiveProvider` as the default (018 law). Fix B4 in the SPA.

## Tests

- Existing locks require `'test'` in CheckoutsPage (default).
- After fix: lock default is first real rail when present.

## Source reports

- `plans/019-evals/04-processors-vault-test.md` §B4
- `plans/019-evals/02-merchant-frontend.md` mint always offer Test
