---
number: "060"
id: PAY-CO-012
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 060 — Card titles are not headings; confirming has no live region

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B14
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`card.tsx` `CardTitle` is a `div`. `index.html` `lang="en"`. Verifying spinner is a lucide icon with `animate-spin` and no `aria-live`. Loading is a `div`. Only start errors use `role="alert"`. Restyle **regressed** 014/016 `<h1>`.

## Related files

- `apps/lazuar-pay-checkout/src/ui/components/card.tsx`
- `apps/lazuar-pay-checkout/src/App.tsx` paid / verifying / loading Cards.

## Reproduction

Screen reader on verifying: no `h1` “Confirming payment”, no live update.

## Blast radius

A11y. Restyle regression.

## Suggested fix

`CardTitle` as `h1` on these pages (or `as="h1"`). `aria-live="polite"` on confirming / loading. Keep `role="alert"` for errors. Focus the title on status change.

## Tests

- Locks grep `Card` / `Payment received`. Missing `h1` / `aria-live`.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B14
