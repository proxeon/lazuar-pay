---
number: "051"
id: PAY-CO-003
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 051 — `localStorage` failure mints a new `slot_key` on every call

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B4 (also `05-payment-links-occupancy.md` B6)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`slotKey` `catch { return crypto.randomUUID() }` with no memory fallback. `payPath` (GET/poll) and start POST each call `slotKey(token)`. Private-mode Safari (or blocked storage): GET uses slot A, start uses slot B (new child or 409), poll uses slot C. Occupancy can **double-take** seats, or 409 full while the buyer already started under A and cannot resume.

Host `Same_slot_start_twice` reuses one slot string — does not cover this.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **21–36**, **129–132**.
- `apps/lazuar-pay-checkout/src/locks.test.ts` — greps `localStorage` / `slot_key`, not the catch path.

## Reproduction

Block storage. Open a max=2 link. Click Pay twice. Two seats. Poll may show full / not paid.

## Blast radius

Private mode, IT-blocked storage, some embeds. Combined with 001/019.

## Suggested fix

Module-level `Map<token, string>` fallback when `localStorage` throws. Same in-memory UUID for the document lifetime. Optionally persist to `sessionStorage` first. Do not mint inside `payPath` without caching.

## Tests

- Missing: SPA unit test that two `slotKey(token)` calls after storage throw return the same id.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B4
- `plans/019-evals/05-payment-links-occupancy.md` §B6
