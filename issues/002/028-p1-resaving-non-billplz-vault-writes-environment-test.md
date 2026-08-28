---
number: "028"
id: PAY-VAULT-003
severity: P1
status: resolved
source: plans/019-evals/04-processors-vault-test.md
head: "9f04ad58"
---

# 028 — Re-saving a non-Billplz vault always writes `environment=test`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/04-processors-vault-test.md` B5
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

PUT defaults `environment` to `"test"` when the body omits it. Billplz **requires** the field (400 if missing). Stripe/CHIP/Xendit/Razorpay UIs do not send `environment` except Billplz. Re-saving a “live” CHIP brand (if you ever stored live) overwrites `environment=test`.

For most rails environment is unused at runtime (Billplz sandbox vs live URL is the consumer). The GET hydrate still shows `test` after a live-key rotate. Honesty + Billplz-only runtime field stored on every row (04 G4).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` **76–85**, **117–123**.
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **86–100** — only Billplz sends `environment`.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` **84** — `Environment` default `"test"`.

## Reproduction

PUT Billplz `environment: live`. PUT Stripe without environment. Stripe row `environment=test`. PUT CHIP again to rotate `sk_` — CHIP `environment` becomes `test` even if GET previously showed something else (CHIP UI never sent live).

## Blast radius

Mostly metadata. Billplz is the rail where environment is money (sandbox vs live host). Merchant Billplz select **does** send it. Non-Billplz clobber is honesty.

## Suggested fix

Omit field on PUT → **keep** existing row environment (or null). Require environment only for Billplz. Do not default other rails to `test` on rotate.

## Tests

- Missing: PUT Stripe without environment leaves previous environment unchanged; Billplz missing environment still 400.

## Source reports

- `plans/019-evals/04-processors-vault-test.md` §B5 §G4
