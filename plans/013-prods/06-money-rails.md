# 06 — Production money rails (BYOK, Stripe or CHIP, webhooks) without Hub Payments module

**Family:** 013-prods  
**Paper:** 06 — gateways, keys, webhook HTTP, wrap-rails honesty  
**Date:** 21 August 2026  
**Type:** Uncondensed analysis. **Not an implementation.** **Do not** add a Stripe/CHIP/Billplz adapter, **do not** add a webhook route, **do not** encrypt a key column, **do not** flip `NP-GW-*` / `NP-API-002` / `NP-API-006`, **do not** retarget `lazuar-ops` at 8081.  
**Coordinate:** [07-fulfillment-ledger-docs.md](./07-fulfillment-ledger-docs.md) owns journal + `RCPT-` + SST. This paper names the **same-handler rule** and stops. It does not design accounts, series years, or tax math.

**Repos and SHAs (this write-up):**

| Tree | Path | Branch (this write) | HEAD |
|------|------|---------------------|------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` (`6f866ff0`) — `feat(pay): scaffold merchant and checkout Vite apps` |
| One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` (`0f79fe4`) — `WIP: Thu Aug 20 21:24:22 +08 2026` |

**Must-read sources actually opened (not summarized away):**

- Pay `plans/011-new-lazuar-pay/01-product.md` — wrap-rails, BYOK, dogfood sentence (“pastes CHIP or Stripe keys”).
- Pay `plans/011-new-lazuar-pay/03-first-slice.md` step 8; `11-checklist.md` `NP-GW-*`, `NP-API-002`, `NP-API-006`, `NP-XX-011`, `NP-XX-012`; `12-first-slice-tracker.md` steps 8–11.
- Pay `plans/012-one-to-pay/checklists/p50-money.md`; `09-webhooks-events.md` Plane B vs A vs C; `07-authz-roles.md` VIEWER honesty; `03-pay-host-seams.md` isolation bans.
- Pay `plans/008-evals/02-payments-adapters-rails.md` and `plans/009-bugs/04-payments-adapters-webhooks.md` — historical. Live `Modules/Payments` on this SHA is the authority when they disagree.
- Pay `plans/007-feats/05-malaysia-gateways.md` (Billplz vs CHIP as rail), `13-payments-refunds-rails.md` (wrap, not acquire).
- Hub `apps/lazuar-api/Modules/Payments/` — README, `IPaymentGatewayAdapter`, five adapter classes, `PaymentGatewayCapabilities`, webhook `Endpoints.cs`, `ProcessGatewayWebhookCommandHandler*`, `TenantPaymentConfiguration`, `PaymentWebhookLog`, `UpdatePaymentConfigCommandHandler`, `GetPaymentConfigQueryHandler`, `ChipWebhookRegistrar`, `BillplzPublicBase`, `CheckoutSessionCashier`, `AesSecretVault`.
- New host: `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/*`, `Program.cs`, `packages/pay-spec/main.tsp`, `tests/Lazuar.Pay.Tests/CheckoutTests.cs` + `IsolationTests.cs`, merchant Vite `VITE_PAY_API_URL` only.
- Old ops copy: `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` (wrap banners, `canSaveVault`).

**Assigned slice (binding):** Gateways, keys, webhook HTTP, wrap-rails honesty. Fulfillment / ledger / `RCPT-` is paper 07. One SPA / `lzr_sk_` / HMAC Plane A is paper 08. Merchant Vite chrome is paper 04. Hosted buyer page is paper 05.

---

## 0. How to read this paper

Three “webhook” stories already live in this family. Mixing them is how Hub ended up with an in-process catalog talking to itself **and** a public HMAC door **and** Stripe/CHIP/Billplz callbacks, all called “webhooks”:

| Plane | Direction | Job | This paper? |
|-------|-----------|-----|-------------|
| **A. One → Pay** | One POSTs signed JSON to Pay | `tenant.suspended`, membership | **No.** [012/09](../012-one-to-pay/09-webhooks-events.md). Mandatory **before live charges**, different HMAC, different table. |
| **B. PSP → Pay** | Stripe / CHIP / Billplz POST to Pay | Money. Verify, empty body 400, idempotent `(org_id, provider, event_id)` | **Yes. This paper.** |
| **C. Pay → merchant / second app** | Pay POSTs to a stranger | `payment.completed` and friends | **No.** Bezos door later. Old Hub outbound is museum. |

Plane B is **Pay’s** product: provider SDK or provider HMAC, provider headers, provider event ids. Do not share a table, a secret, or a route prefix with Plane A. Do not implement Plane A in order to “practice” Plane B. Do not wait for One to “hear” a charge before writing money — that is the parked-event tax [011/04](../011-new-lazuar-pay/04-linux-shape.md) already paid.

**Same-handler rule (named, not designed).** [011/01](../011-new-lazuar-pay/01-product.md) and `NP-FUL-001`: the first successful pay creates the subscription (or marks a one-off complete) **and** writes the ledger **in the same handler**. [011/07](../011-new-lazuar-pay/07-separate-vs-one-binary.md) rule 2: Pay must not `POST One/grant-buyer-access` as the only fulfillment. [012/09](../012-one-to-pay/09-webhooks-events.md): “Wait for One to hear `payment.succeeded` before writing the ledger” fails the slice.

On the new host that means: `POST /v1/webhooks/{provider}/{orgId}` verifies, inserts the idempotency row, **then calls the fulfillment function in-process** (journal + `RCPT-` + session `paid`) in **one Pay DB transaction**. It does **not** `PublishAsync` a `GatewayPaymentCompletedIntegrationEvent` onto a Payments outbox for Commerce to consume later. The Hub cashier’s README is explicit that Payments is “not a fulfillment engine” and “not an accounting ledger.” That split is the cathedral. New Pay is one binary; the webhook HTTP handler **is** the fulfillment entry. Paper **07** designs the journal lines and the `RCPT-` series. This paper stops at: the handler exists, it is the same request, it is not MediatR, it is not an inbox job.

**What is already on 8081 (do not pretend otherwise).** Focused Pay at this SHA has whoami, org-ready, and an **in-memory checkout fixture** (`status: "open"` only). `packages/pay-spec/main.tsp` has `POST`/`GET /v1/checkouts` and **no** webhook route. Merchant Vite `:5178` probes `/health`. IsolationTests ban `MediatR`, `BuildingBlocks`, `Modules.`, `lazuar-api`. Host csproj has **zero** `PackageReference`. There is no Pay database. There is no key column. There is no PSP client.

**008 / 009 are not current truth.** `008-evals/02` was written against a pre-fix tree (empty body 500, EventId = object id). `009-bugs/04` re-read `297ba98` / `30d07d2` (CHIP `$0` skip_capture never vaults; EventId unique **not** tenant-scoped; empty body still 500 in that report). Live adapters on `6f866ff0` have moved: empty body is **400**; webhook log unique is `(OrganizationId, Provider, EventId)`; CHIP `purchase.preauthorized` **with a token** maps to `PAYMENT_COMPLETED`; CHIP off-session now sends a `reference` / lookup; Razorpay `SetupFutureUsage` is discarded and always mints a payment link; Stripe setup without a PM is `Verified=false`. This paper quotes **live files**, and names 008/009 only as residual history.

---

## 0.1 Standing law (do not weaken)

1. **BYOK.** Money settles on the **merchant’s** Stripe / CHIP / Billplz account. Pay is software, not an acquirer, not a Merchant of Record, not Stripe Connect `application_fee_amount`.
2. **Wrap-rails.** Stripe/CHIP auto-charge **only if a vaulted PM / recurring token exists**. Billplz-class = reminder + hosted link, **never silent debit**. `SupportsEmandate` is false for every name. No homemade FPX e-mandate (`NP-XX-011`).
3. **One Malaysian rail you will dogfood (CHIP or Billplz), not five adapters day one.** Razorpay and Xendit stay `NP-LAT-002`. The factory of five is the Hub lie this slice refuses to copy.
4. **Stripe OR CHIP/Billplz for the first pasted keys** ([011/03](../011-new-lazuar-pay/03-first-slice.md) step 8, `NP-GW-001` notes). You do not need every rail on the first charge. You **do** need wrap-rails honesty on whatever you paste.
5. **Never Stripe Billing `subscription.updated` as source of truth** (`NP-XX-012`). Pay’s billing job (later, paper 07 / `NP-FUL-004`) mints a checkout or an off-session charge. Stripe Checkout is `mode=payment` (or `mode=setup` for vault, which is **not paid**).
6. **Never treat setup / setup-intent as paid** (`NP-GW-008`, [011/03](../011-new-lazuar-pay/03-first-slice.md) fail lock). Hub still emits `EventType: "PAYMENT_COMPLETED"` with `AmountPaid: 0` for setup-mode and for `setup_intent.succeeded`. Steal the HTTP extract of customer + PM. **Do not steal the event name.**
7. **Webhook:** verify signature; empty body **400**; idempotent on `(tenant, provider, event_id)` = `(org_id, provider, event_id)`; retry no-ops (`NP-GW-004` / `005` / `006`). Must not double-journal — that no-op is why paper 07’s write sits **behind** the unique insert.
8. **Public door:** provider webhook URL on Pay **`/v1`** (`NP-API-002`, [011/08](../011-new-lazuar-pay/08-bezos-door.md)). Not Hub `/api/v1/webhooks/payments/{gateway}/{tenantId}`. Not `/one/*`. Not a second app reading Pay tables.
9. **VIEWER cannot paste / rotate keys** (`NP-GW-009`, `NP-ONE-021`). One has **no** membership role `viewer` ([012/07](../012-one-to-pay/07-authz-roles.md) §10). Enforce with One `authz/check` on a relation that exists. See §4.6.
10. **Never in Vite.** Merchant `:5178` holds `VITE_PAY_API_URL`. Checkout `:5179` holds the same class of public origin. Stripe `sk_live_`, CHIP Bearer, Billplz X-Signature, AES master key — Pay process only, encrypted at rest, per `org_id`.
11. **Steal adapters as HTTP judgment.** Do not copy the module, MediatR, `IEventBus`, outbox/inbox jobs, `PaymentsDbContext` schema name, or `BuildingBlocks`. IsolationTests will fail the copy. That is the point.
12. **Do not implement from this file.** Sketch in §5–§6 is so the next checklist has a place to start.

---

## 1. Method / SHAs

### 1.1 What this paper is answering

How new Pay on **8081** takes money in production **without** growing `apps/lazuar-api/Modules/Payments`, **without** Hub session tables, **without** five adapters, and **without** lying about auto-debit.

The dogfood sentence this slice exists to unlock ([011/01](../011-new-lazuar-pay/01-product.md)):

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

This paper’s jobs inside that sentence: **paste keys**, **hosted pay (PSP URL)**, **webhook verify + retry no-op**. Receipt and journal are paper 07, in the **same handler**. Buyer page chrome is paper 05. Merchant paste UI is paper 04, calling this paper’s `/v1` key routes.

### 1.2 Method

1. Re-read 011 product law and the living checklist (`NP-GW`, `NP-API-002`/`006`, refuse `NP-XX-011`/`012`).
2. Re-read 012 Plane B notes and P50 (fixture door already ticked; buyer pay and rails still out).
3. Walk live `Modules/Payments` on Pay `6f866ff0`: types, adapters, webhook HTTP, encryption, ops copy. Sample enough to **name** classes and wrap comments. Do not dump the module.
4. Walk new host checkout fixture, pay-spec, IsolationTests, merchant Vite env.
5. Diff 008/009 claims against live files so this paper does not re-litigate closed Hub bugs as if they were the new design.
6. Leave CHIP vs Billplz **unpicked** when the tree is ambiguous (§10). Present evidence.

