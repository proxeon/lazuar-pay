---
number: "042"
id: PAY-MERCH-008
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 042 — Merchant always offers Test; host refuses it in Production

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B8 (also `04-processors-vault-test.md` B3)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

SPA `rails` includes `'test'`. `withTest` always `unshift`s Test even when GET `/gateways` omitted it. Cards always show Test Ready. Host `PayProviders.Listed` **drops** Test when `env.IsProduction()`. Create payment-link `provider=test` then 400 `"test processor is not enabled"` — after product 201 (038).

Local/dev matches (host lists Test). A production merchant **build of this same JS** is a client bug.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **30–38**, **123–144**.
- `apps/lazuar-pay-merchant/src/lib/processors.ts` **1**.
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **126–177**.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs` **18–22**.
- `apps/lazuar-pay-merchant/src/locks.test.ts` — asserts `'test'` is always there.

## Reproduction

Production host + this SPA. Test card Ready. Create Test link → 400. Orphan product.

## Blast radius

Production merchant UI. Locks.test will **fail if you fix** this without updating the grep.

## Suggested fix

If GET `/gateways` `processors` does not include `test`, do not `unshift` it and do not render Test as Ready. Trust the host list. Keep `withTest` only when the list **does** include Test. Update honesty locks.

## Tests

- Existing locks require Test in processors.ts / CheckoutsPage.
- After fix: lock “Test only when host listed it.” Host Production mint 400 already exists or belongs in 006.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B8
- `plans/019-evals/04-processors-vault-test.md` §B3
- `plans/019-evals/08-contracts-spec-honesty.md` bug 6
