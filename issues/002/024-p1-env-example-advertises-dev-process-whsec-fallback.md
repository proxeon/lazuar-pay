---
number: "024"
id: PAY-SEC-002
severity: P1
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 024 — `.env.example` still advertises a Dev process `whsec_` fallback

- **Severity:** P1 (doc lie; leftover forge path if operators trust it)
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Live `StripeWebhook.ResolveSecret` uses process `Pay:StripeWebhookSecret` only when environment is **Testing** and ciphertext is empty. Development with NULL `WebhookCiphertext` is 503, not process env. README is honest (“Testing-only”). `.env.example` still says “Dev fallback only; Production uses per-org webhook_secret.”

Operators who paste a platform `whsec_` into Development `.env` and skip PUT `webhook_secret` will not verify Stripe (503). Operators who think Development still has the 014 forge-all-orgs fallback are also wrong — but the example file teaches the old vector.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs` **78–91**.
- `apps/lazuar-pay/.env.example` (Stripe webhook / process fallback comments).
- `apps/lazuar-pay/README.md` **67**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs` — missing secret 503 after nulling ciphertext **in Testing**.

## Reproduction

Read `.env.example` vs `ResolveSecret`. They disagree.

## Blast radius

Misconfigured dogfood (no Stripe verify). Not the 014 forge-all-orgs path in Development **code**. Doc must not resurrect it.

## Suggested fix

Comment: Testing-only; PUT `webhook_secret` is required for every real rail. Do not read process env in Development. Do not put a real `whsec_` in gitignored `.env` as a platform fallback.

## Tests

- Existing: Testing-only fallback coverage. Production empty ciphertext 503.
- Doc fix is the issue. Optional: a string-lock test is overkill; just edit the example.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B9
- `plans/019-evals/10-honesty-bugs-gaps.md` §016 P0-1 / P0-E FIXED
