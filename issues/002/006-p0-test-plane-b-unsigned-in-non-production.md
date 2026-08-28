---
number: "006"
id: PAY-TEST-001
severity: P0
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 006 — Test Plane B is unsigned in every non-Production environment

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B1 (also `04-processors-vault-test.md` B1/B2, `01-pay-host-seams.md` B4)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`TestWebhook.Parse` never verifies a signature. `WebhookEndpoints.Handle` skips vault credentials when the path provider is `test`. `PayProviders.AllowsTest` is `!env.IsProduction()`, so **Development, Testing, and Staging** all accept `POST /v1/webhooks/test/{orgId}` with a JSON body.

Local `launchSettings` is Development. A Cloudflare tunnel to 8081 (the same tunnel Billplz needs) makes Test webhooks world-writable. Combined with 007 (amount optional) this is unauthenticated fulfill of any open `provider=test` checkout whose id leaked.

Start on Test **also** fulfills in-process (`PublicPayEndpoints` after `CreateHostedUrlAsync`) — that is the intended dogfood door. The **webhook** is a second, unsigned money door.

`TestRailTests.Webhook_pays_open_test_checkout` posts unsigned JSON and expects a receipt — the suite **locks the hole**.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs` **9–57** — parse JSON only.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **49–55**, **77** — skip creds; `TestWebhook.Parse(raw)`.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs` **21–22** — `AllowsTest` = `!IsProduction()`.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **176–186** — Test start auto-fulfill.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs` **11–20** — redirect to success URL, no secrets.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs` **41–59** — unsigned webhook → document count 1.
- `apps/lazuar-pay-merchant/src/lib/processors.ts` **15** — “Local only. No secrets.” Copy is stricter than the host.

## Reproduction

```bash
# Development host, any open Test checkout id
curl -sS -X POST http://localhost:8081/v1/webhooks/test/t1 \
  -H 'Content-Type: application/json' \
  -d '{"id":"evt_x","checkout_id":"<open-test-checkout-id>"}'
# 200, Official Receipt
```

Staging named anything except `Production` behaves the same.

## Blast radius

Any non-Production process reachable from the internet. Fake `RCPT-` rows that look like Stripe in the receipts table (same title, unless a later issue adds `provider`). Production compose sets `ASPNETCORE_ENVIRONMENT=Production` and mint of Test 400s — Production is closed **if the env name is really Production**.

## Suggested fix

Pick one:

1. Narrow `AllowsTest` to `IsDevelopment() || IsEnvironment("Testing")` (or `Pay:EnableTestProcessor` default false). Add `Test_webhook_in_production_is_400` with `UseEnvironment("Production")`. Staging-shaped factory too.
2. **Delete** the Test webhook route; keep start-to-pay as the only Test money door.
3. If a webhook must exist: HMAC the body with a Testing secret; fail closed when missing.

Do not enable Test in Production. Do not invent a factory. Merchant “always offer Test” is 042.

## Tests

- Existing: `Webhook_pays_open_test_checkout` (locks unsigned pay). `Mint_and_start_pays_without_keys`.
- Missing: Production env Test mint 400; unsigned Test webhook 400 in Production **and** Staging. After fix, the existing webhook test must use the new authenticator or be deleted if the route dies.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B1
- `plans/019-evals/04-processors-vault-test.md` §B1 §B2
- `plans/019-evals/01-pay-host-seams.md` §B4
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-2
