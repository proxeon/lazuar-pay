# W1-LP-037 — Apple Pay / Google Pay via Stripe wrap

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-037` (“Apple Pay / Google Pay **via Stripe** (wrap, don’t rebuild)”). Tracker row in [00-checklist-tracker.md](../00-checklist-tracker.md) §C: `LP-037 | Apple Pay / Google Pay | Wave 1 | Lazuar = N | Stripe = Y`.  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) reuses `LP-037` for “Dashboard net cash / stats”. That is a different numbering scheme. Ignore it.

**Invariant:** Wallets are Stripe card-network tokens on hosted Checkout. Lazuar enables them by asking Stripe for `card` on session create. Lazuar does not host Apple Pay / Google Pay buttons, register merchant domains, or speak Payment Request / Elements.

---

## 0. Scope lock

In scope:

- `StripeGatewayAdapter.GenerateCheckoutAsync` Checkout Session create (`SessionCreateOptions`)
- The **child PaymentIntent** Stripe creates for that session (inherits session `payment_method_types`)
- A small testable helper + unit tests next to the existing `ApplySetupFutureUsage` tests

Out of scope (do not expand this ticket):

- Hop-1 portal Apple / Google Pay buttons, logos, or Payment Request API (`LP-021` / reserved `LP-UX-010` / [09 §9](../09-checkout-and-payment-links.md))
- Stripe Elements, Express Checkout Element, Embedded Checkout, publishable key
- `ApplePayDomainService` / `PaymentMethodDomain` registration (hosted Checkout is `checkout.stripe.com`)
- Billplz / CHIP / Razorpay adapters or “Apple Pay” copy on a non-Stripe workspace
- `payment_method_types: ['fpx']` / `['grabpay']` / `['link']` (not this rail; [04-stripe.md](../04-stripe.md) + [13-payments-refunds-rails.md](../13-payments-refunds-rails.md))
- Passing `apple_pay` or `google_pay` as Checkout / PaymentIntent types (invalid on this API)
- Off-session `ChargeOffSessionAsync` PaymentIntent allow-list (already bound to a vaulted PM)
- Webhook map, fee extract, TypeSpec, cashier port, capability matrix UI
- Custom Checkout domain (`LP-UX-006`)

**Already shipped, do not redo:** hosted Stripe Checkout `mode=payment` (`LP-PAY-001`), metadata on Session **and** `PaymentIntentData`, `setup_future_usage` + `CustomerCreation=always` (LP-047), inbound verify (LP-090).

---

## 1. Product contract (what “done” means)

1. When the active gateway is Stripe, hop-2 Stripe Checkout can show Apple Pay / Google Pay **because the session asks for cards**.
2. Lazuar does **not** rebuild wallets. Buyer still leaves hop 1 and pays on Stripe’s host.
3. Non-Stripe gateways stay silent. No fake wallet chrome.
4. Tracker honesty after ship: `LP-037` Lazuar cell becomes **`W`** (wrap), not `Y`. We are not an Apple Pay company.

Stripe’s own docs (2026): *“No additional configuration is required to use Apple Pay in Checkout.”* *“Using Google Pay in Checkout requires no additional code implementation.”* Domain register is for **merchant-hosted** buttons (Elements), not `checkout.stripe.com`.

So this ticket is an **explicit `card` allow-list** on session create — not a wallet SDK.

---

## 2. Stripe physics (do not invent types)

Apple Pay / Google Pay are **not** Checkout Session / PaymentIntent `payment_method_types`.

They are wallets that tokenize a card. The PaymentMethod Stripe stores is type `card` (wallet apple_pay / google_pay). Recurring / `setup_future_usage` works the same as a typed card. Fees are card fees. Webhooks are the same `checkout.session.completed` / `payment_intent.succeeded` already mapped.

Stripe.net **48.0.1** (`Directory.Packages.props`) `SessionCreateOptions.PaymentMethodTypes` enum is:

`acss_debit`, `affirm`, `afterpay_clearpay`, `alipay`, `alma`, `amazon_pay`, `au_becs_debit`, `bacs_debit`, `bancontact`, `billie`, `blik`, `boleto`, **`card`**, `cashapp`, `customer_balance`, `eps`, **`fpx`**, `giropay`, **`grabpay`**, `ideal`, `kakao_pay`, `klarna`, `konbini`, `kr_card`, **`link`**, `mobilepay`, `multibanco`, `naver_pay`, `oxxo`, `p24`, `pay_by_bank`, `payco`, `paynow`, `paypal`, `pix`, `promptpay`, `revolut_pay`, `samsung_pay`, `satispay`, `sepa_debit`, `sofort`, `swish`, `twint`, `us_bank_account`, `wechat_pay`, `zip`.

**There is no `apple_pay` or `google_pay`.** Those strings exist on Payment Element `paymentMethodOrder` and wallet display prefs. Sending them on `SessionService.CreateAsync` is an API error.

`apple_pay` / `google_pay` on `excluded_payment_method_types` also errors; Stripe says use the wallets hash on Elements instead. Irrelevant here — we have no Elements.

Two Stripe modes for Checkout PMs ([docs](https://docs.stripe.com/payments/checkout/payment-methods.md?payment-ui=stripe-hosted)):

| Mode | How | Wallets |
|------|-----|---------|
| Dynamic (current) | Omit `payment_method_types` | Appear if Dashboard has **cards** on and the device/browser qualifies |
| Manual | Set `payment_method_types` | Appear if **`card` is in the list**. Dashboard list is overridden. Methods not listed are hidden. |

You cannot mix: you cannot “add wallets” on top of dynamic PMs. Wallets are not a third type.

---

## 3. Current code

### 3.1 Checkout Session create — the only hop-2 write

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` `GenerateCheckoutAsync` (lines 32–63):

