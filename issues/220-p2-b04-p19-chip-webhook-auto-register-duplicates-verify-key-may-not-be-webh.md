---
number: "220"
id: B04-P19
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 220 — B04-P19 — CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P19 — P2 — CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key`

**Where.** `UpdatePaymentConfigCommandHandler.cs:116-138`. No GET-list. PEM from `GET /public_key/` (company), not from the created webhook object. CHIP docs: webhook deliveries use a dedicated key pair.

**What.** Re-save → N webhook rows at CHIP, N deliveries of the same event (EventId dedupes after the first). Wrong PEM → every delivery `Verified=false` → 500. Unsoaked; residual.

## Evaluation (current tree, 2026-08-18)

### What the bug is
When an operator pastes a new CHIP API key, `UpdatePaymentConfigCommandHandler` always `GET https://gate.chip-in.asia/api/v1/public_key/` (the **company** RSA PEM) and then `POST https://gate.chip-in.asia/api/v1/webhooks/` with callback `{ApiBaseUrl}/webhooks/payments/chip/{orgId}` and events `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`. There is no list-or-update of existing CHIP webhook rows for that callback URL, so every key re-save creates another CHIP subscription. CHIP will then deliver the same purchase event N times. After `a1afc09` / issue 063 the Payments log unique is per tenant + EventId / business key, so deliveries 2..N ACK 200 and do not re-fulfill — they still hammer CHIP and our intake. The PEM stored as `TenantPaymentConfiguration.WebhookSecret` is the company key, not `Webhook.public_key` from the created webhook object. CHIP’s own callbacks page says success-callback payloads use the company key (`GET /public_key/`) while **webhook** deliveries use a dedicated key pair (`Webhook.public_key`). If CHIP signs `POST /webhooks/payments/chip/...` with that dedicated key, `ChipCollectGatewayAdapter.ParseWebhookAsync` `ImportFromPem(webhookSecret)` + `VerifyData` fails, the handler treats `Verified=false` as `InvalidOperationException`, and CHIP retries a 500 storm. This tree has used company-PEM since Wave 0; soak status is still not in-repo.

### Still present?
**STILL BROKEN**

Duplicate register is still a bare POST with no GET-list and no stored CHIP webhook id:

```105:138:apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs
        // Automatically fetch RSA Public Key and register webhooks when a new CHIP key is supplied
        if (gatewayType == "CHIP" &&
            !SecretVaultExtensions.IsKeepExistingSecret(request.ApiKey) &&
            !string.IsNullOrEmpty(resolvedPlainApiKey))
        {
            // ...
                var pubKeyResponse = await client.GetAsync("https://gate.chip-in.asia/api/v1/public_key/", ct);
                // ...
                resolvedPlainWebhook = rawKey.Trim('"').Replace("\\n", "\n");
                // ...
                var webhookPayload = new
                {
                    title = "Lazuar Platform Webhook",
                    events = new[] { "purchase.paid", "purchase.payment_failure", "payment.refunded", "purchase.preauthorized" },
                    callback = webhookUrl
                };

                var webhookResponse = await client.PostAsJsonAsync("https://gate.chip-in.asia/api/v1/webhooks/", webhookPayload, ct);
                webhookResponse.EnsureSuccessStatusCode();
```

Verify still imports that same stored PEM:

```139:146:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
            using var rsa = RSA.Create();
            rsa.ImportFromPem(webhookSecret);

            bool isValid = rsa.VerifyData(bodyBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValid)
            {
                _logger.LogWarning("CHIP Collect RSA signature verification failed.");
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "RSA signature verification failed."));
```

Handler still 500s on `!Verified` (`ProcessGatewayWebhookCommandHandler.cs:78-80`). Localhost is still rewritten to `lazuar-local-dev.com` (`UpdatePaymentConfigCommandHandler.cs:125-128`). I did not find a CHIP webhook-id column, a GET-list helper, or a test fixture that POSTs `/api/v1/webhooks/`. The PEM-mismatch half remains **unsoaked** (no live CHIP delivery captured in this repo); the duplicate-row half is visible in code.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` — only writer of CHIP webhook subscriptions and of the stored verify PEM.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — RSA verify against that PEM; `Verified=false` on mismatch.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — turns verify failure into HTTP 500 + gateway retry.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Queries/GetPaymentConfigQueryHandler.cs` — never returns the PEM (correct); ops cannot see which key is stored.
- `apps/lazuar-api/Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs` — `WebhookSecret` is a single blob; no CHIP webhook id.
- CHIP Collect callbacks / authentication docs (`docs.chip-in.asia/chip-collect/overview/callbacks`) — company PEM vs `Webhook.public_key`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ChipCollectGatewayAdapterTests.cs` — signs fixtures with a **test** PEM we generate; does not exercise CHIP register.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/PaymentSecretsAndSoftDisableTests.cs` — encrypt/mask only.

