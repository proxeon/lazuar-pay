---
number: "027"
id: PAY-VAULT-002
severity: P1
status: resolved
source: plans/019-evals/04-processors-vault-test.md
head: "9f04ad58"
---

# 027 — PUT accepts any CHIP `webhook_secret`; PEM is only checked at verify

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/04-processors-vault-test.md` B6
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

CHIP webhook verify imports PEM at parse time. PUT stores whatever string as `WebhookCiphertext`. A one-line paste that is not a PEM still **200s**. First CHIP webhook then 400/503. Merchant textarea (018) makes a real PEM pasteable; it does not validate.

Do **not** solve this with `ChipWebhookRegistrar` on PUT (IsolationTests / refuse). Staff paste PEM. Validate-on-PUT is in-scope.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` **46–63**, **98–99** — require non-empty webhook_secret; wrap.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs` — `ImportFromPem`.
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **241–249** — Textarea, no client parse.
- `apps/lazuar-pay-merchant/src/locks.test.ts` — greps Textarea + PEM copy.

## Reproduction

PUT CHIP with `webhook_secret: "not-a-pem"`. 200, `webhook_configured: true`. CHIP webhook 400 invalid key.

## Blast radius

Dogfood: Ada thinks CHIP is on, first paid buyer fails verify. Production same. Not a forge path (bad PEM does not verify).

## Suggested fix

`ImportFromPem` (or equivalent) on PUT; 400 `"webhook_secret must be a CHIP PEM"` if it throws. Do not register webhooks on PUT. Do not log the PEM.

## Tests

- Missing: PUT CHIP webhook_secret `"nope"` is 400. PUT a fixture PEM 201/200 and `webhook_configured true`.

## Source reports

- `plans/019-evals/04-processors-vault-test.md` §B6
- `plans/019-evals/02-merchant-frontend.md` PEM note
- `plans/019-evals/06-rails-webhooks-fulfillment.md` §G9