```csharp
var options = new SessionCreateOptions
{
    Mode = "payment",
    CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : null,
    LineItems = new List<SessionLineItemOptions> { /* PriceData amount * 100, Quantity */ },
    Metadata = metadata,
    PaymentIntentData = new SessionPaymentIntentDataOptions
    {
        Metadata = metadata
    },
    SuccessUrl = successUrl,
    CancelUrl = cancelUrl,
};

ApplySetupFutureUsage(options, setupFutureUsage);
var session = await service.CreateAsync(options);
```

Missing today:

- `PaymentMethodTypes` — unset → Dashboard dynamic PMs ([04](../04-stripe.md) line 138, [13](../13-payments-refunds-rails.md) line 361)
- `PaymentMethodConfiguration` — we do not store a Dashboard config id
- `UiMode` — default hosted page (correct)
- Idempotency key on session create (not this ticket)

`SessionPaymentIntentDataOptions` has **no** `PaymentMethodTypes`. The child PI takes types from the **session**. Setting them on `PaymentIntentData` is not an SDK option.

`ApplySetupFutureUsage` (lines 403–414) already mutates the same `options` object. LP-037 should be the same shape: a static helper, called once before `CreateAsync`.

### 3.2 PaymentIntent create — off-session only

`ChargeOffSessionAsync` (lines 284–295) is the **only** `PaymentIntentService.CreateAsync` in this adapter:

```csharp
var options = new PaymentIntentCreateOptions
{
    Amount = (long)(amount * 100),
    Currency = currency.ToLowerInvariant(),
    Customer = customerId,
    PaymentMethod = tokenId,
    OffSession = true,
    Confirm = true,
    Description = description,
    Metadata = meta
};
```

This PI is already bound to a vaulted PM. The buyer is not choosing Apple Pay. The token may be a wallet-provisioned **card**, or (if Dashboard Link was on) a `link` PM.

**Do not set `PaymentMethodTypes = ["card"]` here.** It can reject a non-card vaulted type. Wallets for LP-037 are a **hosted Checkout** problem.

One-shot on-session PI is never created by Lazuar. Checkout owns that PI.

### 3.3 Callers — already Stripe-only when K2 is Stripe

No port change. `IPaymentGatewayAdapter.GenerateCheckoutAsync` is gateway-blind. The factory picks `StripeGatewayAdapter` only for `STRIPE`.

| Caller | Path | Notes |
|--------|------|-------|
| `CheckoutSessionCashier.GenerateAsync` | Commerce + M2M + detailed query | Decrypts BYOK key, `EnsureKeyModeMatchesGateway`, calls adapter |
| `GenerateCheckoutSessionQueryHandler` | Commerce hop 2, update-payment, custom link | Thin cashier wrap |
| `InitiateCheckoutCommandHandler` | Portal product checkout | `SetupFutureUsage = product.Interval != "one_time"`; `preferredGateway = product.GatewayName` |
| `CreateIntegrationCheckoutCommandHandler` | M2M cashier | Same adapter |
| `GenerateSystemCheckoutSessionQueryHandler` | Platform utility top-up | Same `GenerateCheckoutAsync`; card allow-list is correct |

