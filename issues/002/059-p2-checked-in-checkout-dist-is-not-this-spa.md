---
number: "059"
id: PAY-CO-011
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 059 — Checked-in checkout `dist/` is not this SPA

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B13
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`apps/lazuar-pay-checkout/dist/` (and merchant `dist/`) can be present in the worktree. 016 already said hashed JS lacked then-current strings. `vite preview` without rebuild, or any deploy that ships git `dist/`, runs a **pre-occupancy / pre-restyle** pixel. `task pay:checkout` is fine (Vite source). CI `pnpm --filter lazuar-pay-checkout build` produces a fresh dist on the runner.

Root `.gitignore` includes `dist/`; leftover files can still confuse operators.

## Related files

- `apps/lazuar-pay-checkout/dist/`
- `apps/lazuar-pay-merchant/dist/`
- `.gitignore`

## Reproduction

`vite preview` without rebuild. Grep dist for `slot_key` / `Link is full` — may not match current `App.tsx`.

## Blast radius

Wrong pixel in preview/deploy. Not `task pay:checkout`.

## Suggested fix

Either gitignore `dist/` (prefer) or rebuild in the same commit as `App.tsx`. Production image (when it exists) must `pnpm build` with `VITE_PAY_API_URL`.

## Tests

- CI already builds. Do not treat git dist as evidence.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B13
- `plans/019-evals/02-merchant-frontend.md` dist hash note
