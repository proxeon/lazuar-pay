---
number: "029"
id: PAY-ONE-002
severity: P1
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 029 — Single process `Pay:OneWebhookSecret` vs One per-tenant `whsec_`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B2
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Even after 011, One issues a unique secret per webhook endpoint. Pay verifies with **one** process env var. Two workspaces cannot both deliver unless they share a secret (they do not) or Pay stores N secrets (it does not). 014 already called this a one-shop hatch. Still true.

Missing secret → 503 `"One webhook secret missing"`. There is no Pay code that POSTs One `/tenants/{id}/webhooks` to register `http://…/v1/one/webhooks`. Ops must do it. One SSRF blocks loopback.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` **24–28**.
- `apps/lazuar-pay/.env.example` — `Pay__OneWebhookSecret` commented.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` — no per-org One webhook ciphertext (contrast `GatewayCredentialRow.WebhookCiphertext`).

## Reproduction

Two One tenants, two endpoint secrets. Pay has one env var. Second tenant’s deliveries 401.

## Blast radius

Anything beyond dogfood of one tenant. Pause (011) cannot be multi-shop even with the dialect fixed.

## Suggested fix

Hatch, not cathedral: store per-org One `whsec_` the same way PSP webhook secrets are stored (writer PUT, SecretBox), **or** document “one Pay process = one One endpoint / one shop” until kernel. Do not hold a Zitadel PAT. Do not copy Modules/One.

011 first — a second secret does not help if packaging still 401s.

## Tests

- Missing: two orgs, two secrets, only matching org pauses. After a hatch exists.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B2 §S3