Billplz / CHIP / Razorpay `GenerateCheckoutAsync` implementations must not grow wallet flags.

### 3.4 Hop 1 — no wallet surface (keep it that way)

`lazuar-portal` `CheckoutForm.tsx` collects email/name and `window.location.href = result.url`. No Payment Request, no Stripe.js, no method tiles.

Ops Stripe form (`PaymentSettingsPage.tsx` ops + admin twin) stores `sk_` + `whsec_` only. No “enable Apple Pay” checkbox. Correct — Dashboard owns card/wallet eligibility.

`PaymentGatewayCapabilities` (LP-047) is off-session vs reminder-only. It does **not** need a `SupportsWallets` flag for this wrap.

---

## 4. Decision lock

| Question | Lock |
|----------|------|
| What string do we send? | **`card` only.** |
| `apple_pay` / `google_pay` in the list? | **Never.** Invalid on Checkout Session / PaymentIntent types in Stripe.net 48. |
| `fpx` / `grabpay` / `link`? | **No.** This ticket is wallets. Stripe FPX is expensive and cannot vault ([04](../04-stripe.md)). Link is Dashboard / card-checkout chrome. |
| Dynamic Dashboard PMs? | **Overridden** for Stripe sessions once we set the list. A tenant who relied on Stripe-hosted FPX/GrabPay will stop seeing those tiles. Product already says cheap FPX is Billplz/CHIP, not Stripe. |
| Domain verify / `payment_method_domains`? | **No.** Hosted Checkout. Tenant custom domain is Wave 4. |
| Off-session PI types? | **No change.** |
| Hop-1 logos when `GatewayName=STRIPE`? | **No.** `LP-UX-010`. |
| Billplz-only workspace Apple Pay copy? | **Refuse** ([13](../13-payments-refunds-rails.md) Apple Pay section). |
| New adapter / port method / TypeSpec? | **No.** |

This is the “[13] Later” line: *optional Stripe Checkout `payment_method_types` including `card` + wallets, gated on K2=Stripe* — except “wallets” are not types. The implementable form is `["card"]` on the Stripe adapter only.

[04] still says “Dashboard-owned PMs; do not set `payment_method_types`.” That was the **implicit** wrap. LP-037 makes the card/wallet path **explicit**. After this ticket, 04/13 should say: Stripe adapter sends `card`; other Dashboard APMs are not requested.

---

## 5. Minimal change

One helper + one call site + tests. No schema, no DI, no UI required.

### 5.1 Helper (mirror `ApplySetupFutureUsage`)

In `StripeGatewayAdapter.cs`:

```csharp
internal const string CardPaymentMethodType = "card";

internal static void ApplyCardWalletPaymentMethodTypes(SessionCreateOptions options)
{
    options.PaymentMethodTypes = new List<string> { CardPaymentMethodType };
}
```

Call immediately after building `options` (before or after `ApplySetupFutureUsage`; order does not matter). Do **not** touch `PaymentIntentData` for types.

Keep the comment factual: wallets (Apple Pay / Google Pay) ride on `card`; listing `apple_pay`/`google_pay` is invalid; this list replaces Dashboard dynamic PMs for the session.

### 5.2 Do not change

| File | Why |
|------|-----|
| `ChargeOffSessionAsync` | Confirm-with-PM; types would not show a wallet sheet |
| `IPaymentGatewayAdapter` / `CheckoutSessionCashier` | Already Stripe-gated by factory |
| Other adapters | Not Stripe |
| `ParseWebhookAsync` | Wallet success is already a card PI / Checkout session |
| Portal / ops / TypeSpec | Not required to wrap |
| `PaymentGatewayCapabilities` | Off-session honesty, not wallets |

### 5.3 Optional one-liner (same PR only if it stays a paragraph)

Ops + admin Stripe credential blurb: Apple Pay / Google Pay appear on **Stripe-hosted** Checkout when the Stripe account can take cards and the buyer’s device supports them. Enable cards in Stripe Dashboard → Payment methods. Not available on Billplz. Domain verify is only if they leave Stripe’s host (we do not).

If that grows into a settings toggle, stop — out of scope.

---

## 6. Tests