### 1.3 SHA facts that bind the sketch

| Fact | Evidence on `6f866ff0` |
|------|------------------------|
| Focused host listens **8081** | `apps/lazuar-pay` launchSettings, README, pay-spec `@server("http://localhost:8081")` |
| Checkout is a fixture, `status: "open"` | `CheckoutEndpoints.cs` hard-codes `Status = "open"`; `CheckoutStore` comment: “Not a ledger. Replace when money is real.” |
| Create requires Bearer + One `authz/check` `member` | `MemberGate.RequireMemberAsync` → `OneClient.CheckMemberAsync` |
| Amount `<= 0` is 400 | `CheckoutTests.Create_rejects_non_positive_amount` |
| Idempotency on create is `(org_id, Idempotency-Key)` in memory | `CheckoutStore._idempotency`; `NP-CHK-003` done as fixture |
| No webhook in pay-spec | `packages/pay-spec/main.tsp` Health, Session, Orgs, Checkouts only |
| Isolation bans | Host **and** test csproj + `*.cs` under `src/` must not contain `MediatR` / `BuildingBlocks` / `Modules.` / `lazuar-api` |
| Host has **no** NuGet | `Lazuar.Pay.csproj` — Stripe.net / Npgsql are not there yet |
| Merchant Vite secret surface | `VITE_PAY_API_URL=http://localhost:8081` in `.env.example` only |
| One HEAD | `0f79fe4` — Pay still Consumer-0; Pay does not hold Zitadel PAT (`NP-XX-017`) |

### 1.4 Tracker rows this paper may later unlock (leave `todo` now)

| ID | Feature | Wave | Dogfood | Status now |
|----|---------|------|---------|------------|
| NP-GW-001 | Encrypted BYOK keys per workspace | S1 | Y | todo — notes: Stripe **or** CHIP/Billplz |
| NP-GW-002 | Stripe card checkout | S1 | Y | todo — off-session only if a real PM exists |
| NP-GW-003 | One Malaysian rail (CHIP **or** Billplz) | S1 | Y | todo — not five adapters |
| NP-GW-004 | Webhook: verify signature | S1 | Y | todo |
| NP-GW-005 | Empty webhook body → 400 | S1 | — | todo |
| NP-GW-006 | Idempotent `(tenant, provider, event_id)`; retry no-ops | S1 | Y | todo — must not double-journal |
| NP-GW-007 | Honest matrix | S1 | — | todo |
| NP-GW-008 | Never treat setup / setup-intent as paid | S1 | — | todo — fail lock in 03 |
| NP-GW-009 | Merchant ops: paste / rotate keys; VIEWER cannot | S1 | Y | todo |
| NP-API-002 | Provider webhook URL on `/v1` | S1 | Y | todo |
| NP-API-006 | Idempotency on money POSTs | S1 | — | todo — aligns with NP-CHK-003 / NP-GW-006 |
| NP-FUL-001 | Same handler as webhook | S1 | Y | **paper 07** — named here only |
| NP-CHK-004 | States open → paid / expired | S1 | Y | fixture is open-only today |
| NP-XX-011 | Homemade FPX e-mandate | refuse | — | refuse |
| NP-XX-012 | Stripe Billing `subscription.updated` as SoT | refuse | — | refuse |

P50 (`plans/012-one-to-pay/checklists/p50-money.md`): door `POST`/`GET /v1/checkouts` is ticked as fixture; “Buyer pays without a One account” is **not**. Hosted page, rails, journal, `RCPT-` still out. This paper is the rails half of that remainder.

### 1.5 What “steal judgment” means in this folder

Copy the **decision**, not the type:

| Steal | Do not copy |
|-------|-------------|
| Stripe Checkout `mode=payment`, `payment_method_types=['card']`, `EventUtility.ConstructEvent` | `StripeGatewayAdapter` as a class in `Modules.Payments`, MediatR query handlers, Stripe Billing Portal as v1 |
| CHIP `POST gate.chip-in.asia/api/v1/purchases/`, RSA `X-Signature`, Brand ID as merchant id, `force_recurring` only when vaulting | `ChipCollectGatewayAdapter` + `ChipWebhookRegistrar` localhost→`lazuar-local-dev.com` rewrite as production policy |
| Billplz v3 bills, HMAC `x_signature` dual-compute, Collection ID, **no** off-session, **no** refund API, public HTTPS callback | `BillplzPublicBase` Hub-hostname folklore (already deleted in code comments); `PublicDnsFallback` unless Billplz is the dogfood rail and DNS is actually broken |
| `PaymentGatewayCapabilities.SupportsOffSession` = Stripe or CHIP; `SupportsEmandate` = false | Unread flags `SupportsDuitNowQr` / `SupportsHostedWallet` as product; five-name factory |
| Encrypted at rest, GET returns last-4 hint, blank PUT means keep | `AesSecretVault` `Jwt:Secret` fallback; `DecryptOrPlaintext` swallowing bad ciphertext as the key |
| Empty body 400; unique `(org, provider, event_id)`; Stripe dual-event business key | `IMediator.Send(ProcessGatewayWebhookCommand)`; outbox requeue on Dead; `HandleExistingLogAsync` republish |
| Ops amber copy: Billplz cannot vault | Five-option dropdown; Hub `ADMIN`/`VIEWER` strings; Razorpay “e-mandate” leftover (already softened in live ops to “cards; reminder-only”) |

---

## 2. Old Payments module: what is live vs parked vs lie (name types)

Path: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/`.

The module README on this SHA is **more honest than 008 remembered**, and **still a Hub cashier**, not new Pay:

> Live adapters are **Stripe**, **Billplz**, **CHIP**, **Razorpay**, and **Xendit**.  
> **Not an Accounting Ledger.** **Not a Fulfillment Engine.**  
> Machine (`/integrations/payments/checkouts`) sessions are stored as `IntegrationCheckoutSessions`.

That last sentence is the session table new Pay must **not** grow as a second product. The focused host already has `/v1/checkouts`. One Pay checkout row is enough.

### 2.1 Compiled rail set (live types)

`DependencyInjection.AddPaymentsModule` registers five `IPaymentGatewayAdapter` implementations and `PaymentGatewayFactory`:

| Class | `GatewayType` | File |
|-------|---------------|------|
| `StripeGatewayAdapter` | `STRIPE` | `Infrastructure/Gateways/StripeGatewayAdapter.cs` |
| `ChipCollectGatewayAdapter` | `CHIP` | `Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` |
| `BillplzGatewayAdapter` | `BILLPLZ` | `Infrastructure/Gateways/BillplzGatewayAdapter.cs` |
| `RazorpayGatewayAdapter` | `RAZORPAY` | `Infrastructure/Gateways/RazorpayGatewayAdapter.cs` |
| `XenditGatewayAdapter` | `XENDIT` | `Infrastructure/Gateways/XenditGatewayAdapter.cs` |

Inbound allow-list is the same five names (`Endpoints.cs` `AllowedGatewayTypes`). Factory `GetAdapter` uppercases and throws `InvalidOperationException` if nothing matches. There is still no Fiuu, Midtrans, Cashfree, SenangPay, PayPal, or Toyyib class.

Port `IPaymentGatewayAdapter`:

| Method | Meaning |
|--------|---------|
| `GenerateCheckoutAsync` | Hosted hop-2. Returns URL + provider session id. `setupFutureUsage` is a bool on every rail. |
| `ParseWebhookAsync` | Verify + map to `GatewayWebhookParsedResult`. |
| `IssueRefundAsync` | `bool` only — no refund id. |
| `GenerateCustomerPortalAsync` | Stripe Billing Portal or throw. |
| `ChargeOffSessionAsync` | Merchant-initiated charge against a stored token. |

`GatewayWebhookParsedResult` fields that matter for this paper: `Verified`, `EventType`, `EventId`, `AmountPaid`, `Currency`, `GatewayTransactionId`, `Metadata`, `GatewayFee`, `GatewayCustomerId`, `GatewayTokenId`, `UnusableAfterVerify`. There is **no** distinct `SETUP_COMPLETED` type. Setup is stuffed into `PAYMENT_COMPLETED`. That is a Hub lie new Pay must not copy (`NP-GW-008`).

### 2.2 Capability matrix type (live, honest on the two axes that have readers)

`Modules.Payments.Contracts.PaymentGatewayCapabilities`:

```csharp
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
public static bool SupportsOffSession(string? gatewayName)
    => Normalize(gatewayName) is "STRIPE" or "CHIP";

public static bool IsReminderOnlyGateway(string? gatewayName)
    => !SupportsOffSession(gatewayName);

