---
number: "311"
id: B09-U43
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 311 — B09-U43 — Xendit/Razorpay/Stripe first-save does not require webhook secret

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U43 — Xendit/Razorpay/Stripe first-save does not require webhook secret (P2)

Vaults accept a key without a callback/whsec. First Billplz *does* require 128-char X-Signature. Inconsistent; Xendit webhooks will 401 until they come back.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops and admin payment vaults let a merchant activate Stripe, Razorpay, or Xendit with only the API/secret key. The webhook / x-callback-token / whsec field is optional on first save. Billplz is the exception: first save refuses a missing or non-128-char X-Signature. The API `UpdatePaymentConfigCommandHandler` also allows a null webhook secret as long as an API key exists. Inbound `POST /webhooks/payments/{gateway}/{tenant}` then hits `ProcessGatewayWebhookCommandHandler`, which throws `InvalidOperationException("Webhook secret not configured for this tenant gateway.")` before the adapter runs. That exception is not mapped to 401 on the webhook route (it is excluded from the catch and becomes a 500). If a secret *is* stored but wrong/empty, Xendit’s adapter fails `VerifyCallbackToken` and the handler throws “Webhook signature verification failed”. Paid Xendit/Razorpay/Stripe events will not fulfill until someone re-opens the vault and pastes the callback token. The audit’s “401” is the user-visible “processor retries / we never ACK”; the status code in this tree is 500, not 401.

### Still present?
**STILL BROKEN**

Ops first-save gates (admin `PlatformPaymentSettingsPage.tsx` 115–130 is the same clone):

```123:138:apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx
    if (gatewayType === "STRIPE") {
      if (!hasSecretKey && !secretKey.trim()) {
        toast.error("Secret Key is required for first-time Stripe configuration.");
        return;
      }
    }

    if (gatewayType === "RAZORPAY" && !hasApiKey && !apiKey.trim()) {
      toast.error("API Key is required for first-time Razorpay configuration.");
      return;
    }

    if (gatewayType === "XENDIT" && !hasApiKey && !apiKey.trim()) {
      toast.error("API Key is required for first-time Xendit configuration.");
      return;
    }
```

Billplz contrast (ops 89–94): `if (!hasWebhookSecret && webhookSecret.trim().length !== 128)`.

PUT body still sends `webhook_secret: webhookSecret.trim() || undefined` (ops 142–151). Handler persists a first-time row with a null webhook:

```147:170:apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs
        if (config == null && string.IsNullOrEmpty(resolvedPlainApiKey))
        {
            throw new BusinessRuleValidationException(
                new GenericBusinessRule("API key (or Stripe secret key) is required for first-time gateway configuration."));
        }
        ...
        if (config == null)
        {
            config = new TenantPaymentConfiguration(
                request.OrganizationId,
                gatewayType,
                encryptedApiKey,
                encryptedWebhook,
                finalMerchantId,
                isActive,
                environment);
```

Webhook intake:

```58:62:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, request.GatewayType, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant gateway.");
        }
```

```317:321:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs
    internal static bool VerifyCallbackToken(string webhookSecret, Dictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return false;
        }
```

CHIP is a third path: first save of the API key auto-fetches an RSA public key into `WebhookSecret` (handler 105–120). That is why U43 names Xendit/Razorpay/Stripe only.

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` — merchant vault validation.
- `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` — Hub vault, same first-save rules, no environment select (U44).
- `apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` — allows null webhook.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — fail-closed without secret.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` — webhook route; `InvalidOperationException` not mapped.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` — callback token verify.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs` `Handle_MissingConfig_ThrowsInvalidOperation`.
- Unused clones `PaymentSettingsModal.tsx` (ops) — do not patch those as product.

### Tests
- Existing tests that touch this path: `ProcessGatewayWebhookCommandHandlerTests.Handle_MissingConfig_ThrowsInvalidOperation` (asserts the *intake* fail-closed). `PaymentSecretsAndSoftDisableTests` (encrypt/mask). No test that the **ops/admin first-save UI** requires a webhook secret for Xendit/Razorpay/Stripe. No test that `UpdatePaymentConfig` rejects first-save without `WebhookSecret` for those gateways.
- Whether any test would fail if the bug is still there: **No** for the UI hole. The webhook test *passes while the vault allows the bad save* — it cements the 500/throw, not the form.
- What a first regression test should assert: first-save Xendit/Razorpay/Stripe without `webhook_secret` is refused in the handler (and the form). Billplz 128-char rule stays. CHIP still auto-registers. Do not require rotating an already-stored secret on later saves (`hasWebhookSecret` leave-blank).

### Reproduction today
Ops → Payment Gateways → Xendit. Paste only `xnd_development_…`, leave callback token blank, Save. Assert: toast success, `has_webhook_secret` false on GET. Fire a Xendit invoice webhook at `/api/v1/webhooks/payments/xendit/{tenant}`. Assert: handler throws “Webhook secret not configured”; checkout stays OPEN. Repeat Stripe with only `sk_test_…` and empty `whsec_`. Repeat Billplz without 128-char X-Signature: form blocks.

### Blast radius
Any merchant (or Hub, via admin) who “finishes” BYOK for Xendit/Razorpay/Stripe and goes live. First paid session never completes; processor retries; support sees a configured gateway and a silent miss. Money: captured at the processor, not fulfilled in Lazuar (same class as other webhook-secret holes). Frequency: every first-time Xendit/Razorpay/Stripe save. PII: none extra. Ops will not notice until the first buyer pays.

### Suggested fix
Mirror Billplz: on first save (`!hasWebhookSecret`) require a non-empty Stripe `whsec_`, Razorpay webhook secret, and Xendit callback token (no 128-char rule for those). Optionally enforce the same in `UpdatePaymentConfigCommandHandler` for those three gateway types so API clients cannot skip the UI. Leave-blank-to-keep on later saves. Do not implement Stripe Billing, homemade e-mandate, or WhatsApp. No TypeSpec regen unless you add a new required field on the existing DTO (prefer handler validation over spec churn).

### Evaluation notes
009 residual after cf0f07d (Xendit fields exist; callback still optional). Adjacent: 073 (Xendit token is a shared secret, not a body HMAC — still true, `VerifyCallbackToken`). 122 (500s no longer echo `Exception.Message` — merchants see a generic 500, not “secret not configured”). Severity still P2 as a first-save footgun; first live Xendit payment is closer to P1 operationally. Not blocked. Pair with U44 if touching admin vault.