Existing `StripeGatewayAdapterTests` already constructs `SessionCreateOptions` without hitting Stripe. Add the same style:

1. `ApplyCardWalletPaymentMethodTypes_SetsCardOnly` — list is exactly `{ "card" }`, count 1, does not contain `apple_pay` / `google_pay` / `fpx`.
2. After apply, `PaymentIntentData` is unchanged (null or existing metadata only). Proves we did not invent a PI types field.

Do **not** add a live Safari/Wallet test in CI. Manual check if someone has a Stripe test account + Safari + a real card in Wallet (Stripe returns a test token; [testing/wallets](https://docs.stripe.com/testing/wallets.md)).

Keep existing `ApplySetupFutureUsage_*` tests green; both helpers must compose on one `SessionCreateOptions`.

Filter: `StripeGatewayAdapterTests`.

---

## 7. Call graph after the change

```
portal / M2M / system top-up
        │
        ▼
CheckoutSessionCashier / GenerateSystemCheckoutSessionQueryHandler
        │
        ▼
PaymentGatewayFactory.GetAdapter("STRIPE")
        │
        ▼
StripeGatewayAdapter.GenerateCheckoutAsync
        │
        ├─ SessionCreateOptions.Mode = payment
        ├─ ApplyCardWalletPaymentMethodTypes → ["card"]   ← LP-037
        ├─ ApplySetupFutureUsage (unchanged)
        └─ SessionService.CreateAsync
                │
                ├─ hosted Checkout URL (hop 2)
                └─ child PaymentIntent.payment_method_types = ["card"]
                        │
                        └─ Stripe shows card + Apple Pay + Google Pay
                           when device / account / region allow
```

Off-session renewals stay: vaulted `pm_…` → `PaymentIntentCreateOptions` with `PaymentMethod` set. No new types.

---

## 8. Adjacent IDs (do not fold in)

| ID | Relation |
|----|----------|
| `LP-PAY-001` | Seed “Stripe hosted checkout (cards, Apple/Google Pay via Stripe)” — adapter already shipped; wallets were implicit. This ticket makes the allow-list explicit. |
| reserved `LP-UX-010` | Hop-1 visibility + “domain verify in Stripe” copy. Not this wrap. |
| `LP-046` | 3DS / SCA — Stripe Checkout + wallets already handle it. Do not add `request_three_d_secure` here. |
| `LP-047` | Vault / `setup_future_usage`. Wallet PMs vault as cards. Do not change. |
| `LP-030` | Cards Visa/MC — already `W`. Wallets are the same rail. |
| `LP-033`–`036` | DuitNow / TnG / GrabPay / Shopee — Wave 4 local rails. Not Stripe wallets. Tracker Wave 4 bucket “LP-033–037” is a **label slip**; implement-ids put **LP-037 in Wave 1**. |
| Inventory `LP-037` | Dashboard net cash. Different scheme. |

---

## 9. Risks

| Risk | Handling |
|------|----------|
| Implementer sends `apple_pay`/`google_pay` | Stripe 400. Tests forbid those strings. |
| Tenant used Stripe Checkout for FPX/GrabPay | Those tiles disappear. Acceptable: Stripe is the card/wallet rail; document in 04/13 when this ships. Do not add `fpx` to “fix” it. |
| Account cannot accept cards | Session create fails with Stripe’s message; cashier already returns `GatewayError`. Same as today if Dashboard had no usable PMs. |
| Wallet not shown (Chrome vs Safari, no card in Wallet, India, HTTP) | Stripe device/eligibility. Not a Lazuar bug. No hop-1 fallback button. |
| Custom domain later | Then domain register becomes real (`LP-UX-006` + `LP-UX-010`). Not now. |

---

## 10. Files (implementer touch list)

| Path | Change |
|------|--------|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Helper + call from `GenerateCheckoutAsync` |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` | Two helper tests |

Optional:

| Path | Change |
|------|--------|
| `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` | One Stripe paragraph |
| `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` | Same twin |

Do not edit Billplz/CHIP/Razorpay, cashier, portal checkout, TypeSpec, or webhook parse.

---

## 11. Done when

- Stripe Checkout Session create always sends `payment_method_types: ['card']`.
- Unit tests prove the list is `card` only.
- No new wallet UI, no domain API, no other gateway touched.
- After ship, tracker `LP-037` Lazuar = **`W`**. Do not claim `Y`.