public static bool SupportsEmandate(string? gatewayName)
{
    _ = gatewayName;
    return false;
}
```

`SupportsApiRefund`: Stripe, CHIP, Razorpay, Xendit. Billplz false.  
`RequiresMarkRefunded`: Billplz + offline names.  
`SupportsDuitNowQr` / `SupportsHostedWallet`: **tests + the static class**. Zero generate-path readers under `Modules/Payments/` (009 B04-P24, still true on this SHA). Do not promote unread flags into new Pay product chrome.

Runtime reader **inside Payments**: `ExecuteOffSessionChargeIntegrationEventHandler` short-circuits on `!SupportsOffSession` and publishes `GatewayPaymentFailed` with `off_session_not_supported`. Commerce / Billing / dunning **outside** this module also read the flags (008 §2.1). New Pay has no Commerce module; the matrix must live next to the charge function, not in a Contracts assembly for a sibling folder.

### 2.3 Stripe — live HTTP judgment

- **Generate:** Stripe.net `SessionService.CreateAsync`. Non-zero amount → `Mode = "payment"`, `PaymentMethodTypes = ["card"]` (`ApplyCardWalletPaymentMethodTypes`). Comment: wallets ride on `card`; listing `apple_pay` / `google_pay` is invalid. This **replaces** Dashboard dynamic PMs — Stripe FPX / GrabPay / Link will not appear on a Lazuar-created session. That wrap is honest if the merchant UI says “cards (+ Apple/Google Pay when Stripe shows them).”
- **`$0` + `setupFutureUsage`:** `Mode = "setup"` (SetupIntent). A `$0` PaymentIntent is invalid. Steal the split. **Do not** count the later `checkout.session.completed` as paid.
- **Paid vault:** `ApplySetupFutureUsage` sets `PaymentIntentData.SetupFutureUsage = "off_session"` and `CustomerCreation = "always"`. Without a Customer, Stripe often returns no reusable PM. Steal the pairing.
- **Parse:** require `Stripe-Signature`; `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)`. Maps `checkout.session.completed` / `payment_intent.succeeded` → `PAYMENT_COMPLETED` with `EventId = stripeEvent.Id` (`evt_…`). `payment_intent.payment_failed` → `PAYMENT_FAILED`. Disputes: `charge.dispute.created` / `closed` / `updated`. Refunds: `TryMapRefundCompleted` (live; 008 said this was missing). Setup: `TryMapSetupIntentSucceeded` returns **`PAYMENT_COMPLETED` amount 0** if a PM exists; setup session without PM → `RefuseSetupSessionWithoutToken` `Verified=false` so Stripe retries (B04-P20 closed at adapter).
- **Off-session:** PaymentIntent `OffSession=true`, `Confirm=true`, idempotency `lazuar-offsession:{chargeAttemptId}`. Success is **`succeeded` only** (`IsOffSessionSucceeded`). 009 claimed `processing` counted; live helper does not. Steal “succeeded only.” Hub still does **not** publish completed from the off-session HTTP success — it waits for `payment_intent.succeeded`. New Pay same-handler world: either wait for the webhook to book cash (honest) or book a **pending** that paper 07 must not call `RCPT-` yet. Do not treat adapter `true` as paid.
- **Portal:** Stripe Billing Portal by email. Only adapter that implements it. **Not v1 dogfood.** Buyer magic-link portal is `NP-BUY-004` (V1).
- **Not mapped:** `customer.subscription.updated` / `invoice.paid` from Stripe Billing. There is no Stripe Subscription object in generate (`Mode` is never `"subscription"`). That is the `NP-XX-012` lock already encoded in the adapter, accidentally correctly.

### 2.4 CHIP Collect — live HTTP judgment

- **Generate:** `POST https://gate.chip-in.asia/api/v1/purchases/` Bearer key. Requires `merchantId` as **Brand ID**. No payment-method allow-list — FPX, DuitNow QR, wallets, cards, BNPL appear if the CHIP **brand** is configured. Lazuar does not request or suppress them. That is wrap-rails at its best: the hosted page is CHIP’s.
- **Vault flags:** `setupFutureUsage` → `force_recurring=true`; amount 0 → `skip_capture=true`. CHIP docs: `skip_capture=true` success callback fires on **capture**, not on pay. Live parse **now** maps `purchase.preauthorized` **when a recurring token is present** to `PAYMENT_COMPLETED`. 009 B04-P01 (never vaults) is **partially closed at parse**. It is **still** `NP-GW-008` if new Pay books that as paid money. Steal: preauthorized + token = **vaulted, not captured**. `purchase.paid` = money.
- **Parse:** `X-Signature` base64, RSA PKCS#1 v1.5 SHA256 of **raw body**, PEM in `WebhookSecret`. `EventId = $"{mapped}:{purchaseId}"` (`PAYMENT_COMPLETED:purch_…`). Currency fail-closed (`GatewayCommon.TryNormalizeCurrency`, no invented MYR). Fees from `payment.fee_amount` when present; else stamp `gateway_fee_status=unknown` (`NP-MON-002` spirit: unknown ≠ 0).
- **Refund webhook:** live maps `payment.refunded` → `REFUND_COMPLETED`. 008/009 said registered-and-dropped; **live maps it**. Paper 07 owns reverse-once.
- **Off-session:** create purchase + `POST purchases/{id}/charge/` with `{ recurring_token }`. Live sends `reference` = idempotency key and looks up an existing purchase (`TryFindPurchaseIdByReferenceAsync`). 009 B04-P04 (no key) is **mitigated**, not Stripe-class. CHIP has no processor idempotency header like Stripe’s `Idempotency-Key`. Steal the reference lookup; do not claim it is Stripe.
- **Registrar:** `ChipWebhookRegistrar.EnsureRegisteredAsync` lists existing callbacks (B04-P19 “duplicates” mitigated), prefers `Webhook.public_key`, falls back to company `GET /public_key/`. Events registered: `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`. Hub `UpdatePaymentConfigCommandHandler` still rewrites localhost → `lazuar-local-dev.com` when saving a CHIP key. Billplz `BillplzPublicBase` **refuses** that host. Do not copy the rewrite as production policy.
- **Host:** always `https://gate.chip-in.asia/api/v1/`. Tenant `environment=test` does **not** select a CHIP test host. CHIP test mode is a **dashboard toggle on the same API**. Steal that honesty in merchant copy: “Test vs live is the CHIP brand switch, not a Pay hostname.”
- **Portal:** throws `InvalidOperationException`. Keep throwing. There is no CHIP Billing Portal.

### 2.5 Billplz — live HTTP judgment

- **Generate:** `POST {host}api/v3/bills` Basic `apiKey:`. Requires Collection ID. `setupFutureUsage` is an **unused parameter** (not even `_ = setupFutureUsage` in the signature body — the parameter exists on the port and is ignored). Honest: no vault.
- **Hosts:** `https://www.billplz.com/api/v3/` vs `https://www.billplz-sandbox.com/api/v3/` via `BillplzPublicBase.IsProductionApi` (`App:BillplzEnvironment` then tenant `environment`). Comment: **do not infer from Hub hostname**. Steal that. `pay-local.lazuar.com` must never go live.
- **Callback URL:** `{publicHttps}/webhooks/payments/billplz/{tenantId}?type=&reference_1=` plus optional `checkout_id`. Billplz strips body metadata. Query string + server-side session merge (`ProcessGatewayWebhookCommandHandler.Metadata.cs`) are the recovery path. New Pay should put `checkout_id` (Pay session id) on the query **and** persist `provider_session_id` = bill id so merge is a fallback, not the SoT.
- **Public base:** `TryResolveCallbackBase` fails closed on loopback, `lazuar-local-dev.com`, and non-HTTPS unless `App:AllowInsecureBillplzCallback`. Error token `CALLBACK_BASE_NOT_PUBLIC`. Steal for **any** rail whose PSP cannot POST to localhost — Billplz is the strictest. ngrok/Cloudflare tunnel is local-only (§8).
- **Parse:** form body, not JSON. HMAC-SHA256 over sorted `key+value` joined by `|`. Dual-compute: with extra fields (`paid_at`, `transaction_id`, `transaction_status`) then without. Fixed-time hex compare. Paid if `paid=true` or `state=paid`, else **`PAYMENT_FAILED`**. `EventId = $"{COMPLETED|FAILED}:{billId}"`. Currency **hardcoded `"MYR"`**. Fee formula exists but the handler always passes `0,0,0` — Billplz `GatewayFee` is always 0 in production (`NP-MON-002`: do not invent a fee).
- **Off-session:** logs a warning, returns `false`. Does not throw.
- **Refund:** `IssueRefundAsync` → `false`. Comment: “Billplz has no bill-refund API. A Payment Order is a new disbursement, not a reversal.” `RequiresMarkRefunded("BILLPLZ")` is true. Steal the SOP: refund in Billplz dashboard, then mark (paper 07).
- **HTTP client:** `PublicDnsFallback` (1.1.1.1 / 8.8.8.8 connect hook). Only Billplz uses it. Park unless dogfood proves Hub DNS is still a problem.

### 2.6 Razorpay / Xendit — live, **parked for new Pay**

| Type | Live Hub job | Why it is a lie if new Pay ships it on day one |
|------|----------------|-----------------------------------------------|
| `RazorpayGatewayAdapter` | Payment links. `SetupFutureUsage` **discarded**; comment: “Reminder-only: we do not claim e-mandate.” HMAC `X-Razorpay-Signature`. `SupportsOffSession` false. `SupportsEmandate` false. | `NP-LAT-002`. Old ops label used to say “MY e-mandate + cards”; live ops says “cards; reminder-only until token soak”. Do not re-open Curlec e-mandate. |
| `XenditGatewayAdapter` | Hosted invoices. Class comment: “Reminder-only until a payment-token soak proves off-session. We do not rebuild wallets.” `x-callback-token` compare, not body HMAC. | `NP-LAT-002`. 008: ops dropdown without fields. Live ops **now has** Xendit fields + amber “Hosted invoice only… No silent auto-charge, no FPX e-mandate.” Still not dogfood. |

Parked ≠ delete from Hub. New Pay simply does not register them.

### 2.7 Webhook HTTP (Hub) — live, do not copy the shape

`POST /webhooks/payments/{gatewayType}/{tenantId}` (`Modules/Payments/Infrastructure/Endpoints.cs`):

- Unknown gateway → **400**.
- Empty body → **400** `{ error: "Empty request body." }` (B04-P18 closed on live SHA; 008/009 said 500).
- Then `IMediator.Send(ProcessGatewayWebhookCommand)` — **MediatR**. IsolationTests on the new host ban the string `MediatR`.
- Success → **200** `{ received: true }`. Comment: “Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session.” **That comment is the cathedral.** New Pay’s 200 means “verified, idempotent row in, fulfillment function called (or no-op duplicate).”
- `PaymentWebhookUnusablePayloadException` → **400** (poison after verify; stop retries).
- Verify fail → handler throws `InvalidOperationException` → endpoint’s catch **re-throws** non-`InvalidOperationException` only… actually `InvalidOperationException` is **not** in the 500 swallow: `catch (Exception ex) when (ex is not InvalidOperationException && ex is not BusinessRuleValidationException) { log; throw; }`. So signature failure **bubbles as 500**. Stripe retries forever. **Do not copy.** Signature fail is **400**.
- Missing tenant webhook secret → `InvalidOperationException` → **500**. **Do not copy.** Config missing is **400**.

Idempotency live:

