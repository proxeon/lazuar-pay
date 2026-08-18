---
number: "225"
id: B04-P24
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 225 — B04-P24 — Dead / unused in this module

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P24 — P2 — Dead / unused in this module

- `ChipCollectGatewayAdapter._configuration` (injected, never read).
- `BillplzPublicBase.ProductionHosts` (filled, discarded).
- `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` (no Payments readers).
- `xendit_payment_methods` (no Payments/Commerce setter).
- Razorpay `ChargeOffSessionAsync` email/phone branch.
- Estimated fee parameters on `ParseWebhookAsync` (handler always 0).
- Payments README §3 “stateless checkouts”, §6 two adapters, overview “FPX, Curlec”.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The Payments module still carries several symbols that compile, are tested or injected, and do not drive production behaviour (or drive the wrong story in the README). An implementer reading the matrix / README will believe DuitNow QR, hosted wallets, e-mandate, Xendit channel filters, Billplz hostname→live inference, CHIP test-host selection, Razorpay off-session buyer PII, and estimated webhook fees are product. They are not. This is honesty / dead-code, not a fulfillment hole.

### Still present?
**DOCS / HONESTY ONLY** (code listed in the audit is still dead; nothing here captures or drops money by itself)

Current tree vs the audit list:

- `ChipCollectGatewayAdapter._configuration` — still injected, only assigned, never read (`ChipCollectGatewayAdapter.cs:21, 32`). CHIP always uses `https://gate.chip-in.asia/api/v1/`.
- `BillplzPublicBase.ProductionHosts` — still allocated then discarded (`BillplzPublicBase.cs:15-21, 39-41`). Live vs sandbox still follows `App:BillplzEnvironment` then tenant `environment`, never Hub hostname (LP-182 leftover).
- `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` — still **no production readers** under `apps/lazuar-api` outside `PaymentGatewayCapabilities.cs`. Commerce **does** read `SupportsOffSession` / `IsReminderOnlyGateway` / `RequiresMarkRefunded` (InitiateCheckout, BillingEngine, dunning, RecordRefund). The three unread flags exist for tests + future ops.
- `xendit_payment_methods` — still only read inside `XenditGatewayAdapter.ResolveRequestedPaymentMethods` (`XenditGatewayAdapter.cs:304`). Grep of Commerce / ops / cashier found **no setter**. Production invoices use Xendit dashboard defaults. Test `BuildInvoicePayload_FiltersUnknownChannels` still exercises the unused hook.
- Razorpay `ChargeOffSessionAsync` email/phone branch — still dead. Notes are built in-method with `type` / `subscription_id` / `tenant_id` / `receipt` only (`RazorpayGatewayAdapter.cs:159-174`); `notes.TryGetValue("customer_email")` / `customer_phone` (`197-210`) can never hit. Capability `SupportsOffSession("RAZORPAY")` is false, so Billing never calls this (068).
- Estimated fee parameters on `ParseWebhookAsync` — handler still passes `0, 0, 0` (`ProcessGatewayWebhookCommandHandler.cs:74-76`). Billplz formula is dead (222 / 239).
- Payments README — still stale: §1 “Stripe, Billplz, FPX, Curlec”; §3 “Stateless regarding Checkouts” / “does not store pending checkout sessions”; §6 lists only Stripe + Billplz. `IntegrationCheckoutSessions` and five adapters exist.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — dead `_configuration`.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs` — dead `ProductionHosts`.
- `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` — unread QR / wallet / e-mandate flags.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` — unused channel filter.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` — dead off-session PII branch.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — fee args 0.
- `apps/lazuar-api/Modules/Payments/README.md` — human-facing lie.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/PaymentGatewayCapabilitiesTests.cs` — locks the unread flags’ return values.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/XenditGatewayAdapterTests.cs` — `xendit_payment_methods` filter.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzPublicBaseTests.cs` — host/callback, not `ProductionHosts` use.

### Tests
- Existing tests: `PaymentGatewayCapabilitiesTests.Xendit_IsReminderOnly_AndHostsWallets` asserts `SupportsDuitNowQr("XENDIT")` / `SupportsHostedWallet` / `SupportsEmandate` false — **locks the matrix**, not a reader. `SupportsApiRefund_StripeChipRazorpay` name still omits Xendit (cosmetic lie from the 009 catalog). `XenditGatewayAdapterTests.BuildInvoicePayload_FiltersUnknownChannels`. No test reads Payments README. No test that `_configuration` is unused (cannot fail).
- Whether any test would fail if the bug is still there: **no**. Several tests would fail if you *deleted* the unread flags without updating them.
- What a first regression test should assert: after cleanup, either (a) a Commerce/ops reader exists for each remaining flag, or (b) the flag is gone and `PaymentGatewayCapabilitiesTests` no longer mention it. README test is optional; prefer editing the README.

### Reproduction today
Arrange: read-only. Act: grep `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` / `xendit_payment_methods` / `ProductionHosts` / `_configuration` under `Modules/Payments` and `Modules/Commerce`. Assert: QR/wallet/e-mandate have zero non-test callers; Xendit metadata key is never set; CHIP adapter never reads `IConfiguration`; Billplz `ProductionHosts` is assigned to `_`. Open `Modules/Payments/README.md` §3 and §6 — they still deny `IntegrationCheckoutSessions` and three adapters.

### Blast radius
Implementers and ops copy, not buyers. Risk is building a DuitNow / e-mandate / Xendit-channel UI on flags nobody honors (Wave 5 / homemade e-mandate is explicitly out of wrap-rails). Razorpay off-session dead branch cannot double-charge because capability is false. README “stateless checkouts” hid M2M sessions (006 / 226) from earlier readers. No money, no PII.

### Suggested fix
Delete or `pragma` the CHIP `_configuration` field, or actually use it to pick a CHIP test host if CHIP documents one (do not invent). Delete `ProductionHosts` or restore hostname inference **only** with the LP-182 comment’s forbid on `pay-local.lazuar.com`. Either wire `SupportsDuitNowQr` / `SupportsHostedWallet` to generate-time allow-lists or drop them from the public matrix and the tests. Do **not** set `SupportsEmandate` true. Leave `xendit_payment_methods` as a documented unused hook or delete it. Rewrite Payments README §1/§3/§6 to five adapters + `IntegrationCheckoutSessions`. Remove unused `ParseWebhookAsync` fee args in a follow-up with 222/239, not a drive-by. No TypeSpec regen. No WhatsApp / Xero / Wave 5.

### Evaluation notes
Duplicates: 222 / 239 (fee args); 068 (Razorpay reminder-only — registration link already removed); 227 (Razorpay dummy phone is live on **generate**, not this dead off-session branch). Severity still **P2**. Not blocked. Residual after 161-200: none of the fail-closed work claimed these symbols. `SupportsApiRefund` **is** used conceptually by Commerce mark-refunded; do not delete that one.


