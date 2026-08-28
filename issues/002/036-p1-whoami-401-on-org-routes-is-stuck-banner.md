---
number: "036"
id: PAY-MERCH-002
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 036 — Whoami 401 on org routes is a stuck banner

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B2
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`getWhoami` maps HTTP 401 to thrown `'unauthorized'`. `HomePage` then `signinRedirect()`. `OrgLayout` `catch` sets `error` to that string and renders `<p role="alert">unauthorized</p>` with **no** retry and **no** chrome. Expired access after silent-renew failure lands here (044).

## Related files

- `apps/lazuar-pay-merchant/src/lib/payApi.ts` **32–38**.
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx` **47–60**.
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx` **29–34**.

## Reproduction

Expire the access token on `/o/{id}/overview`. Red sentence “unauthorized”. No Sign in.

## Blast radius

Every session timeout on an org URL. Support: “Pay is broken.”

## Suggested fix

Same as HomePage: on `'unauthorized'` call `signinRedirect()`, preserving `returnTo`. Surface One 503 `detail` (“Identity provider unreachable”) instead of `whoami 503`.

## Tests

- Missing: OrgLayout 401 → redirect (component). HomePage already redirects.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B2