- Unique index `(OrganizationId, Provider, EventId)` (`PaymentConfigurations.cs`; migration `20260822120000_AddPaymentWebhookOrganizationId`). 009 B04-P06 “not tenant-scoped” is **closed on live SHA**.
- Unique `(OrganizationId, Provider, BusinessKey)` filtered `BusinessKey IS NOT NULL`. Business key `EVENTTYPE:GatewayTransactionId` collapses Stripe `checkout.session.completed` + `payment_intent.succeeded` for the same PI. Refunds skip business key so partial refunds do not collapse.
- Concurrent insert 23505 swallowed as success (`TrySaveChangesAsync`). Steal the unique-violation-is-duplicate idea. Do not steal the outbox requeue machine (`HandleExistingLogAsync` / `TryRequeueDeadOutboxAsync`). New Pay has no Payments outbox.
- Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` on the same transaction id is **ignored** (`GetByBusinessKeyAsync("PAYMENT_COMPLETED:"+id)`). Steal for Stripe/CHIP. Billplz namespaced EventIds mean unpaid-after-paid is a **new** EventId; Hub will still publish fail (009 B04-P08 residual). New Pay: if session is already `paid`, a later fail is a log line, not a journal reverse (paper 07: do not double-reverse).

### 2.8 Keys (Hub) — live types

| Type | Job |
|------|-----|
| `TenantPaymentConfiguration` | Per `(OrganizationId, GatewayType)` unique. AES-encrypted `ApiKey` + `WebhookSecret` (base64 IV+ciphertext). `MerchantId` plaintext (Brand / Collection). `IsActive`. `Environment` `test`\|`live`. |
| `UpdatePaymentConfigCommandHandler` | Encrypt via `ISecretVault`. Stripe secret lives in `ApiKey` column (`SecretKey` field). CHIP new key → `ChipWebhookRegistrar`. First-time create requires an API key. |
| `GetPaymentConfigQueryHandler` | **Never returns secrets.** `Api_key = null`, `Webhook_secret = null`, last-4 via `HintLast4`. |
| `AesSecretVault` | AES-256-CBC, `Kms:MasterKey` **falling back to `Jwt:Secret`**. PadRight 32 with `'0'`. |
| `SecretVaultExtensions.DecryptOrPlaintext` | Decrypt fail → treat as legacy plaintext. Migration crutch. |
| `SecretVaultExtensions.IsKeepExistingSecret` | blank or contains `••••` → keep. |

HTTP paste routes (Hub):

- Merchant: `GET`/`PUT /admin/commerce/payment-config` (`Commerce.Infrastructure.PaymentConfigEndpoints`) **`RequireAuthorization("OrgAdmin")`** — Hub policy, MediatR.
- Platform twin: `/api/v1/platform/payment-config` (`PlatformEndpoints`) — Hub staff, not a Pay merchant destination (`NP-XX-018`).

Ops UI (`PaymentSettingsPage.tsx`):

- `canSaveVault = role === "ADMIN" || role === "SUPER_ADMIN"`. Save button omitted otherwise. Hub `VIEWER` cannot paste. **Steal the product rule, not the role strings.** One roles are `owner` \| `admin` \| `member` ([012/07](../012-one-to-pay/07-authz-roles.md)).
- Amber Billplz copy (live, steal wording):

  > **Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it. There is no silent auto-charge (subscription renewals, dunning AUTO_CHARGE). Use Stripe or CHIP Collect when you need recurring auto-debit.

- Amber Xendit copy (live, steal if/when `NP-LAT-002`): hosted invoice only; no silent auto-charge; no FPX e-mandate.
- Stripe copy: Apple/Google Pay on Stripe-hosted Checkout when the account can take cards; not on Billplz; Lazuar does not host wallet buttons.
- GET never fills password fields (“Never populate password fields with stored secrets”).
- Default dropdown value in state: `BILLPLZ`. Historical Aura System B default ([007/05](../007-feats/05-malaysia-gateways.md)). Not a reason to default new Pay to Billplz.

### 2.9 Session tables (Hub) — live, **do not grow on 8081**

| Type | Job | New Pay |
|------|-----|---------|
| `IntegrationCheckoutSession` | M2M `/integrations/payments/checkouts`, 24h TTL, statuses `open`/`completed`/`failed`/`expired`, `SetupFutureUsage`, `ProviderSessionId` | **Do not copy.** Fixture `/v1/checkouts` is the session. Paper 05/07 grow it. |
| Commerce hop-2 `subscription_id` in PSP metadata | Hub cashier is stateless for Commerce; metadata is the pointer | New Pay session id **is** the pointer. Stamp `checkout_id` / `org_id` in metadata. |
| `CheckoutSessionCashier` | Decrypt, `KEY_MODE_MISMATCH`, last-resort `"BILLPLZ"` when `requireActiveGateway` is false | **Do not copy the last resort.** Missing keys → 400/409, not a surprise Billplz bill. Steal test/live key prefix guard for Stripe-shaped keys. |
| `CheckoutAmountRules` | MYR min **RM 2.00**, else 0.50; 2 decimal places on MYR | Steal as HTTP validation on `POST /v1/checkouts` when money is real (fixture today only checks `> 0`). |
| `GenerateSystemCheckoutSessionQueryHandler` | Hub SaaS fee / utility credits / `PlatformCheckoutTypes.SystemOrganizationId` | **Refuse for v1.** Pay is not Hub billing itself. |

### 2.10 Live vs parked vs lie (one table)

| Thing | Verdict on Hub `6f866ff0` | New Pay |
|-------|---------------------------|---------|
| `StripeGatewayAdapter` HTTP | **Live.** Cards, webhook verify, off-session idempotency, setup mode, disputes, refund map | Steal HTTP. Drop Billing Portal from S1. Never `subscription.updated`. |
| `ChipCollectGatewayAdapter` HTTP | **Live.** Purchases, RSA, refund API, off-session reference lookup, preauthorized+token mapped completed | Steal HTTP. Remap preauthorized to **vault, not paid**. |
| `BillplzGatewayAdapter` HTTP | **Live.** Bills, HMAC, public callback, no vault, no refund API | Steal HTTP **if** this is the dogfood MY rail. Reminder-only copy is mandatory. |
| `RazorpayGatewayAdapter` | **Live code, parked product** | `NP-LAT-002` |
| `XenditGatewayAdapter` | **Live code, parked product** | `NP-LAT-002` |
| `PaymentGatewayCapabilities` off-session / e-mandate | **Live and honest** | Steal as a 10-line helper next to charge, not a Contracts project |
| DuitNow / wallet flags | **Lie as product** (unread) | Do not ship hop-1 method tiles. Hosted page is the PSP’s. |
| Five-adapter factory | **Live, product lie vs 011** | One or two adapters. Factory of five is how day-one became five. |
| `ProcessGatewayWebhookCommandHandler` + outbox | **Live cashier split** | Lie for new Pay. Same handler = verify + fulfill. |
| Empty body 400 | **Live** (fixed after 008) | Keep |
| Signature fail 500 | **Live lie** | 400 |
| Unique `(org, provider, event_id)` | **Live** (fixed after 009) | Keep |
| Setup as `PAYMENT_COMPLETED` | **Live lie** | `NP-GW-008` |
| `TenantPaymentConfiguration` AES | **Live** | Steal “encrypted at rest, hint on GET”. New master key. No `Jwt:Secret` fallback in prod. |
| CHIP localhost rewrite | **Live foot-gun** | ngrok/tunnel for local; public HTTPS for staging. No fiction DNS. |
| `IntegrationCheckoutSession` | **Live M2M** | Do not copy. `/v1/checkouts` is the row. |
| BILLPLZ last-resort resolve | **Live lie** | Missing config fails closed |
| Ops wrap banners | **Live honest** (Billplz, Xendit) | Steal wording onto `:5178` |
| Hub `OrgAdmin` / `ADMIN` vault gate | **Live** | Map to One `admin` check; do not invent VIEWER |
| Payments README “not fulfillment” | **Live architecture lie for 011** | The new host’s webhook **is** fulfillment (paper 07) |
| MediatR / inbox / outbox / per-schema workers | **Live cathedral** | IsolationTests exist so this cannot sneak in |

---

## 3. What new Pay must implement for dogfood

### 3.1 011’s OR is real

Step 8 ([011/03](../011-new-lazuar-pay/03-first-slice.md), [011/12](../011-new-lazuar-pay/12-first-slice-tracker.md)):

> Store BYOK Stripe **or** CHIP/Billplz keys for that tenant.

`NP-GW-001` notes: “Stripe **or** CHIP/Billplz for dogfood.”

The product dogfood sentence ([011/01](../011-new-lazuar-pay/01-product.md)) names **CHIP or Stripe**, not Billplz.

The checklist still marks **both** `NP-GW-002` (Stripe) and `NP-GW-003` (one MY rail) `Dogfood = Y`.

Those three sentences do not pick a single pair. They constrain the **maximum**: not five adapters; at least one pasted rail that can take a real buyer payment; wrap-rails honesty on that rail. §10 holds the MY-rail pick.

### 3.2 Minimum dogfood surface (this paper’s slice)

For **one** pasted rail `R` ∈ { Stripe, CHIP, Billplz }:

1. **Key vault** per `org_id` + provider, encrypted at rest. Paste/rotate on `/v1`. GET masked. `authz/check` so a read-only staff cannot PUT (§4.6).
2. **Generate hosted checkout** from `POST /v1/checkouts` when the org has an active config for `R`. Persist `provider`, `provider_session_id`, `checkout_url`. Status stays `open` until Plane B says otherwise.
3. **Public webhook** `POST /v1/webhooks/{provider}/{orgId}`. Verify `R`’s signature. Empty body 400. Idempotent `(org_id, provider, event_id)`.
4. **Same handler** calls paper 07’s fulfill function on first `paid` for that session. Duplicate delivery returns 200 and does not call it.
5. **Wrap-rails:** if `R` is Billplz, Pay must not call off-session anywhere, including a future billing job. Merchant UI must say pay-link renewals. If `R` is Stripe or CHIP, off-session is allowed **only** after a real PM/token was stored from a **paid or setup-complete** event — never from `mode=setup` counted as paid.

That is enough for “buyer pays on the hosted page without a One account” **if** paper 05 hosts the redirect to `checkout_url` and paper 07 writes `RCPT-`.

### 3.3 One card rail + one MY rail, or one of them

| Interpretation | What you implement | What 011 allows | Risk |
|----------------|--------------------|-----------------|------|
| **A. Stripe only** | `NP-GW-002` | Step 8 OR; 01 sentence “CHIP or Stripe” | No Malaysian rail. `NP-GW-003` stays todo. Fine for a global-card dogfood; fails “one MY rail you will actually dogfood” as a **checklist** row. |
| **B. CHIP only** | One adapter that is **both** MY and card-capable (hosted page may show FPX **and** cards) | Step 8 OR; 01 sentence names CHIP | Satisfies `NP-GW-003`. `NP-GW-002` as “Stripe card checkout” is **not** CHIP cards. Do not tick `NP-GW-002` because CHIP showed a Visa form. |
| **C. Billplz only** | Reminder-only MY rail | Step 8 OR (`CHIP/Billplz`) | 01 sentence does **not** name Billplz. Auto-charge path untested. Wrap copy is mandatory or you have silently debit-lied. |
| **D. Stripe + one MY** | Two adapters | Both Dogfood Y rows | Closest to the checklist. Heavier than step 8. `NP-SOON-008`: second gateway only after the first two are boring — “two” here **is** the S1 pair, not a third. |

**This paper does not pick A–D.** It requires: whatever you paste, the adapter is real HTTP (not a stub that returns `status: paid`), wrap-rails copy matches `SupportsOffSession`, and you do not tick Stripe-done because Billplz redirected.

Practical overlap: **CHIP is the only single rail that is Malaysian and can vault.** Stripe is the only rail with processor idempotency keys and a well-soaked SetupIntent story. Billplz is the SME mental-model default ([007/05](../007-feats/05-malaysia-gateways.md)) and the **reminder-only** end of `NP-GW-007`. See §10.

### 3.4 What “one Malaysian rail” is not

- Not Fiuu, Toyyib, senangPay, iPay88, Revenue Monster, HitPay, Midtrans, Cashfree.
- Not “Razorpay / Curlec because FPX e-mandate.” `SupportsEmandate` is false. Hub adapter is a **payment link**.
- Not Xendit wallets as a Pay checkbox. Hosted invoice wrap is later, labelled reminder-only.
- Not a Pay-native DuitNow QR. QR is pixels on CHIP/Billplz hosted pages (`SupportsDuitNowQr` has no generate reader).
- Not CHIP Send / Expense / Advance, not Billplz Catalog Store, not Stripe Connect.

### 3.5 Off-session is not the first charge

First dogfood is **hosted hop-2**, amount `> 0`, buyer present. Fixture already refuses `amount <= 0`. Keep that for S1 money: do not start with `$0` setup mode.

`NP-GW-002` notes: “Off-session only if a real PM/token exists.” `NP-FUL-004` (renew / billing job) is **V1**, wrap-rails. Do not build `ChargeOffSessionAsync` in order to prove the first `RCPT-`.

When off-session **does** land:

- Stripe: real `customer` + `payment_method`, idempotency key, wait for `payment_intent.succeeded` (or paper 07’s pending rule). `processing` is not paid.
- CHIP: real `recurring_token`, never FPX token. CHIP FAQ: CHIP does not run the subscription clock — Pay does.
- Billplz: **do not call**. Capability false. Returning `false` and then inventing `PAST_DUE` without a failed charge is `NP-FUL-005` (paper 07). This paper: the function is a no-op or is absent.

### 3.6 NuGet / HTTP on the focused host

Today: zero packages, `HttpClient` only as `OneClient`. Money will need:

- Stripe.net (or raw HTTP — Stripe’s official library is the verify story; steal `EventUtility.ConstructEvent`, do not reimplement Stripe signatures).
- `HttpClient` for CHIP / Billplz JSON (already in the shared framework).
- A database (paper 03 host seams) — keys and webhook log and checkout session cannot stay `ConcurrentDictionary` past the first retry.

Do **not** add a project reference to `Modules.Payments.*`. Do **not** add `BuildingBlocks`. Do **not** add MediatR. If Stripe.net’s types tempt a `IPaymentGatewayAdapter` port with five methods “for later,” stop at **two functions**: `CreateHostedCheckout` and `ParseWebhook`. Refund and off-session wait until paper 07 / V1 need them.

---

## 4. Key storage (encrypted at rest in Pay DB, per `org_id`). Secret handling. Never in Vite.

### 4.1 Where the secret lives

| Secret | Store | Who sees plaintext |
|--------|-------|---------------------|
| Stripe `sk_test_` / `sk_live_` | Pay DB, per `org_id` + provider `stripe`, AES ciphertext | Pay process on create-checkout and off-session. Never GET. |
| Stripe `whsec_…` (PSP signing secret) | Same row, separate column | Webhook verify only |
| CHIP secret key (Bearer) | Pay DB, per `org_id` + `chip` | Generate + off-session + optional registrar |
| CHIP webhook PEM | Same row | Verify `X-Signature` |
| CHIP Brand ID | Same row, **not secret** (UUID) | Generate |
| Billplz secret key | Pay DB, per `org_id` + `billplz` | Generate (Basic auth) |
| Billplz X-Signature key (128 hex) | Same row | Verify HMAC |
| Billplz Collection ID | Not secret | Generate |
| Pay AES master key | Process env / secret manager (`Pay:Kms:MasterKey` or equivalent). **Not** `Jwt:Secret`. **Not** Vite. | Process start |
| One `whsec_…` (Plane A) | **Different** table/column (`source=one`). [012/09](../012-one-to-pay/09-webhooks-events.md) | Plane A verify only |
| Zitadel PAT / FGA admin / One webhook AES pepper | **Never in Pay** (`NP-ONE-020`, `NP-XX-017`) | — |

`org_id` **is** the One tenant id ([011/02](../011-new-lazuar-pay/02-one-integration.md), [012/06](../012-one-to-pay/06-tenant-org.md)). Unique `(org_id, provider)` like Hub `TenantPaymentConfiguration` `(OrganizationId, GatewayType)`. Soft-disable `is_active` without deleting ciphertext (steal `SetActive`). `environment` `test`\|`live` (steal `PaymentGatewayEnvironment`; **do not** stamp Hub metadata key `hub_payment_environment`).

### 4.2 Crypto shape to steal, and the Hub crutches to drop

Steal from `AesSecretVault` / `TenantPaymentConfiguration`:

- Ciphertext at rest; never return raw to clients (`GetPaymentConfigQueryHandler` comment).
- IV prepended, base64 stored — a boring envelope.
- Last-4 hint after decrypt (`HintLast4`) for “stored …abcd” chrome.
- Blank / mask PUT means keep (`IsKeepExistingSecret`).

Drop:

- **`Jwt:Secret` fallback.** Hub `AesSecretVault` uses `Kms:MasterKey` then `Jwt:Secret`, then pads to 32 with `'0'`. That couples payment keys to a leftover JWT secret. New Pay is not an IdP. If `Pay:Kms:MasterKey` is missing, **refuse to boot** (or refuse to save keys), do not silently encrypt under `"0000…"`.
- **`DecryptOrPlaintext`.** Swallowing decrypt failure as “legacy plaintext” is how a mis-keyed deploy starts sending AES blobs to Stripe as `sk_live_`. New rows are always ciphertext. Reject undecryptable keys at use time.
- **CBC as a religion.** The product requirement is encrypted at rest. AES-GCM (or the platform secret box paper 03 picks) is fine. Do not import `BuildingBlocks.Infrastructure.AesSecretVault` to “stay consistent.”

Audit: `NP-AUD-003` — audit row on gateway-key change, **same DB transaction** as the write. Paper 07’s audit table can be the one; this paper only requires the key PUT handler to call it. Do not log the new secret. Do not log the old secret. Log provider, actor (`user_id` from whoami), `org_id`, action `created`/`rotated`/`disabled`.

### 4.3 HTTP for keys on 8081 (sketch, Bezos door)

Merchant ops is a client of `/v1` (`NP-API-004`). Do not PUT Hub `/admin/commerce/payment-config`. Do not use MediatR.

Suggested (grow `packages/pay-spec` when implementing):

| Method | Path | Auth | Body | Returns |
|--------|------|------|------|---------|
| `GET` | `/v1/orgs/{orgId}/gateways` | Bearer + `authz/check` `member` | — | List: `provider`, `is_active`, `environment`, `has_api_key`, `api_key_hint`, `has_webhook_secret`, `webhook_secret_hint`, `merchant_id` (Brand/Collection). **No ciphertext, no plaintext.** |
| `PUT` | `/v1/orgs/{orgId}/gateways/{provider}` | Bearer + **`authz/check` `admin`** (§4.6) | `api_key?`, `webhook_secret?`, `merchant_id?`, `is_active?`, `environment?` | 200 masked row. First-time requires `api_key` (Stripe: `sk_…` in `api_key`). |

`provider` path: `stripe` \| `chip` \| `billplz` (allow-list of **dogfood rails only**). Unknown → 400. Do not accept `razorpay` / `xendit` until `NP-LAT-002`.

CHIP convenience (optional, not required for dogfood): on first key save, Pay may `GET /public_key/` and `POST /webhooks/` like `ChipWebhookRegistrar`, storing PEM as webhook secret so Ada does not paste a PEM. If you do this, **list-before-create** (live registrar already does) and register Pay’s **public** `/v1/webhooks/chip/{orgId}`, not `http://localhost:8081/...` rewritten to `lazuar-local-dev.com`.