### Tests
- Existing tests that touch this path: `ChipCollectGatewayAdapterTests.ParseWebhook_MissingSignature_IsNotVerified`, `ParseWebhook_BadSignature_IsNotVerified`, `ParseWebhook_PurchasePaid_*` (local RSA, not CHIP register). `PaymentSecretsAndSoftDisableTests` does not call `UpdatePaymentConfigCommandHandler` CHIP branch. No `UpdatePaymentConfigCommandHandler` CHIP HTTP test exists under `apps/lazuar-api/tests/`.
- Whether any test would fail if the bug is still there: **no**. Nothing asserts “second CHIP key save does not POST `/webhooks/` again” or “verify PEM equals `Webhook.public_key` from the create response”.
- What a first regression test should assert: with a fake `IHttpClientFactory`, first CHIP save GETs `/public_key/` then POSTs `/webhooks/` once; second save of a **new** key either GET-lists and skips create when `callback` already exists, or PATCHes the stored webhook id; the PEM persisted for verify is the `public_key` field from the webhook object (or the test documents an explicit company-PEM fallback). A second test: adapter `ParseWebhookAsync` with company PEM vs a body signed by a different webhook key is `Verified=false` (today) / succeeds after the fix.

### Reproduction today
Arrange: tenant with CHIP already registered (one CHIP dashboard webhook pointing at `/webhooks/payments/chip/{tenantId}`). Act: ops Payment Settings → paste the same or a rotated CHIP secret key → Save (not the keep-existing mask). Observe CHIP dashboard now has two (or N) webhooks with the same callback. Pay a test purchase. CHIP POSTs the event N times; first delivery fulfills (`PAYMENT_COMPLETED:{purchaseId}`), later deliveries hit the existing log and ACK. If CHIP signed with `Webhook.public_key` and we stored company PEM, every delivery is `Verified=false` → HTTP 500 → CHIP retry queue. Assert: N CHIP rows after N key saves; verify PEM is not the webhook object’s `public_key`.

### Blast radius
Every CHIP merchant who re-saves credentials (key rotation, “save” click, env flip). Duplicate deliveries are ops noise + CHIP rate-limit risk, not double-cash after EventId/business-key work (063 / `a1afc09`). Wrong PEM is worse: **all** CHIP inbound money/fail/refund events 500 until someone pastes a matching key. PII is not leaked; money fulfillment is blocked, not doubled. Frequency: every CHIP config write that is not “keep existing”. Unsoaked in production from this repo.

### Suggested fix
Smallest correct change: (1) GET CHIP’s webhook list (or store the created webhook id on `TenantPaymentConfiguration`) and skip/PATCH when `callback` already equals our URL; (2) after create/list, persist `Webhook.public_key` as `WebhookSecret` for inbound `/webhooks/payments/chip/...`, and keep company `GET /public_key/` only if you still verify a CHIP **success_callback** (we do not). Do not register a homemade e-mandate. Do not emit Stripe Billing `subscription.updated`. Do not regen TypeSpec. Keep EventId namespacing. Localhost → tunnel rewrite can stay.

### Evaluation notes
Duplicates: related to B04-P01 / issue 005 (we now *want* `purchase.preauthorized` delivered — duplicate subscriptions amplify that path); B04-P13 / 070 / 085 (`payment.refunded` is still subscribed here and still not mapped — see 223). Severity still **P2**: duplicate rows do not double-book after log unique; PEM mismatch is residual/unsoaked, not a proven live outage. Not blocked by another issue. Residual after 161-200 fail-closed work: 063 scoped EventId by tenant (helps shared-brand first-writer races) but did not touch register-or-PEM.


