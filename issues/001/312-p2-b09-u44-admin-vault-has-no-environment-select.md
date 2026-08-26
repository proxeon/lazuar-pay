---
number: "312"
id: B09-U44
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 312 — B09-U44 — Admin vault has no environment select

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U44 — Admin vault has no environment select (P2)

Ops does (`230:242:PaymentSettingsPage.tsx`). Admin does not. Hub SaaS top-ups cannot mark test vs live in the UI.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Merchant ops can mark a vault row `test` vs `live` (“Processor environment”). That flag is what picks Billplz sandbox vs www; Hub hostname does not. The admin Platform Gateway Vault is the same form minus that `<select>`. `PUT /platform/payment-config` already accepts `Environment`, but admin never sends it. The handler then infers only from a Stripe-shaped `sk_live_` / `sk_test_` key, else keeps the existing value, else **defaults to `test`**. A superadmin pasting a live Billplz / Xendit / CHIP / Razorpay key for Hub SaaS credit top-ups therefore stores `environment=test` unless they already had a live row. Top-ups can hit the sandbox processor or be rejected as key/host mismatch.

### Still present?
**STILL BROKEN**

Ops has the control (lines moved to 235–248 after role-gating):

```235:248:apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx
                  <div className="space-y-1.5 sm:col-span-2">
                    <label className="text-[11px] font-semibold text-[#09090b]">Processor environment</label>
                    <select
                      value={environment}
                      onChange={(e) => setEnvironment(e.target.value as "test" | "live")}
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]"
                    >
                      <option value="test">Test / sandbox (Billplz sandbox, Stripe sk_test_)</option>
                      <option value="live">Live (Billplz www, Stripe sk_live_)</option>
                    </select>
                    <p className="text-[11px] text-[#71717a]">
                      Hub hostname does not pick Billplz sandbox vs live. A test API key cannot use a live config.
                    </p>
                  </div>
```

Admin `PlatformPaymentSettingsPage.tsx` has no `environment` state, no `<select>`, and the PUT body is only `gateway_type`, keys, `collection_id`, `is_active` (133–142). Grep of `apps/lazuar-admin` for `environment` is empty.

Platform API *does* accept the field:

```31:37:apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs
        group.MapPut("/payment-config", async Task<Ok<StatusResponse>> (
            SavePaymentConfigRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new UpdatePaymentConfigCommand(
                ctx.TenantId, req.Gateway_type, req.Api_key, req.Collection_id, req.Webhook_secret, req.Secret_key, req.Is_active, req.Environment);
```

```97:103:apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs
        var environment = request.Environment;
        if (string.IsNullOrWhiteSpace(environment))
        {
            environment = PaymentGatewayEnvironment.InferFromStripeShapedKey(resolvedPlainApiKey)
                ?? config?.Environment
                ?? PaymentGatewayEnvironment.Test;
        }
```

`InferFromStripeShapedKey` only understands `sk_live_` / `sk_test_` (`PaymentGatewayEnvironment.cs` 22–40). Billplz/Xendit/CHIP/Razorpay keys return null → default `test`.

### Related files
- `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` — missing select; first-save also skips webhook secret (U43).
- `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` — the clone to copy from.
- `apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` — already wired.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` — default-to-test.
- `apps/lazuar-api/Modules/Payments/Domain/PaymentGatewayEnvironment.cs` — Stripe-only infer.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Queries/GetPaymentConfigQueryHandler.cs` — GET already returns `Environment`.
- Do **not** confuse with 102 (LHDN SANDBOX/PROD was cosmetic). That is a different environment flag.

### Tests
- Existing tests that touch this path: `PaymentSecretsAndSoftDisableTests` (masking/hints). No admin page test. No test that platform PUT without `environment` stays `test` for a Billplz key.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: admin form includes the same test/live `<select>` and PUT sends `environment`. Handler: Billplz first-save with `environment: "live"` persists `live`; omitted environment + non-Stripe key → `test` (document that default).

### Reproduction today
Sign in to admin (`:3005` / `/admin/`). Open Platform Gateway Vault. Assert: no Processor environment control. Save live Billplz collection + API key. GET `/platform/payment-config`: `environment` is `"test"`. Compare ops `/workspace/payment-gateways` on a tenant: the select is there and is included in PUT.

### Blast radius
Hub operators charging SaaS / credit packs through the platform vault. A live key stored as `test` can miss captures or hit sandbox. Frequency: every Hub processor setup, rare but load-bearing. Money: platform GMV / credit top-ups. Not merchant BYOK (ops already has the select).

### Suggested fix
Copy the ops environment `<select>` + `environment` state + PUT field into `PlatformPaymentSettingsPage.tsx`. Default the select from GET `environment` (already on `PaymentConfigDto`). Do not infer live from Hub hostname. Do not add Stripe Billing. No TypeSpec regen — `Environment` is already on the save DTO.

### Evaluation notes
009 listed this as the residual after Xendit fields landed. Pair with U43 on the same file. Severity still P2 (Hub-only audience) but it is the only way to mark Billplz live for platform charges. Not blocked. 102 is not a duplicate.