Stripe: Ada pastes `whsec_…` from the Stripe Dashboard endpoint that points at Pay’s public URL. Pay does not create Stripe webhook endpoints via API in v1 (Hub didn’t either).

Billplz: Ada pastes 128-char X-Signature and Collection ID. Callback URL is **not** pasted into Pay — it is printed for Ada to put on the Billplz collection, **or** sent as `callback_url` on each bill (live generate already sets per-bill `callback_url`). Prefer per-bill callback with Pay’s public origin so local/staging/prod do not share one collection callback.

### 4.4 Never in Vite

Live merchant app:

```ts
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
```

`.env.example`: `VITE_PAY_API_URL=http://localhost:8081` only.

**Forbidden env names on `:5178` / `:5179`:** `VITE_STRIPE_*`, `VITE_CHIP_*`, `VITE_BILLPLZ_*`, `VITE_KMS_*`, anything that is a secret. Vite inlines `VITE_*` into the browser bundle. A `sk_live_` in merchant source is a production incident, not a DX shortcut.

Checkout `:5179` may receive a **publishable** Stripe key later if Pay ever hosts Elements. S1 is **hosted redirect** (PSP page). No publishable key required. Do not add `VITE_STRIPE_PUBLISHABLE_KEY` “for later.”

### 4.5 Test vs live

Steal `CheckoutSessionCashier.EnsureKeyModeMatchesGateway`:

- `sk_test_` cannot charge a `environment=live` config.
- `sk_live_` cannot charge `environment=test`.
- Non-Stripe-shaped keys (Billplz, CHIP) skip prefix inference; Ada’s `environment` dropdown is SoT.
- CHIP: dashboard test-mode is **not** a key prefix. Copy must say so.
- Billplz: sandbox host vs www host follows `environment`, **not** Pay’s hostname (`BillplzPublicBase` comment). Copy already in ops: “Hub hostname does not pick Billplz sandbox vs live.” Replace “Hub” with “Pay.”

Dogfood on **test/sandbox keys** until paper 08/10 say staging is real. First `RCPT-` may be sandbox money. That is still a real webhook, a real signature, a real idempotency tuple. Live keys are a production PSP requirement (§8), not a unit-test requirement.

### 4.6 VIEWER cannot paste — mapped onto One

Locked: `NP-GW-009`, `NP-ONE-021`, 011/12 step 12, 01 dogfood sentence.

Hub: `OrgAdmin` policy on PUT; ops `canSaveVault` for `ADMIN` \| `SUPER_ADMIN`; `VIEWER` does not see Save.

One ([012/07](../012-one-to-pay/07-authz-roles.md) §10, restated because money routes will get this wrong):

- Membership roles: `owner` \| `admin` \| `member`. **No `viewer`.**
- FGA `viewer` is a relation on **type `app`**, not “read-only merchant.”
- `authz/check` `member` is true for every valid membership. If key PUT uses `check(member)`, every invited member can rotate Stripe keys.
- You cannot invite a VIEWER on One. The dogfood sentence’s “VIEWER cannot charge” is **not expressible as a One role** today.

Honest mappings (pick in paper 08 / implementation checklist; this paper only binds **key paste**):

| Gate | `authz/check` relation | Who can paste keys | Who can see hints |
|------|------------------------|--------------------|-------------------|
| **A. admin-write** (recommended for keys) | PUT: `admin` (owner has admin). GET: `member` | owner, admin | owner, admin, member |
| **B. member-write** | PUT: `member` | everyone invited | everyone invited |
| **C. Pay-side VIEWER flag** | — | second membership plane | `NP-XX-014` refuse |

**Key paste uses A.** Rotating `sk_live_` is not a `member` job. Hub already used OrgAdmin, not OrgMember. 011 “invited MEMBER can see ops” (`NP-ONE-022`) is GET chrome, not PUT keys. 011 “VIEWER cannot change keys” is satisfied if the people who cannot PUT are… everyone who fails `check(admin)`. There is no VIEWER; **member cannot PUT**. Charge/refund gates are paper 07 + 08; do not invent a Pay `VIEWER` table here.

Dummy `/v1/orgs/{orgId}/ready` today checks `member` only (README: “Staff VIEWER is not a One tenant role… ready checks member, not cannot charge”). That dummy is **not** the key gate.

---

## 5. Webhook endpoint design on 8081: path, signature, empty body, idempotency key, 400 vs 200

### 5.1 Path (Bezos door, not Hub, not Plane A)

Hub: `POST /webhooks/payments/{gatewayType}/{tenantId}` under the modular API (`/api/v1` in front of that in production).

012/09 sketch for **One** receiver: `POST /v1/one/webhooks`. Explicitly: do **not** put Plane A under `/api/v1/webhooks/payments/{gateway}/…`.

This paper’s Plane B sketch:

```text
POST http://localhost:8081/v1/webhooks/{provider}/{orgId}
```

Examples:

- `POST /v1/webhooks/stripe/{orgId}`
- `POST /v1/webhooks/chip/{orgId}`
- `POST /v1/webhooks/billplz/{orgId}`

| Constraint | Why |
|------------|-----|
| Under `/v1` | `NP-API-002`, 011/08, pay-spec server 8081 |
| `{provider}` allow-list of dogfood rails only | Five-name Hub allow-list is how Xendit got a URL before a form |
| `{orgId}` is One tenant id | Same as checkout `org_id`. Decrypt **that** org’s webhook secret. |
| No Bearer | PSP cannot do One OIDC. HMAC/RSA/Stripe-Signature **is** the auth |
| Not `/v1/one/webhooks` | Plane A |
| Not `/webhooks/payments/...` | Hub. Isolation + honesty |
| Not `/api/v1/...` | Focused host has no `/api` prefix (`/v1/health`, `/v1/whoami`, `/v1/checkouts`) |

Grow `packages/pay-spec` with this route when implementing. Anonymous POST. Document that merchants **configure the PSP** to this public URL. Pay may print it on GET gateways.

Billplz will also send query string (`type`, `reference_1`, `checkout_id`). Enable buffering, read **raw body** first (form bytes), then copy query into a header map like Hub `Query-{key}` if the parser needs it. Do not JSON-bind the body before verify.

### 5.2 Raw body, then verify

Pipeline (one function, no MediatR):

