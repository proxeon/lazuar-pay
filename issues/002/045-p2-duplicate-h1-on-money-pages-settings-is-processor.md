---
number: "045"
id: PAY-MERCH-011
severity: P2
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 045 — Duplicate `<h1>` on money pages; Settings is Processor

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B10
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`DashboardChrome` renders `<h1>{title}</h1>` in the top bar. Pay links / Processor / Payments / Receipts / create-workspace also render `PageHeader` `<h1>` with the same word. Screen readers hear “Pay links Pay links”. Overview titles differ (Overview vs tenant name) — fine.

User menu **Settings** navigates to `/gateway`. There is no settings page. Staff looking for email/password land on Processor keys.

## Related files

- `apps/lazuar-pay-merchant/src/layout/DashboardChrome.tsx`
- `apps/lazuar-pay-merchant/src/layout/PageHeader.tsx`
- `apps/lazuar-pay-merchant/src/ui/components/app-sidebar/user-menu.tsx`

## Reproduction

Open Pay links. Two h1s. Click Settings in the user menu → Processor.

## Blast radius

A11y noise; mis-click into secrets.

## Suggested fix

Drop the canvas `PageHeader` title when it equals the chrome title (keep subtitle). Rename the menu item “Processor”, or remove it (sidebar already has Processor). Do not invent a Hub settings cathedral.

## Tests

- Locks currently require PageHeader titles. Update greps if the duplicate h1 is removed.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B10
