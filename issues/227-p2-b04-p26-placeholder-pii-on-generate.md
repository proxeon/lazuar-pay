---
number: "227"
id: B04-P26
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 227 — B04-P26 — Placeholder PII on generate

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P26 — P2 — Placeholder PII on generate

**Where.** `GatewayCommon.PlaceholderEmail = "customer@example.com"`; CHIP/Billplz/Xendit `ResolveEmail`. Razorpay phone `+60100000000`.

**What.** Blank buyer email becomes a real processor customer record on the tenant account. Not a verify skip; it is a data-quality / support bug.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
When generate-checkout is called with a blank buyer email, CHIP / Billplz / Xendit do not fail closed. `GatewayCommon.ResolveEmail` substitutes `customer@example.com`. That string is sent as CHIP `client.email`, Billplz `email`, Xendit `payer_email`. Razorpay does **not** use `ResolveEmail` (blank email is sent blank) but if `customer_phone` is missing from metadata it always sends `contact: +60100000000`. The processor then creates or attaches a real customer object on the **tenant’s** Stripe-equivalent account (CHIP client, Billplz bill, Xendit invoice, Razorpay payment-link customer). Receipts, support inboxes, and “customers” lists fill with Lazuar’s dummy identity. This is not a webhook verify skip. Quotes already refuse this address (158 / 192); the cashier generate path does not.

### Still present?
**STILL BROKEN**

Placeholder is still a public constant and the default:

```14:30:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
    public const string PlaceholderEmail = "customer@example.com";
    // ...
    public static string ResolveEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? PlaceholderEmail : email;
```

Call sites still use it on generate:

```59:60:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
                email = GatewayCommon.ResolveEmail(customerEmail),
                full_name = clientName
```

```96:97:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
            ["email"] = GatewayCommon.ResolveEmail(customerEmail),
            ["name"] = GatewayCommon.ExtractName(customerEmail),
```

```269:269:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs
            ["payer_email"] = GatewayCommon.ResolveEmail(customerEmail),
```

Razorpay dummy phone is still unconditional on generate:

```261:276:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs
        metadata.TryGetValue("customer_phone", out var customerPhone);
        var finalName = !string.IsNullOrWhiteSpace(customerName) ? customerName : GatewayCommon.ExtractName(customerEmail);
        var finalPhone = !string.IsNullOrWhiteSpace(customerPhone) ? customerPhone : "+60100000000";
        // ...
                ["email"] = customerEmail,
                ["contact"] = finalPhone
```

CHIP off-session clone also falls back to the same placeholder (`ChipCollectGatewayAdapter.cs:525, 536, 540`). Stripe generate omits `CustomerEmail` when blank (`StripeGatewayAdapter.cs:561, 578`) — honest. `GatewayCommonTests.ResolveEmail_UsesPlaceholderWhenBlank` **locks** the substitution. Quote UI already rejects `customer@example.com` (`fix/158-quote-placeholder-email`, `fix/192-quote-refuse-placeholder-email`); M2M `CreateIntegrationCheckoutCommand.CustomerEmail` can still be `""`.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` — the substitution.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — generate + off-session clone.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` — bill email.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` — `payer_email`.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` — dummy MY mobile.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — contrast: blank email omitted.
- `apps/lazuar-api/Modules/Payments/Application/Queries/GenerateCheckoutSessionQueryHandler.cs` / `CheckoutSessionCashier.cs` — pass caller email through.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/GatewayCommonTests.cs` — locks the placeholder.
- `issues/158-p2-b09-u29-quote-placeholder-email.md` / `192` — quote side already refuse.

### Tests
- Existing tests: `GatewayCommonTests.ResolveEmail_UsesPlaceholderWhenBlank` (asserts `null`/`""`/`"  "` → `customer@example.com`). `RazorpayGatewayAdapterTests.BuildPaymentLinkRequest_NeverMintsCardRegistration` uses `buyer@example.com` and does **not** assert `contact`. CHIP/Billplz/Xendit generate tests do not assert the email field. No test that generate refuses a blank email.
- Whether any test would fail if the bug is still there: **no**. `ResolveEmail_UsesPlaceholderWhenBlank` would **fail if you fixed** the default without updating the test.
- What a first regression test should assert: `ResolveEmail` / CHIP / Billplz / Xendit generate with blank email throws or returns `Success=false` with a stable error (do not POST `customer@example.com`). Razorpay `BuildPaymentLinkRequest` with no `customer_phone` omits `contact` (or fails) rather than `+60100000000`. Stripe already omits email — keep that. Align with 158: `customer@example.com` is never a valid buyer.

### Reproduction today
Arrange: tenant CHIP (or Billplz / Xendit) active. Act: Commerce/M2M generate with `customerEmail=""` (M2M allows empty string through the DTO). Inspect the outbound processor payload (CHIP purchase `client.email`, Billplz bill, Xendit invoice). Assert it is `customer@example.com`. Razorpay payment link `customer.contact` is `+60100000000`. CHIP dashboard / Billplz collection shows a customer named from `ExtractName(null)` → `"Customer"`.

### Blast radius
Every blank-email checkout on CHIP, Billplz, Xendit (and every Razorpay link without a phone). Processor customer lists, receipts, and support tickets show Lazuar’s dummy identity — **PII quality**, not a signature skip. Possible processor rejection if they later ban `example.com`. Shared dummy email can merge unrelated buyers on the processor (CHIP client-by-email). Frequency: high on M2M if integrators omit email; lower on Commerce if hop-1 collected one. Stripe path is clean.

### Suggested fix
Fail closed in `GatewayCommon.ResolveEmail` or at the cashier: blank / `customer@example.com` is `GatewayError` / `PaymentIntegrationException` before HTTP to the processor. Razorpay: omit `contact` when unknown; do not invent `+60100000000` (110 already killed dummy phones on LHDN). Update `ResolveEmail_UsesPlaceholderWhenBlank` so it no longer locks the hole. Do not change TypeSpec. Do not touch quote 158 (already correct). No Wave 5 / e-mandate.

### Evaluation notes
Duplicates: 158 / 192 (quote); 110 (LHDN dummy phone); 225 (Razorpay **off-session** email branch is dead — generate phone is the live twin). Severity still **P2**. Not blocked. Residual after 161-200 fail-closed: 158/192 closed the quote hole only; generate still invents PII.