1. Allow-list `provider`. Else **400**.
2. Read raw body bytes. If empty/whitespace → **400** (`NP-GW-005`). Health-check POSTs from load balancers must not 500 (Hub B04-P18).
3. Load gateway row for `(orgId, provider)`. Missing / no webhook secret → **400** `{ error: "gateway not configured" }` (not 500; not 200 — Ada should notice). Soft-disabled: **still verify and fulfill**. Hub comment: “Webhooks still process when gateway is soft-disabled (credentials retained).” Steal that: disable means no **new** checkouts, not “throw away paid money.”
4. Decrypt webhook secret. Decrypt fail → **500** once, then ops (this is our bug, not the PSP’s). Do not `DecryptOrPlaintext`.
5. Verify with **provider rules** (§5.3). Fail → **400**. Never 401 (no Bearer). Never 500 (retry storm).
6. Parse to an internal money event: `{ kind: paid | failed | ignored | vaulted, event_id, provider_txn_id, amount, currency, checkout_id?, customer_id?, token_id? }`. `kind=ignored` (unknown event type after verify) → **200** `{ received: true, ignored: true }`. Forward compatible. Hub drops passthrough with a silent `return` then 200.
7. `kind=vaulted` is **not paid** (`NP-GW-008`). Persist token on the payer/session if paper 07 has a column; **do not** call fulfill-as-paid. First S1 dogfood can ignore vaulted events (200) if `$0` setup is out of scope.
8. Idempotency insert `(org_id, provider, event_id)` unique. Duplicate → **200** `{ received: true, duplicate: true }`. **Do not** fulfill again. This is `NP-GW-006` and the dogfood “webhook retry no-ops.”
9. Optional Stripe business key `(org_id, provider, "paid:"+payment_intent_id)` so `checkout.session.completed` and `payment_intent.succeeded` share one fulfill. Unique violation → duplicate. Steal Hub `BuildBusinessKey`. Refunds (paper 07) must **not** use the PI-level key.
10. Call paper 07 fulfill **in this request**, same DB transaction as the insert. Success → **200** `{ received: true }`. Fulfill throw after insert: you need a status on the log row (`received` vs `applied`) **or** you have lost money. Prefer one transaction: insert + session `paid` + journal + `RCPT-` + audit. If that exceeds the PSP timeout, **still** commit money then 200; do not ACK before the insert.
11. Inbound metadata `org_id` / `tenant_id` mismatch vs URL `orgId` → **200** ignore + log (do not 400 — Stripe retries a 400 with the same poison). Hub already rejects mismatch except platform checkouts. New Pay has no platform/system org; mismatch is always ignore.

Timeouts: Stripe expects a fast 2xx. Keep the handler in-process (Linux room). Do not enqueue Hub inbox work as the ACK.

### 5.3 Signature per dogfood rail

| Provider | Header / field | Algorithm | Secret stored |
|----------|----------------|-----------|---------------|
| Stripe | `Stripe-Signature` (case-insensitive) | Stripe `EventUtility.ConstructEvent` (HMAC + timestamp ~300s) | `whsec_…` |
| CHIP | `X-Signature` base64 | RSA SHA256 PKCS#1 v1.5 over **raw JSON bytes** | PEM (`Webhook.public_key` preferred) |
| Billplz | form `x_signature` | HMAC-SHA256 hex, sorted `key+value` joined by `\|`, dual-compute with/without extra fields, fixed-time compare | 128-char X-Signature key |

Missing header/field → **400** (treat as verify fail). Do not 200.

**Do not** JSON re-serialize before HMAC/RSA. Hub CHIP uses `Encoding.UTF8.GetBytes(rawBody)` after reading the request as string — preserve the same bytes the PSP signed. ASP.NET model binding will break CHIP and Stripe.

Billplz is **form**, not JSON. `Content-Type: application/x-www-form-urlencoded`. A JSON body on the Billplz route is unusable → **400**.

Xendit `x-callback-token` and Razorpay `X-Razorpay-Signature` are **not** S1. Do not add “just the verify function.”

### 5.4 Event id per rail (what goes in the unique tuple)

| Provider | `event_id` to store | `provider_txn_id` | Dual-event collapse |
|----------|---------------------|-------------------|---------------------|
| Stripe | Stripe `evt_…` (`stripeEvent.Id`) | PaymentIntent id (or bill later) | Business key `paid:{pi}` / `failed:{pi}` |
| CHIP | `{kind}:{purchaseId}` e.g. `paid:purch_…` | purchase id | Fail then pay are **different** event ids (live `a1afc09` judgment). Do **not** use bare purchase id as event_id. |
| Billplz | `{kind}:{billId}` | bill id | Same. Create-time `due` is `failed:bill_…`. Paid is `paid:bill_…`. |

Never invent a Guid when the id is missing. Hub CHIP/Billplz/Razorpay fail-closed (`UnusableAfterVerify` / `Verified=false`). **400** after verify if id missing so the PSP stops. **Do not** 200 a random id — that is a double-fulfill on retry with a new Guid.

Currency: fail-closed. Live Stripe/CHIP refuse invented MYR. Billplz is MY-only; hardcode `MYR` is acceptable **for Billplz only**.

### 5.5 400 vs 200 (normative table)

| Situation | HTTP | Body (sketch) | PSP retry? |
|-----------|------|----------------|------------|
| Empty body | **400** | `empty body` | No / few |
| Unknown provider | **400** | `unsupported provider` | No |
| Org not found / no secret | **400** | `gateway not configured` | Stripe will retry; Ada must paste keys. Prefer 400 over 500. |
| Signature / HMAC / RSA fail | **400** | `invalid signature` | Stops after attempts. **Not 500.** |
| Unusable after verify (no id, no currency) | **400** | `unusable payload` | Stops poison |
| Unknown event type, verified | **200** | `ignored: true` | Stops. Forward compatible |
| Setup / preauthorized vault, no capture | **200** | `vaulted: true` | Stops. **Not paid** |
| Duplicate `(org, provider, event_id)` or business key | **200** | `duplicate: true` | Stops. No journal |
| First paid, fulfill committed | **200** | `received: true` | Stops |
| First failed (decline), session not paid | **200** | `received: true` | Stops. Paper 07: do not invent PAST_DUE on a healthy seat without this event |
| Handler crash **before** insert | **500** | — | Retry is correct |
| Handler crash **after** insert, before 200 | **500** then retry | — | Retry hits duplicate → 200. Fulfill must be in the same txn as insert so this is safe |
| Bearer missing on **this** route | n/a | There is no Bearer | — |

Do not 401. Do not 204 (harder to log). Do not 201.

Tests that must exist (names, not code): empty body 400; bad signature 400; duplicate event_id 200 and fulfill called once; tenant A event_id does not collide with tenant B (shared CHIP credentials — that is why the tuple includes `org_id`); health `GET /v1/health` still does not call PSP or One.

### 5.6 Plane A vs Plane B tables

Do not share `processed_inbound_events` without a `source` discriminant ([012/09](../012-one-to-pay/09-webhooks-events.md) §10.3). Stripe `evt_…` and One UUIDs are different spaces. Hub’s original unique `(Provider, EventId)` **without tenant** was 008’s P0 and 009 B04-P06. Live Hub fixed tenant into the index. New Pay starts with `(org_id, provider, event_id)` and `provider` never equals `one`.

---

## 6. How checkout fixture grows into a real charge without Hub session tables

### 6.1 What the fixture is today

Files: `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/{CheckoutEndpoints,CheckoutSession,CheckoutStore,CreateCheckoutRequest}.cs`.

`CheckoutSession` fields: `Id`, `OrgId`, `Amount`, `Currency`, `Status`, `SuccessUrl`, `CancelUrl`, `CreatedAt`.

Create:

- `MemberGate` (Bearer + One `member` on `org_id`).
- `amount > 0` else 400.
- Currency default `MYR`, uppercased.
- `Idempotency-Key` header or body `idempotency_key`, keyed by `org_id + "\n" + key` in a `ConcurrentDictionary`.
- `Id = Guid.NewGuid().ToString("N")` — **32-char hex, not a `RCPT-`**. Receipt numbers are paper 07. Do not start printing this id as a document number (`NP-DOC-002`).
- `Status = "open"` always.
- 201 snake_case JSON (`OneClient.Json` / `JsonNamingPolicy.SnakeCaseLower`).

Get: 404 if missing; 403 if caller is not member of `session.OrgId`; 200 the same object.

Tests lock: no bearer 401; other org 403; idempotent create; default MYR; amount 0 → 400; health still skips One.

pay-spec comment: “Checkout is a fixture (open session), not a charge.”

There is **no** `checkout_url`, **no** `provider`, **no** payer email (`NP-BUY-001` still todo), **no** webhook, **no** `paid`.

### 6.2 Growth, not a second table

Replace `CheckoutStore` `ConcurrentDictionary` with a **Pay DB** table that **is** this session. Do **not** add `IntegrationCheckoutSessions`. Do **not** add Commerce `CheckoutSession` hop-2. Do not stamp `subscription_id` into Stripe metadata as the only pointer — Pay’s `checkouts.id` **is** the pointer.

Additive fields (sketch):

| Field | Why |
|-------|-----|
| `provider` | `stripe` \| `chip` \| `billplz` — chosen from the org’s **active** config (explicit body `provider` later; v1: the only active dogfood config) |
| `provider_session_id` | Stripe `cs_…`, CHIP purchase id, Billplz bill id |
| `checkout_url` | Hosted hop-2. Paper 05 redirects the buyer here |
| `payer_email` / `payer_name` | `NP-BUY-001`. Required by CHIP/Billplz generate (`GatewayCommon.TryResolveEmail` refuses placeholder) |
| `status` | `open` → `paid` \| `expired` \| `failed` (`NP-CHK-004`) |
| `expires_at` | Optional. Hub M2M 24h. Fine to steal a TTL; a late paid webhook on an expired **open** session should still fulfill (buyer paid). Hub M2M dropping late pay on `expired` is 009 B04-P25 — do not copy |
| `setup_future_usage` | Bool for later vault. S1 first charge can be false |
| `idempotency_key` | Persist unique `(org_id, idempotency_key)` like the fixture, like Hub M2M |

Do **not** add `SetupFutureUsage` as a reason to mint Stripe `mode=setup` on the first dogfood product.

### 6.3 Create path when keys exist

```text
POST /v1/checkouts
  MemberGate(member)
  validate amount / currency (steal MYR min RM 2 when leaving fixture-cheap amounts)
  load active gateway for org (exactly one dogfood config, or body.provider)
  decrypt api key
  CreateHostedCheckout(PSP)  // Stripe Session / CHIP purchase / Billplz bill
  persist session open + checkout_url + provider_session_id
  return 201 including checkout_url
```

If no keys: **409** or **400** `payments_not_configured` (Hub M2M uses ProblemDetails `PAYMENTS_NOT_CONFIGURED`). Do not fall back to Billplz.

If PSP generate fails: 502/400 with PSP message; **no** open session that can never be paid — or persist `open` with no URL only if you have a retry. Prefer fail the POST.

Stamp metadata the PSP will round-trip:

- Stripe / CHIP JSON metadata: `org_id`, `checkout_id` (Pay session id). Steal `ApplyPayingTenantMetadata` so Pay never clobbers a paying org with a “system” org — new Pay has no system org.
- Billplz: `callback_url` includes `checkout_id`; `reference_1` can be `checkout_id` for S1 (Hub used `subscription_id` because Commerce was SoT).

Buyer **does not** call `POST /v1/checkouts`. Merchant (or later `lzr_sk_` worker) does. Buyer opens `checkout_url` (paper 05) **without** a One account (`NP-CHK-007`). Success/cancel URLs are the fixture fields already stored — they are **redirects**, not fulfillment. Hub portal `/success` must not treat landing as paid; new checkout Vite the same.

### 6.4 Webhook path attaches to the same row

```text
POST /v1/webhooks/stripe/{orgId}
  verify
  insert (orgId, stripe, evt_…)
  lookup session by org + metadata.checkout_id
       or provider_session_id = session.payment_intent / cs_ / purchase / bill
  if kind=paid and session.open: fulfill() // paper 07, same txn
  if kind=paid and session.paid: already applied (duplicate business key or replay)
  if kind=failed and session.open: mark failed (optional) — do not reverse a paid session
```

No `IEventBus`. No `GatewayPaymentCompletedIntegrationEvent`. No `IntegrationCheckoutGatewayEventsHandler` that marks `failed` terminal and then **drops** a later completed (009 B04-P02). Fail-then-pay on the same Billplz bill / CHIP purchase: **paid wins** if money captured. Session status is not a one-way `failed` latch.

### 6.5 What “without Hub session tables” does not mean

It does not mean “stateless cashier, metadata only.” Hub README used to claim that and then grew `IntegrationCheckoutSessions` because Billplz strips metadata. New Pay **keeps** a session row — the fixture’s row — so Billplz query-string loss is recoverable by bill id.

It does not mean “Commerce hop-2 GenerateCheckoutSessionQuery.” There is no Commerce module.

It does not mean “M2M `/integrations/payments/checkouts`.” `NP-SOON-007` is a **second** consumer of the **same** `/v1/checkouts`, later.

### 6.6 Idempotency alignment (`NP-API-006`)

| POST | Key | Unique |
|------|-----|--------|
| `POST /v1/checkouts` | `Idempotency-Key` (header or body) | `(org_id, key)` — fixture already |
| PSP webhook | PSP `event_id` | `(org_id, provider, event_id)` |
| Future refund POST | paper 07 | not this paper |
| Future off-session | Stripe `lazuar-offsession:{attempt}` | V1 |

Two Stripe events for one PI must not two-journal: business key **or** session already `paid` short-circuit. Paper 07 sees fulfill called **once**.

---

## 7. Mapping wrap-rails matrix into Pay code + merchant UI copy

### 7.1 The matrix new Pay actually needs

Hub’s static class is larger than the product. New Pay needs **four** questions, next to the charge function:

| Question | Stripe | CHIP | Billplz | Razorpay/Xendit (later) |
|----------|--------|------|---------|-------------------------|
| Hosted checkout (buyer present) | Y | Y | Y | Y (parked) |
| Vault + off-session auto-charge | Y, if PM exists | Y, if `recurring_token` exists; **cards not FPX** | **N** | N until soak |
| API refund | Y | Y | **N** (mark in dashboard) | later |
| Homemade FPX e-mandate | N | N | N | N (`SupportsEmandate` false) |

Unknown / blank / `OFFLINE` = reminder-only. Never silent debit (`NP-GW-007`).

Code shape (sketch, not a Contracts project):

```csharp
static bool SupportsOffSession(string provider) =>
    provider is "stripe" or "chip";

static bool IsReminderOnly(string provider) => !SupportsOffSession(provider);

static bool SupportsEmandate(string _) => false;
```

A billing job that cannot see this helper will AUTO_CHARGE Billplz. That is how Hub dunning grew `PaymentGatewayCapabilities` readers in `PastDueDunningProcessor` and `DunningCampaignAutoChargeGuard`. When paper 07’s renew job exists, it **must** call this helper. Until then, **do not implement AUTO_CHARGE**.

### 7.2 Mapping Hub flags → new Pay behavior

| Hub flag | Readers today | New Pay |
|----------|---------------|---------|
| `SupportsOffSession` | BillingEngine, dunning AUTO_CHARGE skip, vault persist, `$0` checkout, product DTO, arrears, off-session handler | Charge function + future renew job + merchant copy |
| `IsReminderOnlyGateway` | inverse | Same |
| `SupportsApiRefund` / `RequiresMarkRefunded` | RecordRefund, RefundModal | Paper 07 refund SOP; Billplz button is “mark refunded,” not “call PSP” |
| `SupportsDuitNowQr` | **none** in Payments generate | Do not show a DuitNow toggle on `:5178` |
| `SupportsHostedWallet` | **none** | Do not show GrabPay tiles on `:5179`. PSP hosted page owns tiles |
| `SupportsEmandate` | always false | Keep false. No Curlec `method=emandate` |

### 7.3 Merchant UI copy (steal wording, new origin)

Paper 04 owns `:5178`. This paper owns the **sentences** so wrap-rails cannot be redesigned as a green “Auto-debit ON” switch.

**When provider is Billplz** (Hub amber, steal):

> **Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it. There is no silent auto-charge. Use Stripe or CHIP Collect when you need recurring auto-debit.

**When provider is Stripe:**

> Cards on Stripe-hosted Checkout. Apple Pay / Google Pay may appear when the Stripe account can take cards and the buyer’s device supports them. Lazuar does not host wallet buttons. Recurring auto-debit runs only after a card is saved (a real payment method). A setup form without a charge is not a payment.

**When provider is CHIP:**

> CHIP Collect hosted page shows whatever you enabled on the brand (FPX, cards, DuitNow QR, wallets). Lazuar does not rebuild those rails. Auto-debit is **card token only**. We will not silent-debit FPX. CHIP does not run your subscription clock — Pay does, and only with a stored token.

**When no provider configured:**

> Paste test keys for Stripe or CHIP (or Billplz). Keys are encrypted at rest. They never ship in this browser app.

**Product / pay-link create (when paper 04 builds it):** if `IsReminderOnly`, show Hub product-form equivalent: “Not auto-debit.” If off-session, “Card on file; we charge the saved method.” Do not use Hub `summary.notAutoDebit` i18n keys from `lazuar-portal`.

**Checkout `:5179`:** do not print “FPX e-mandate enrolled.” Do not print “auto-debit enabled” on Billplz. Success page is not paid (paper 05).

### 7.4 What Hub still gets wrong — do not reimport

- Razorpay `SetupFutureUsage` used to mint a **card registration** link while capability said reminder-only (009 B04-P11). Live adapter **discards** the flag and always `PaymentLink.Create`. If new Pay ever adds Razorpay, keep it a payment link.
- Commerce still sent `SetupFutureUsage: true` for every recurring interval, including Billplz (ignored) and Razorpay (was harmful). New Pay: pass `setup_future_usage` **only** for Stripe/CHIP when the product is recurring **and** you intend to vault. Billplz path never sets it.
- Stripe adapter maps setup to `PAYMENT_COMPLETED`. New parse must not.
- CHIP live maps `purchase.preauthorized` + token to `PAYMENT_COMPLETED`. New parse: `vaulted`. Amount 0 is not cash.
- Ops `gatewaySupportsOffSession` in `lazuar-ops/src/lib/utils.ts` duplicates C#. New merchant UI should take `supports_off_session` from **GET `/v1/orgs/{id}/gateways`**, not re-implement the matrix in Vite.
- AUTO_CHARGE as a dunning step string (`EMAIL|WHATSAPP|AUTO_CHARGE`) is Commerce cathedral. Not S1.

### 7.5 Renewals (pointer only)

`NP-FUL-004` V1: billing job mints checkout **or** off-session charge. Wrap-rails:

```text
if SupportsOffSession && token present → ChargeOffSession (V1)
else → mint hosted checkout + email link (reminder)
never → homemade e-mandate
never → Stripe Billing subscription.updated
```

Paper 07 designs PAST_DUE. This paper forbids the Billplz branch from calling charge.

---

## 8. Production PSP requirements (webhook URL publicly reachable, staging keys, ngrok only for local)

### 8.1 The PSP has to POST to Pay

Stripe, CHIP, and Billplz **will not** deliver to `http://127.0.0.1:8081`. Hub learned this the hard way on Billplz (`CALLBACK_BASE_NOT_PUBLIC`). CHIP registrar’s localhost→`lazuar-local-dev.com` rewrite is a **fiction DNS** that Billplz public-base would refuse.

| Environment | Pay origin the PSP sees | Keys | Tunnel |
|-------------|-------------------------|------|--------|
| **Laptop unit tests** | none (fake parser, no HTTP to PSP) | fixtures | no |
| **Laptop dogfood** | **ngrok / Cloudflare Tunnel / similar** HTTPS origin → 8081 | **test/sandbox** | **yes, local only** |
| **Staging** | real `https://pay-staging.…` (or whatever paper 03/10 deploy) | staging / test keys, optionally a sealed live brand | **no ngrok** |
| **Production** | real public HTTPS | live keys | **no ngrok** |

ngrok in production is an incident. ngrok URL in a CHIP webhook subscription that Ada forgets to delete is how test payments hit a laptop. On staging cutover, **re-register** CHIP webhooks and Stripe endpoint URLs to the staging origin.

### 8.2 What Ada must configure at the PSP

**Stripe.** Dashboard → Developers → Webhooks → `https://<pay>/v1/webhooks/stripe/<orgId>`. Events at least: `checkout.session.completed`, `payment_intent.succeeded`, `payment_intent.payment_failed`. Signing secret `whsec_…` pasted into Pay. API key `sk_test_…` / `sk_live_…`. Do not enable Stripe Billing customer portal as a Pay feature. Do not subscribe to `customer.subscription.updated`.

**CHIP.** Brand ID + secret key in Pay. Pay may auto-register callback `https://<pay>/v1/webhooks/chip/<orgId>` for `purchase.paid`, `purchase.payment_failure`, `purchase.preauthorized` (vault), `payment.refunded` (paper 07). Test mode is the CHIP dashboard switch. Test cards from CHIP docs (`4444 3333 2222 1111`, etc.) — do not hardcode them in Vite.

**Billplz.** Collection ID + secret + 128-char X-Signature. Sandbox account is a **different host** (`billplz-sandbox.com`). Per-bill `callback_url` must be public HTTPS unless the insecure flag is on (**dev only**). Redirect URL ≠ callback URL. Buyer landing on `success_url` is not paid.

### 8.3 Staging keys vs live keys

- Default new rows `environment=test` (Hub default). First-time dogfood should refuse `sk_live_` if `environment=test` (`KEY_MODE_MISMATCH` judgment).
- Production deploy: master key from a secret manager, not repo `.env`. Paper 03 host seams.
- Compose today still points at `apps/lazuar-api` (focused README). Do not put live PSP secrets in that compose to “test 8081.”
- One staging proof is **NOT PASSED** ([011/02](../011-new-lazuar-pay/02-one-integration.md)). Money dogfood can still run against **local One** + **PSP test mode** + **tunneled webhook**. That is not “production-ready.” Production-ready adds: public origin, live or sealed staging keys, Plane A `tenant.suspended` **before** live charges ([012/09](../012-one-to-pay/09-webhooks-events.md) money gate).

### 8.4 Local topology collisions (honesty)

- One API documented `:8080`. Hub API `:8080`. Pay `:8081`.
- This laptop sometimes remaps **One API to 8081** because Aura owns 8080 ([012/09](../012-one-to-pay/09-webhooks-events.md) §9.3). Pay **also** wants 8081. They cannot both bind 8081.
- A CHIP/Stripe webhook registered at `http://localhost:8081/v1/webhooks/...` will never arrive.
- Tunnel must target whichever port Pay actually bound, and that public URL is what you paste at the PSP / auto-register.

Billplz `AllowInsecureBillplzCallback` is a **developer hatch**. Leave it off in staging/prod. If it is on in prod, anyone who can guess a bill payload still needs the HMAC secret — but you have also admitted HTTP callbacks.

### 8.5 What production does **not** require on day one of rails

- Five adapters.
- Stripe Billing.
- CHIP Send payouts.
- A Pay-native FPX bank list.
- Hub `PublicDnsFallback`.
- Outbound merchant webhooks (Plane C).
- Live LHDN.
- ngrok reserved domains as the staging origin.

---

## 9. Anti-goals

Do not do these in the name of S1 money rails:

1. **Copy `Modules/Payments` into `apps/lazuar-pay`.** No `AddPaymentsModule`, no `PaymentsDbContext`, no `IMediator`, no inbox/outbox jobs, no `BuildingBlocks`. IsolationTests are the tripwire.
2. **Register five adapters** “because the factory already did.” `NP-GW-003`, `NP-SOON-008`, `NP-LAT-002`.
3. **Treat Stripe Billing `customer.subscription.updated` / `invoice.paid` as SoT.** `NP-XX-012`. Checkout `mode=subscription` is the same refuse.
4. **Homemade FPX e-mandate** / Curlec `method=emandate` / Billplz Agreements v5 as a quiet extra. `NP-XX-011`. `SupportsEmandate` stays false.
5. **Count setup / `setup_intent.succeeded` / CHIP `purchase.preauthorized` as paid.** `NP-GW-008`. Fail the slice if `RCPT-` is minted for amount 0 vault.
6. **Silent debit on Billplz** (or Razorpay/Xendit). Including dunning AUTO_CHARGE, including “the adapter returned false so we mark PAST_DUE and try again.”
7. **Vite secrets.** `sk_live_` in `:5178` / `:5179`.
8. **Hub session tables** (`IntegrationCheckoutSessions`, Commerce hop-2, platform `SystemOrganizationId` credits).
9. **BILLPLZ last-resort** when no config exists.
10. **Plane A HMAC on the PSP route** (or Stripe-Signature on `/v1/one/webhooks`). Different secrets, different ids, different tables.
11. **ACK 200 before idempotency insert + fulfill txn.** Retry double-journal is a fail lock ([011/03](../011-new-lazuar-pay/03-first-slice.md)).
12. **Signature fail 500.** Hub does this. Stripe retries until the endpoint looks like an outage.
13. **Empty body 500.** Hub used to. Live Hub is 400. Stay 400.
14. **Invent event ids.** Guid fallback was a 007 CHIP gap; live adapters fail-closed. Keep fail-closed.
15. **Wait for One** to grant buyer access or to ACK a webhook before writing the journal. Same-handler rule. Paper 07.
16. **Retarget `lazuar-ops` / `lazuar-portal` at 8081** to paste keys. New UIs `:5178` / `:5179`. `NP-XX-018` for admin.
17. **Pay password form** or a second org table to own “the merchant who pasted Stripe.” One tenant id is `org_id`.
18. **Buyer as Zitadel human.** Webhook does not `POST /members`.
19. **ngrok as staging/prod origin.**
20. **`Jwt:Secret` as KMS.**
21. **DecryptOrPlaintext** so a wrong master key silently sends ciphertext to CHIP as Bearer.
22. **Tick `NP-GW-002` because CHIP showed a card form.** Stripe row means Stripe adapter.
23. **Tick `NP-GW-006` because Hub’s unique index exists.** Old tree does not count ([011/11](../011-new-lazuar-pay/11-checklist.md) seed).
24. **Implement paper 07’s journal in this slice “while we are in the handler.”** Name the call; do not design accounts.
25. **Outbound `payment.completed` to Aura** as a blocker for first-party dogfood. Plane C later. `NP-SOON-007`.
26. **Apple Pay domain verification / Pay-hosted wallet buttons.** Wrap: PSP hosted page.
27. **Fee = 0 meaning “the fee is zero.”** Stamp unknown (`NP-MON-002`). Paper 07 books unknown ≠ 0.
28. **Platform / Hub SaaS fee / utility credits** (`PlatformCheckoutTypes`). New Pay is not billing Hub.

---

## 10. Open questions (CHIP vs Billplz — do not pick silently)

### 10.1 The question

**Which Malaysian rail will you actually dogfood: CHIP Collect or Billplz?**

011 refuses to pick. This paper refuses to pick. The implementation checklist that follows 013 must write the name, not “CHIP/Billplz.”

### 10.2 Evidence from 011 (product law)

| Source | What it says |
|--------|----------------|
| 01 must-have | “One Malaysian rail you will actually dogfood (**CHIP** or **Billplz**), not five adapters on day one.” |
| 01 dogfood sentence | “pastes **CHIP or Stripe** keys” — **Billplz not named** |
| 03 / 12 step 8 | “Store BYOK Stripe **or** CHIP/Billplz keys” |
| NP-GW-001 notes | “Stripe **or** CHIP/Billplz for dogfood” |
| NP-GW-002 | Stripe card checkout, Dogfood **Y** |
| NP-GW-003 | One Malaysian rail (CHIP **or** Billplz), Dogfood **Y** |
| NP-GW-007 | Stripe/CHIP auto-charge if vaulted; **Billplz-class = reminder + hosted link** |
| NP-LAT-002 | Razorpay, Xendit later, reminder-only labelled |
| NP-SOON-008 | Second gateway only after the first two are boring in production |

Reading: you may paste **one** key set for the first living charge (step 8 OR). The checklist still wants Stripe **and** a MY rail as dogfood rows. CHIP is the only rail that can stand in for “MY + vault” **without** being Stripe. Billplz cannot stand in for `NP-GW-002`. Stripe cannot stand in for `NP-GW-003`.

### 10.3 Evidence from old dogfood / product papers (007, Hub defaults)

| Source | Billplz | CHIP |
|--------|---------|------|
| [007/05](../007-feats/05-malaysia-gateways.md) | “Primary rail. Keep. Deepen.” Malaysian default for guest money. “Aura’s entire System B story is Hub + Billplz.” Informal “send a bill.” Flat FPX. **No** adapter off-session. Agreements v5 **not** implemented. | “Second primary rail. Keep. Production-harden.” Developer Collect, JSON metadata, refund API, card token, DuitNow QR, no annual fee. CHIP does **not** run the subscription clock — Pay does. |
| [007/13](../007-feats/13-payments-refunds-rails.md) | v3 bills, HMAC, refund always false | purchases, RSA, refund + recurring token |
| Root README honesty watermark | Billplz renewals = emailed hosted link | CHIP listed with Stripe as real adapters |
| Ops default `useState` | `BILLPLZ` | CHIP is first `<option>` in the live dropdown |
| `CheckoutSessionCashier` last resort | `"BILLPLZ"` | — |
| 01 dogfood sentence | not named | named with Stripe |

007/05 is explicit that Billplz **wins informal send-a-link** and that promising Billplz card-on-file is a lie until Agreements exist **and** the merchant’s collection has Auto-Deduct — which Pay **refuses to homemade**. CHIP is the K2 for merchants who need cards + vault + refunds + DuitNow QR.

### 10.4 Evidence from live adapters (engineering soak)

| Axis | CHIP | Billplz | Favors |
|------|------|---------|--------|
| Off-session / wrap-rails auto-charge | Yes, card token; reference lookup for idempotency (not Stripe-class) | No; returns false | CHIP if you want renew dogfood later without Stripe |
| Refund API | Yes | No; mark-refunded | CHIP |
| Webhook | JSON, RSA, metadata first-class, fees sometimes present | Form HMAC, query metadata, fee always 0 | CHIP |
| Local webhook | Same public-HTTPS problem as everyone; Hub rewrote localhost to fiction DNS | **Fail-closed** without public HTTPS; best teacher of §8 | Billplz teaches PSP production requirements earlier |
| Test vs live | Same API host, dashboard toggle | Separate sandbox host + account | Billplz harder to mis-point at live if `environment` is test |
| MY mental model | “modern Billplz” for developers | Default SME / institution bill | Billplz for Aura-shaped merchants |
| First `RCPT-` complexity | Brand ID + auto PEM | Collection ID + 128-char X-Signature + public callback | CHIP fewer moving parts if registrar works; Billplz more honest about tunnels |
| `NP-GW-008` hazard | Live maps preauthorized+token as `PAYMENT_COMPLETED` | No setup mode | Billplz cannot fail NP-GW-008; CHIP can if you copy live parse |
| Stripe overlap | Cards on hosted page may **look like** NP-GW-002 | Clearly not Stripe | Risk of ticking the wrong row on CHIP |

### 10.5 Evidence from 008 / 009 (residual, not a pick)

- 008/009 spent more pages on CHIP `$0` skip_capture (B04-P01) than on Billplz generate. That is “CHIP is the vault rail we actually poked,” not “CHIP is done.”
- Billplz late-fail-after-pay (B04-P08) is a namespacing leftover. Both rails need session-state rules in paper 07.
- Empty-body and tenant-scoped EventId are **fixed on live Hub**; they do not distinguish CHIP vs Billplz.

### 10.6 Decision table for the later checklist (still unpicked)

| If the first production merchant is… | Prefer | Because |
|--------------------------------------|--------|---------|
| You (first-party), cards, want off-session next | **CHIP** or **Stripe**; MY row = CHIP | 01 sentence; vault; one MY adapter that is not reminder-only |
| You, FPX-only, reminder-only is acceptable | **Billplz** | 007/05 primary rail; wrap copy is already written |
| A salon that “already has Billplz” | **Billplz** | BYOK; do not force a second acquire |
| A salon that needs silent renew | **CHIP** or **Stripe**, never Billplz | `NP-GW-007` |
| Checklist completionist | **Stripe + CHIP** (interpretation D) | Both Dogfood Y rows, no reminder-only confusion |
| Fastest first `RCPT-` on a laptop | **Stripe test mode** + ngrok | Best webhook docs; then add MY rail before claiming `NP-GW-003` |

**Do not** default to Billplz because `CheckoutSessionCashier` did. **Do not** default to CHIP because the 01 sentence listed it first without reading Billplz’s public-callback honesty. **Do not** implement both “just in case” plus Razorpay “while we are here.”

### 10.7 Other open questions (not silent picks)

1. **Key PUT `authz/check` `admin` vs `member`.** This paper recommends `admin` for paste/rotate (§4.6). Paper 08 must not reopen that as `member` without editing `NP-GW-009`.
2. **CHIP auto-register vs Ada-pastes PEM.** Hub auto-registers. Safer dogfood: auto-register only when `Pay:PublicBaseUrl` is HTTPS and non-loopback (steal Billplz public-base, apply to CHIP too).
3. **Stripe dual events:** collapse on business key now, or ignore `checkout.session.completed` and only fulfill `payment_intent.succeeded` (and setup-mode only on `setup_intent.succeeded` as **vaulted**). Either is honest; both-as-paid is not.
4. **Master key algorithm.** Paper 03 host seams. This paper only forbids `Jwt:Secret` fallback and Vite.
5. **MYR minimum RM 2** on `POST /v1/checkouts` when leaving the fixture. Steal `CheckoutAmountRules` or keep `> 0` until a PSP rejects RM 0.01. Do not silently skip the PSP.
6. **`tenant.suspended` (Plane A) before first **live** charge.** 012/09 money gate. Rails can be built and test-charged before Plane A; **live** keys should not fire without suspend-stop. Not a reason to block test-mode dogfood.
7. **Fee unknown.** Paper 07. Adapters must not write `0` as known zero when expand failed (live Stripe/CHIP stamp `gateway_fee_status=unknown` — steal the stamp).

### 10.8 What this paper already closed

- Plane B is not Plane A and not Plane C.
- Webhook lives on Pay `/v1`, 8081, public in staging/prod.
- Empty body 400; bad signature 400; duplicate 200 no-op; fulfill in the same handler as verify (journal designed in 07).
- Keys encrypted per `org_id` in Pay DB; never Vite; VIEWER-as-Hub maps to “not `admin`” on One.
- Five adapters, Stripe Billing SoT, homemade e-mandate, setup-as-paid, Hub session tables, MediatR cashier — refuse.
- CHIP vs Billplz remains an **open named choice**, with evidence, not a silent default.

---

*End of 013-prods paper 06. Do not implement from this file. Do not flip `NP-GW-*` from this file.*
