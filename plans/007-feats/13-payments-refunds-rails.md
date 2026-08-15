# 13 — Payments, refunds, and rails

**Program:** `plans/007-feats` — competitor features vs **Lazuar Pay** (Checkout-as-a-Service / Compliance CaaS).  
**Date:** 2026-08-16  
**Status:** Full uncondensed analysis. **No product code from this file.** Not a ship ticket.  
**Workspace inspected:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Public host:** `hub.lazuar.com`  
**Author role:** staff payments / rails analyst for the **Payments cashier** — adapters, schemes guests tap, refunds, disputes, webhook integrity, settlement.

**Product in scope is Lazuar Pay, not Aura salon software.** Aura is a **first-party integrator** of Hub M2M checkouts. Salon deposits, 50 ≠ 95, desk POS, and guest `/book` are out of this tracker. This chapter does not re-score Fresha/Booksy salon policy. It scores **what money rails and after-capture jobs a CaaS cashier must own**, versus Stripe, Billplz, CHIP, Razorpay, HitPay, Xendit, Paddle, and PayPal.

**This file is not:**

- [`05-malaysia-gateways.md`](./05-malaysia-gateways.md) — that file is *which Malaysian PG is a rail vs a rival product*. This file is *which schemes those PGs present, and what Pay’s four adapters actually do with them*.
- [`09-checkout-and-payment-links.md`](./09-checkout-and-payment-links.md) — buyer journey / portal chrome.
- [`12-dunning-and-recovery.md`](./12-dunning-and-recovery.md) — PAST_DUE campaigns and WhatsApp honesty.
- [`14-developer-dx-api-webhooks.md`](./14-developer-dx-api-webhooks.md) — keys, TypeSpec, outbound DX.
- [`19-refuse-list-and-adjacents.md`](./19-refuse-list-and-adjacents.md) — company-shape constitution. This file applies it to Connect / crypto / Pay-native QR.

**Standing constraints (do not contradict):**

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank (ADR 019 / 021).
- Buyer money on Billplz / Stripe / CHIP / Razorpay (tenant K2) is **not** Lazuar’s SaaS fee and is **not** utility-credit top-up.
- Wrap rails. Do not rebuild acquiring. Do not add `application_fee_amount`.
- Crypto / USDC is **ADR 020 Phase 3** / `LP-XX-010`. Do not treat as near-term.
- Payouts / split / Stripe Connect / CHIP Send / Billplz Payment Order as a *Pay product* — **refuse**.
- Do not invent a Pay-native FPX, DuitNow, or TnG connector. Rails live **inside** the active K2 hosted page unless this file later-builds a method selector.
- `docs/001-gaps/02-payment-webhooks.md` and `docs/001-gaps/06-payments-module.md` are **historical**. Several of their P0s are already fixed in the 16 August 2026 tree. This chapter treats those docs as residual risk, not current truth.

---

## Method

### What this chapter answers

Competitors that Malaysian merchants and SEA integrators compare against do not sell “a checkout URL.” They sell **rails** (schemes the buyer taps) plus **money-movement after capture**:

- Cards (Visa / Mastercard / Amex) with 3-D Secure.
- FPX retail and FPX corporate (B2B1).
- DuitNow QR and DuitNow Online Banking / RPP.
- eWallets: Touch ’n Go, Boost, GrabPay, ShopeePay, MAE.
- Apple Pay / Google Pay.
- BNPL: Atome, Grab PayLater, SPayLater.
- PayPal.
- Crypto / USDC (Phase 3 only).
- After the money: full/partial refunds, voids, disputes/chargebacks, payouts/splits, multi-currency/FX, idempotent webhooks, replay, reconciliation, settlement reports, saved cards / tokenization / off-session.

Lazuar Pay’s Payments module is a **gateway orchestrator** (module README: “the Cashier”). It is **not** a ledger, not a subscription engine, not a fulfillment engine, not an acquirer. The product question is: **which of those rails and after-capture jobs does Pay implement, which does it inherit from the hosted K2 page, and which must it refuse?**

### Sources read (not summarized away)

| Source | Absolute path | Role |
|--------|---------------|------|
| Payments module (live) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/` | Adapters, webhook pipeline, M2M sessions, refund/off-session handlers |
| Port | `…/Application/Ports/IPaymentGatewayAdapter.cs` | checkout, parse, refund, portal, off-session |
| Stripe adapter | `…/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Cards/global, 3DS via Checkout, disputes, refunds, portal, off-session |
| Billplz adapter | `…/Gateways/BillplzGatewayAdapter.cs` | v3 bills, HMAC, query metadata, refund always false |
| CHIP adapter | `…/Gateways/ChipCollectGatewayAdapter.cs` | purchases, RSA, refund API, recurring token |
| Razorpay adapter | `…/Gateways/RazorpayGatewayAdapter.cs` | Payment Links + registration links |
| Billplz public base | `…/Gateways/BillplzPublicBase.cs` | Prod host allow-list + `App:BillplzEnvironment` |
| Gateway common | `…/Gateways/GatewayCommon.cs` | Minor units (round vs truncate) |
| Webhook HTTP | `…/Infrastructure/Endpoints.cs` | Hop A allow-list |
| M2M HTTP | `…/Infrastructure/IntegrationEndpoints.cs` | `POST/GET /integrations/payments/checkouts`, `/me` |
| Webhook handler | `…/Application/Commands/ProcessGatewayWebhookCommandHandler*.cs` | Verify, filter, EventId + BusinessKey, session merge, publish |
| M2M create | `…/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` | Persist-then-provider, idempotency key |
| Cashier | `…/Application/Services/CheckoutSessionCashier.cs` | Gateway resolve + `KEY_MODE_MISMATCH` |
| Amount rules | `…/Application/Services/CheckoutAmountRules.cs` | MYR min RM 2; else 0.50 |
| Config aggregate | `…/Domain/Aggregates/TenantPaymentConfiguration.cs` | Encrypted secrets + `IsActive` |
| Session aggregate | `…/Domain/Aggregates/IntegrationCheckoutSession.cs` | 24h pending session |
| Webhook log | `…/Domain/Entities/PaymentWebhookLog.cs` | EventId + BusinessKey |
| Refund execute | `…/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` | Real amount; fee still 0 |
| Off-session execute | `…/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Failed event publish |
| Hop B enqueue | `…/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | `payment.completed` / `payment.failed` only |
| Config save | `…/Commands/UpdatePaymentConfigCommandHandler.cs` | Encrypt + CHIP auto-webhook |
| Commerce refund | `Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs` | Publisher of `GatewayRefundRequested` |
| Commerce refund HTTP | `Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` | `POST /transactions/{id}/refund` |
| Billing refund | `Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` | Ledger reverse + tax scale |
| Billing dispute | `Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` | Utility credits **only** |
| LHDN refund | `Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | Cancel &lt;72h / CN ≥72h |
| Outbox DLQ | `BuildingBlocks/Infrastructure/MessageProcessingResultApplier.cs` | 5 attempts then `Dead` |
| TypeSpec | `packages/api-spec/modules/payments/{routes,models}.tsp` | Public M2M contract |
| Event catalog | `apps/lazuar-docs/docs/reference/events.md` + `docs/integrations/webhooks.md` | Outbound `payment.*` honesty |
| Cashier quickstart | `docs/payments-integration-quickstart.md` | Integrator surface |
| Module README | `Modules/Payments/README.md` | Declared intent (stale: still says “no pending checkouts”) |
| Gap 02 / 06 (stale) | `docs/001-gaps/02-payment-webhooks.md`, `06-payments-module.md` | Historical P0s |
| ADR 004 / 009 | `docs/architecture-decision-log/` | Stateless metadata, outbox type dispatch |
| ADR 019 / 020 / 021 | same | CaaS BYOK; Phase 3 USDC; Compliance CaaS |
| Ops UI | `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` | Four-gateway BYOK |
| Tests | `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/` | Failed event, Billplz HMAC, M2M, secrets |
| Tracker schema | `plans/007-feats/20-sequencing-and-tracker-schema.md` | `LP-PAY-001`…`012` + reserved `013`–`018` |
| Sibling 05 / 09 / 12 / 14 / 19 | this folder | Rail vs rival; checkout UX; dunning; DX; refuse |
| Competitor public pages (2026-08-16) | Billplz, CHIP, HitPay, Stripe, Razorpay, PayPal | Scheme facts below |

### How to read a cell in this file

| Mark | Meaning |
|------|---------|
| **Y** | Pay implements the job, or the active K2 hosted page exposes it and Pay fulfills the resulting money event |
| **P** | Partial: processor-side only, one adapter only, or honesty gap |
| **N** | Not implemented |
| **R** | Refuse (company-shape / physics / Phase 3) |

**Pay vs K2.** Almost every Malaysian rail (FPX, DuitNow, TnG, Atome) appears on **Billplz / CHIP hosted checkout**, not as a Pay checkbox. Saying “Pay supports FPX” without saying “via Billplz collection config” is a lie. This file always names the layer.

**Ours vocabulary** (for tracker promotion) matches `20-sequencing-and-tracker-schema.md`: `shipped` · `partial` · `backend-only` · `absent` · `refuse`. `shipped` requires a demoable path for the audience the row names.

### Current Pay topology (locked)

```text
Integrator (Aura / any app)  or  Commerce portal
    POST /api/v1/integrations/payments/checkouts   (K1, payments.checkouts:write)
      or Commerce InitiateCheckout → GenerateCheckoutSession*
    → IntegrationCheckoutSession (payments schema, 24h TTL)  [M2M only]
    → CheckoutSessionCashier → IPaymentGatewayAdapter
    → buyer browser → K2 hosted page
         (Billplz bill / Stripe Checkout / CHIP purchase / Razorpay link)

Hop A:  K2 cloud  POST  Pay  /api/v1/webhooks/payments/{gateway}/{tenantId}
        verify signature → PaymentWebhookLog (EventId + BusinessKey)
        → GatewayPaymentCompleted | GatewayPaymentFailed | GatewayDisputeCreated
        → IntegrationCheckoutGatewayEventsHandler marks M2M session
        → OutboundWebhookRequested (payment.completed | payment.failed)

Hop B:  Pay worker  POST  integrator URL
        X-Lazuar-Signature: t=…,v1=…
```

Commerce public buy links (`/public/commerce/*`) are a **second** product line that reuses the same adapters. Do not mix Commerce `subscription.*` / `order.completed` with Payments `payment.*`.

### What “current code” already fixed vs the 001-gaps writeups

Treat the following as **done in tree** (16 August 2026). Do not re-open them as P0s from `06-payments-module.md`:

| Historical gap claim | Live fact |
|----------------------|-----------|
| Refunds amount always 0 / no publisher | `RecordRefundCommand` publishes real `Amount` + `Currency` + `GatewayTransactionId`. Handler rejects `Amount <= 0`. |
| `GatewayPaymentFailed` never published | Handler publishes it. Off-session failures also publish. Tests exist. |
| Secrets plaintext | `AesSecretVault` encrypts `ApiKey` / `WebhookSecret` on write; `DecryptOrPlaintext` for legacy rows. |
| No `IsActive` | Column restored; soft-disable keeps credentials; webhooks/refunds still run. |
| Razorpay `Guid.NewGuid()` EventId | Fail-closed if no `X-Razorpay-Event-Id` and no payment id. |
| CHIP `purchase.preauthorized` = paid | Only `purchase.paid` → `PAYMENT_COMPLETED`. |
| Stripe dual events double-fulfill | `BusinessKey = EventType + ":" + GatewayTransactionId` unique index. |
| Outbox always marks processed | `MessageProcessingResultApplier`: 5 attempts, `2^n` minutes, then `Status=Dead`. |
| Unique-index race → 500 | Handler swallows SQLSTATE 23505 → HTTP 200. |
| Off-session metadata sparse | Stripe/CHIP/Razorpay attach `type`, `subscription_id`, `tenant_id`, `receipt`, optional `dunning_campaign_id`. |
| No pending-session store | `IntegrationCheckoutSession` + Billplz `checkout_id` query + merge-by-provider-session-id. |
| Unknown gateway → 500 | Allow-list 400 in `Endpoints.cs`. |
| Stripe PI path `GatewayFee=0` | PI path now expands `latest_charge.balance_transaction`. |
| Billplz prod heuristic `Contains("lazuar.com")` | `BillplzPublicBase` allow-list: `hub.lazuar.com` / `pay.lazuar.com` / `api.lazuar.com` + `App:BillplzEnvironment`. |
| No tests | `tests/Lazuar.ModuleTests/Payments/` covers webhook, Billplz HMAC, M2M create, off-session, secrets. |

Residual holes (this chapter’s job) are **rails product honesty**, **refund webhooks**, **disputes beyond utility credits**, **settlement**, **method-level capability flags**, and **refusing Connect / crypto / PayPal-as-Pay**.

---

## Rail map MY/SEA/global

A **rail** is a scheme the buyer taps. A **processor** (K2) is who presents that scheme. Pay speaks processors. Buyers speak rails.

### Malaysia — what buyers actually tap (2026)

| Rail | Scheme owner | Buyer motion | Typical MY processor that shows it | Chargeback / mandate physics | Pay layer today |
|------|--------------|--------------|------------------------------------|------------------------------|-----------------|
| **Visa / Mastercard** | Card networks | PAN + 3DS | Billplz (MPGS / 2c2p / Secure Acceptance), CHIP, Stripe, HitPay, iPay88 / Fiuu | Chargebacks exist. 3DS shifts some liability. Vault possible. | **P** — hosted K2. Pay never names the brand. Stripe Checkout `mode=payment` may show cards if the Stripe account has them. |
| **Amex** | Amex | PAN + 3DS | Stripe (if enabled); some CHIP/Billplz collections | Chargebacks. Higher MDR. | **P** — only if K2 collection/account has Amex. Pay has no Amex flag. |
| **FPX retail (B2C)** | PayNet | Redirect to Maybank2U / CIMB Clicks / … then approve | Billplz (`fpx`), CHIP, HitPay, iPay88, **Stripe FPX** (expensive: same 3%+RM1 as cards in 2026 writeups) | **No vault.** No off-session. No hold. Unpaid callback is normal. | **P** — Billplz/CHIP hosted. Pay does not send `payment_methods=[{code:fpx}]`. Stripe adapter does **not** set `payment_method_types: ['fpx']`. |
| **FPX corporate (B2B1)** | PayNet | Corporate internet banking, dual-control | Billplz (`fpxb2b1`), some CHIP/iPay88 enterprise | Same no-vault physics. Higher ticket. | **N** as a Pay concept. **P** if the Billplz collection has B2B1 on. |
| **DuitNow QR** | PayNet / DuitNow | Scan from any bank or e-wallet app | Billplz, CHIP (including CHIP mini in-person), HitPay | Instant account-to-account. No card network. No classic chargeback. Refunds are new credit transfers. | **P** — hosted / CHIP mini. Pay has no QR generation. Reserved tracker: `LP-PAY-013`. |
| **DuitNow Online Banking / RPP** | PayNet | Bank-app pay-to-proxy (often collapsed into “DuitNow” in marketing) | HitPay, some Billplz/CHIP labels | Same A2A physics | **N** as a distinct Pay method. Collapsed into “whatever the hosted page shows.” |
| **Touch ’n Go eWallet** | TnG Digital | Super-app approve | Billplz (`touchngo`), CHIP, HitPay | Wallet T&Cs; limited chargeback | **P** hosted |
| **Boost** | Axiata | Super-app | Billplz (`boost`), HitPay, some CHIP | Wallet | **P** hosted |
| **GrabPay** | Grab | Super-app | Billplz, CHIP, HitPay, **Stripe GrabPay** (MY) | Wallet; Grab PayLater is a different rail | **P** hosted; Stripe adapter does not request `grabpay` |
| **ShopeePay** | Shopee | Super-app | Billplz, HitPay (long activation), some CHIP | Wallet | **P** hosted |
| **MAE** | Maybank | Maybank app wallet / MAE QR | Usually via DuitNow QR or Maybank QR, **not** a separate PSP method code | A2A / wallet | **N** as a named rail. MAE→DuitNow QR looks like DuitNow to the processor. |
| **Apple Pay** | Apple + card networks | Face ID on Safari / app | Stripe (if country+domain verified), some 2c2p/MPGS via Billplz if the sub-acquirer enabled it | Card physics + Apple token | **P/N** — Pay never requests Apple Pay. Stripe hosted Checkout *may* show it if the account is set up. Billplz adapter does not pass Apple Pay flags. Seed `LP-PAY-001` treats this as “via Stripe,” not a Pay wallet product. |
| **Google Pay** | Google + card networks | Chrome / Android | Same as Apple Pay | Card physics | Same as Apple Pay |
| **Atome** | Atome | 3 instalments | Billplz (`twoctwopipp` / instalment codes), CHIP PayLater, HitPay | BNPL lender is Atome. Merchant sees one capture. Partial refunds are processor-specific. | **P** hidden — if collection has Atome, webhook is a normal paid bill. Pay has no instalment ledger. |
| **Grab PayLater / SPayLater** | Grab / Shopee | Instalments inside super-app | Billplz, HitPay | Same BNPL shape | **P** hidden |
| **PayPal** | PayPal | PayPal login | Billplz (`isupaypal` in collection method codes), Stripe+PayPal in some regions, standalone PayPal | PayPal buyer protection + card chargebacks | **N** as a Pay adapter. **P** only if Billplz collection enables PayPal and the bill page shows it. |
| **USDC / crypto** | Chains / Coinbase / BTCPay | Wallet push | CHIP marketing lists **stablecoins**; ADR 020 Phase 3 names USDC/USDT, BTCPay, Coinbase Commerce | Irreversible; no chargeback; FX is the point | **R** near-term (`LP-XX-010`). CHIP hosted *may* show stablecoins; Pay does not parse chain txids. |
| **Cash / static QR** | Informal | Sticker / bank transfer | Not a Pay rail | Proof photo | **Out of Payments.** Commerce offline mark-paid (`LP-COM-006`) is a different job. |

### FPX retail vs corporate (do not flatten)

| | FPX retail | FPX B2B1 corporate |
|--|------------|--------------------|
| Who | Consumer internet banking | Company maker/checker |
| Billplz code | `fpx` | `fpxb2b1` |
| Typical ticket | RM 2 – a few thousand | High B2B |
| Vault | No | No |
| Pay awareness | None | None |
| Competitor who exposes the toggle | Billplz dashboard / collection API; HitPay method list; CHIP brand settings | Same |
| Pay UI | None | None |

Pay’s Billplz adapter posts `collection_id`, `email`, `name`, `amount` (sen), `callback_url` with query metadata, `redirect_url`, `description`, `reference_1` / `reference_2`. It never PATCHes collection payment methods. The merchant (or Pay ops) turns rails on **in Billplz**.

`CheckoutAmountRules.MyrMinimum = 2.00` matches Billplz/FPX practical floors. That is Pay’s only FPX-aware constant.

### DuitNow QR vs DuitNow Online Banking

Marketing pages say “DuitNow” as if it were one button. It is not.

| | DuitNow QR | DuitNow Online Banking / proxy |
|--|------------|--------------------------------|
| Motion | Scan | Bank-app transfer to proxy (phone/NRIC/account) |
| In-person | CHIP mini, HitPay POS, static sticker | Rare at desk |
| Online checkout | Hosted QR on the bill | Redirect / in-app |
| Cross-border (Project Nexus) | PayNow / PromptPay / QRIS interlink (emerging 2025–26) | Different product |
| Pay | No QR render, no proxy pay | No |

Informal MY competitor is often a **static DuitNow QR** on a laminated card. That is not Hop A. Pay must not generate a merchant QR that settles outside the K2 account (`LP-XX` adjacent: duplicate settlement).

Reserved `LP-PAY-013` is “DuitNow QR as first-class rail.” Honest implementation is **not** a new adapter. It is either (a) documentation + optional logo when K2=Billplz/CHIP, or (b) a method-filter if Billplz/CHIP APIs accept a per-purchase method lock. Do not mint a `DuitNowGatewayAdapter`.

### eWallets — five names, one Pay behavior

TnG, Boost, GrabPay, ShopeePay, MAE are **not** five adapters. They are five buttons on someone else’s hosted page.

| Wallet | Billplz method code (API list, 2026) | HitPay | CHIP | Stripe MY | Pay |
|--------|-------------------------------------|--------|------|-----------|-----|
| TnG | `touchngo` | Y | Y (e-wallets) | **N** (2026 writeups) | Hosted only |
| Boost | `boost` | Y | Y | **N** | Hosted only |
| GrabPay | via Razer/2c2p/wallet codes | Y | Y | **Y** (Stripe GrabPay) | Hosted only; Stripe adapter does not request it |
| ShopeePay | wallet codes | Y (≈30-day activation) | Y | **N** | Hosted only |
| MAE | not a first-class PSP code | via DuitNow / Maybank QR | via DuitNow | **N** | Invisible |

Pay must **not** grow `IPaymentGatewayAdapter` implementations named `TouchNGoGatewayAdapter`. That is rebuilding Billplz. File 05 already classifies Fiuu / senangPay / iPay88 the same way: extra **processors**, not extra **schemes**.

### Apple Pay / Google Pay

Global billing companies (Stripe, Paddle, Chargebee) treat wallets as table stakes because they are **card-on-file companies**. Malaysian FPX-first buyers will not demand Apple Pay first.

Physics:

- Apple Pay / Google Pay are **card network tokens**, not DuitNow.
- They require domain verification, merchant IDs, and a processor that speaks the wallet.
- Stripe Checkout can show them when the account + domain are verified.
- Billplz/CHIP may surface them only if the sub-acquirer (MPGS / 2c2p) enabled wallets on that collection.
- Pay’s Stripe adapter sets `Mode = "payment"` and **does not** set `PaymentMethodTypes`. Stripe then uses the Dashboard’s enabled methods. That is an implicit, untested path.

**Later:** optional Stripe Checkout `payment_method_types` including `card` + wallets, gated on K2=Stripe.  
**Refuse:** “Apple Pay” copy on a Billplz-only workspace.

Seed `LP-PAY-001` already covers “Stripe hosted checkout (cards, Apple/Google Pay via Stripe).” Do not split wallets into a Wave 0 row.

### BNPL (Atome, Grab, SPayLater)

| Actor | What they sell | What Pay should do |
|-------|----------------|--------------------|
| US salon OS / GlossGenius | Branded BNPL as *their* product | Do not copy the brand |
| Billplz / HitPay / CHIP | Enable Atome on the collection | Leave at K2 |
| Atome | Underwrites the buyer | Pay never talks to Atome |
| Pay / Billing ledger | One `AmountPaid` | Do **not** split Commerce `BalanceDue` into instalments |

CHIP’s homepage (2026): “FPX, DuitNow QR, cards, PayLater, E-wallets and **stablecoins**.” PayLater and stablecoins are CHIP Collect features. Pay’s CHIP adapter creates a purchase with products + metadata. It does not select `PayLater` vs `FPX`.

B2B financing (Capchase / Pipe / Funding Societies) is ADR 020 Phase 3 — `LP-XX-010`. Different from consumer Atome.

### PayPal

| Path | Exists? | Use in MY CaaS |
|------|---------|----------------|
| Standalone PayPal Commerce Platform | Global | High FX, buyer protection, not FPX |
| Billplz `isupaypal` | Collection method code in Billplz API docs | Occasional merchants who sell to tourists |
| Stripe+PayPal | Region-dependent | Not MY default |
| Pay adapter | **None** | Correct |

A PayPal adapter would be a fifth K2. Only add it if a paying tenant cannot get PayPal through Billplz and will churn. Not Wave 0–1. MoR-shaped PayPal (they hold funds, buyer protection) is a **company-shape** risk even as BYOK — file 07 / 19.

### Crypto / USDC (Phase 3 — not near-term)

ADR 020 §11 and ADR 021 Pillar 3 name USDC/USDT, BTCPay, Coinbase Commerce for **cross-border + zero-rated LHDN export**. CHIP already markets stablecoins on Collect.

**Do not:**

- Put a USDC button on MY Commerce checkout.
- Parse chain reorgs in `ProcessGatewayWebhookCommandHandler` this year.
- Treat CHIP stablecoin captures as a Pay “crypto product.”

**Do:** keep the Phase 3 watermark (`LP-XX-010`). If CHIP’s webhook for a stablecoin purchase still looks like `purchase.paid` + an amount, the cashier will fulfill it as a normal payment. That is an **accident of hosted methods**, not a launch.

### SEA / global (context, not MY Wave 0–1)

| Country | Default rail buyers expect | Processor analogue | Pay |
|---------|----------------------------|--------------------|-----|
| Singapore | **PayNow**, GrabPay, cards | HitPay, Stripe SG | **R** as a MY v1 named rail. `CheckoutAmountRules` allows non-MYR (min 0.50) but there is no PayNow adapter and no SGD product SKU. |
| Indonesia | QRIS, e-wallets, VA | Xendit, Midtrans, Doku | ADR 020 Phase 1 wishlist. **No adapter.** File 06. |
| Thailand | PromptPay | Omise, 2c2p, HitPay | No |
| Philippines | GCash, Maya, InstaPay | Xendit, PayMongo | No |
| India | **UPI**, cards, netbanking, wallets, EMI | **Razorpay**, Cashfree, PayU | Razorpay adapter exists (payment links + registration links). UPI appears on Razorpay hosted page if the merchant account is IN. Pay does not request `upi`. Currency on webhook defaults to **`"MYR"`** if Razorpay omits it — a footgun for INR. `LP-PAY-004`: keep working; do not market IN until ICP. |
| Global cards | Visa/MC/Amex + 3DS + Apple/Google Pay | Stripe | Stripe adapter is the global card path (`LP-PAY-001`) |

HitPay is the **SEA expectation teacher** (file 06), not a fifth consumer-app adapter. If a merchant needs HitPay, add it as **Pay K2**, same port — later, Wave 4, after Billplz/CHIP are sellable.

### Competitor processor map (who owns which rail)

| Processor | FPX | DuitNow QR | MY wallets | Cards+3DS | Apple/GPay | BNPL | PayPal | UPI | PayNow | Refunds API | Vault | Disputes | Settlement report | Payouts/split |
|-----------|:---:|:----------:|:----------:|:---------:|:----------:|:----:|:------:|:---:|:------:|:-----------:|:----:|:--------:|:-----------------:|:-------------:|
| **Billplz** | Y | Y | Y | Y | P | Y | P (`isupaypal`) | N | N | **Dashboard + Payment Order (disburse), not refund-by-bill-id** | **N** | Weak / none as card-network desk | Dashboard | Payment Order (disburse) |
| **CHIP Collect** | Y | Y | Y | Y | P | Y | N | N | N | **Y** (purchase refund) | **Y** (recurring token) | Limited | **Y** (marketed) | **CHIP Send** (separate product) |
| **Stripe** | Y (pricey) | **N** | GrabPay only | Y | Y | N (MY) | Region | N | SG only | **Y** | **Y** | **Y** (Radar + disputes) | Dashboard / Payouts API | Connect (refuse) |
| **Razorpay** | N | N | IN wallets | Y | P | IN EMI | N | **Y** | N | **Y** | **Y** (tokens) | Y (IN) | Dashboard | Route / X (IN marketplace) |
| **HitPay** | Y | Y | Y | Y | P | Y | P | N | **Y** | Y | P | P | Y | Payouts to merchant bank |
| **Xendit / Midtrans** | N (ID) | QRIS | ID wallets | Y | P | P | N | N | N | Y | Y | P | Y | Xenplatform |
| **Fiuu / iPay88** | Y | Y | Y | Y | P | P | P | N | N | Y | P | P | Y | Enterprise |
| **PayPal** | N | N | N | Y | P | N | **Y** | N | N | Y | Y | Y (buyer protection) | Y | Payouts |
| **Paddle / Lemon** | N | N | N | Y | Y | N | N | N | N | Y | Y | Y (they are MoR) | Y | They hold the money |
| **Lazuar Pay** | P hosted | P hosted | P hosted | P hosted | implicit Stripe | P hidden | N | P hosted IN | R | **P** Stripe/CHIP/Razorpay API; Billplz false | **P** Stripe/CHIP/Razorpay | **P** Stripe created only | **N** | **R** |

### 3-D Secure

3DS is not a Pay feature. It is a card-network challenge on the K2 page.

| Processor | 3DS | Pay involvement |
|-----------|-----|-----------------|
| Stripe | PaymentIntents / Checkout handle 3DS; `requires_action` | Adapter treats Checkout as redirect. Off-session `confirm` + `off_session=true` will **fail** if 3DS is required — handler publishes `GatewayPaymentFailed`. |
| Billplz | MPGS / 2c2p 3DS on card bills | Invisible. Unpaid callback if buyer abandons 3DS. |
| CHIP | Hosted 3DS | `purchase.payment_failure` → `PAYMENT_FAILED` |
| Razorpay | Native 3DS on cards | Only `payment.captured` succeeds; failures dropped unless another event is mapped |

Pay must never claim “we do 3DS.” Copy: “Card payments use your processor’s 3-D Secure.”

---

## Our adapters

### Port

`IPaymentGatewayAdapter` (`Application/Ports/IPaymentGatewayAdapter.cs`):

| Method | Job |
|--------|-----|
| `GenerateCheckoutAsync` | Hosted URL + provider session id |
| `ParseWebhookAsync` | Verify + normalize to `GatewayWebhookParsedResult` |
| `IssueRefundAsync(apiKey, transactionId, amount)` | Full or partial (amount in **major** units) |
| `GenerateCustomerPortalAsync` | Billing portal URL |
| `ChargeOffSessionAsync` | Vaulted recurring / dunning |

**Capability-blind.** Unsupported methods `throw` or `return false`. There is no `SupportsOffSession` / `SupportsPartialRefund` / `SupportsDisputes` / `SupportedRails[]`. Callers hardcode Stripe for portal. Ops UI warns Billplz cannot vault (good honesty). Reserved `LP-PAY-018` is this matrix.

**Fee args on `ParseWebhookAsync` are dead.** Handler always passes `0,0,0` after `RemoveAccountingOverrides`. Billplz still *computes* `paid * pct + fixed` but the inputs are zero → **GatewayFee always 0** for Billplz (`LP-PAY-011`).

**No `CancellationToken`.** Long Stripe expands sit on the webhook request thread.

**Money is `decimal` major units + `* 100`.** `GatewayCommon` has rounded vs truncating minor-unit helpers. Stripe adapter still uses `amount * 100` / `AmountReceived / 100m` and will mis-charge **zero-decimal** currencies (JPY/KRW) if anyone sends them. `CheckoutAmountRules` only special-cases MYR minimum RM 2.00 vs 0.50 else.

**LineItems** on `GatewayPaymentCompletedIntegrationEvent` are always `new List<LineItemDto>()` from the webhook handler.

Registered in `PaymentGatewayFactory` + DI: **STRIPE, BILLPLZ, CHIP, RAZORPAY**. M2M allow-list is the same four (`CreateIntegrationCheckoutCommandHandler.AllowedGateways`). Ops UI labels: CHIP Collect (Malaysia), Billplz (Malaysia), Stripe (Global), Razorpay (Global). Unknown `{gatewayType}` on Hop A → **400**, not 500.

README still says “Stripe, Billplz, FPX, Curlec.” **Curlec is not an adapter.** Curlec is Razorpay MY marketing (file 05). FPX is not an adapter.

### Stripe (`StripeGatewayAdapter`) — `LP-PAY-001`

**Checkout**

- Stripe.net `SessionService`, `Mode = "payment"` (not Stripe Billing subscriptions, not `setup` mode).
- Line item `PriceData.UnitAmountDecimal = amount * 100`, quantity separate.
- Metadata on Session **and** `PaymentIntentData.Metadata` (so `payment_intent.succeeded` still carries `checkout_id`).
- `setupFutureUsage` → `PaymentIntent.SetupFutureUsage = off_session`.
- Does **not** set `PaymentMethodTypes` — Dashboard decides cards / FPX / GrabPay / wallets.
- Does **not** auto-register webhook endpoints (manual `whsec_` in ops).
- `KEY_MODE_MISMATCH` (409) if K1 test/live disagrees with `sk_test_` / `sk_live_` K2. Fixture keys like `sk_test` (no trailing `_`) and Billplz secrets are skipped.

**Webhook**

| Stripe type | Mapped | Notes |
|-------------|--------|-------|
| `checkout.session.completed` | `PAYMENT_COMPLETED` | Expands PI for fee/FX/PM |
| `payment_intent.succeeded` | `PAYMENT_COMPLETED` | Also expands `latest_charge.balance_transaction` |
| `charge.dispute.created` | `DISPUTE_CREATED` | Fetches PI metadata |
| `payment_intent.payment_failed` | **passthrough** | Dropped by handler whitelist — **Stripe hosted failures are invisible unless another path publishes fail** |
| `charge.refunded` / `charge.refund.updated` | passthrough | Dropped |
| `checkout.session.expired` | passthrough | Dropped |
| `radar.*` / `invoice.*` | passthrough | Dropped |

`EventId = stripeEvent.Id` (`evt_…`). `GatewayTransactionId` = PI id (preferred) or session id.

**Fees / FX:** `balance_transaction.Fee`, `ExchangeRate`, settlement currency. Best of the four adapters.

**Refunds:** `RefundCreateOptions { PaymentIntent, Amount = (long)(amount * 100) }`. Accepts pending/succeeded. **No void** (`PaymentIntent.cancel` unused). Partial = smaller amount. Same zero-decimal bug.

**Off-session:** PI confirm `OffSession=true` with Commerce correlation metadata (`type`, `subscription_id`, `tenant_id`, `receipt`, optional `dunning_campaign_id`). 3DS-required instruments fail → handler `GatewayPaymentFailed`.

**Portal:** list customers by **email**, first hit, Billing Portal session. Ambiguous for guests / duplicates. Does not use `VaultedCustomerId`.

**Disputes:** only `created`. No `funds_withdrawn` / `closed` / won-lost.

### Billplz (`BillplzGatewayAdapter`) — `LP-PAY-002`

**Checkout**

- `POST /api/v3/bills` with Basic auth `apiKey:`.
- Requires `MerchantId` (collection id).
- Amount: `GatewayCommon.ToMinorUnitsTruncating` (sen).
- `callback_url` = `{publicBase}/webhooks/payments/billplz/{tenantId}?type=&reference_1=&checkout_id=`.
- Also sets `reference_1` / `reference_2` (stripped from S2S body — ADR 009).
- `redirect_url` = success URL (browser only).
- **No** `payment_methods` filter — collection defaults apply (FPX, wallets, cards, BNPL, maybe PayPal).
- Sandbox vs live: `BillplzPublicBase.IsProductionApi` (`App:BillplzEnvironment` or host allow-list). **Not** K1 prefix. Documented honesty gap vs `sk_live_` (quickstart §8.3).
- Public HTTPS callback required (`CALLBACK_BASE_NOT_PUBLIC` if localhost / `lazuar-local-dev.com`).

**Webhook**

- Form body, HMAC-SHA256 over sorted `key+value` excluding `x_signature`, dual-try with/without extra fields (`paid_at`, `transaction_id`, `transaction_status`).
- `paid=true` or `state=paid` → `PAYMENT_COMPLETED`; else `PAYMENT_FAILED` (**now published**).
- `EventId = billId`. Currency hard-coded **MYR**.
- Metadata reconstruct: form refs, else `Query-*`, plus `Query-checkout_id`.
- Query params are **not** in the HMAC. ADR 009 accepts this. Session merge-by-bill-id is the safety net.

**Refunds:** `IssueRefundAsync` → `false` always. Billplz has **no** “refund this bill id” in the adapter. Their **Payment Order** product is a *disbursement to a bank account* (payroll / supplier / “customer refunds” as a new payout), not a card-network reversal. Pay must not pretend Payment Order is `IssueRefundAsync`.

**Off-session / portal:** `NotSupportedException` / `InvalidOperationException`. Ops UI amber banner is correct: “Offline / hosted checkout only.”

**Fees:** estimation formula present, inputs zeroed → fee 0. Ledger net = gross for Billplz (`LP-PAY-011`).

### CHIP Collect (`ChipCollectGatewayAdapter`) — `LP-PAY-003`

**Checkout**

- `POST https://gate.chip-in.asia/api/v1/purchases/` Bearer key.
- Requires `MerchantId` (brand id).
- Amount: `ToMinorUnitsRounded`.
- Metadata under `purchase.metadata` (survives webhook).
- `force_recurring` + `skip_capture` when `setupFutureUsage` and amount 0.
- **No per-purchase callback URL** — account webhook registered at config save.
- Config save: fetch RSA public key → store as `WebhookSecret`; register events `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`. Re-save with new key may **duplicate** webhooks (no list-before-create).

**Webhook**

- `X-Signature` RSA-SHA256 PKCS1 over raw body with PEM public key.
- `purchase.paid` → completed; `purchase.payment_failure` → failed; **`purchase.preauthorized` not paid** (fixed vs gap 02).
- `payment.refunded` registered but **unmapped** → dropped.
- `EventId = root.id`, fallback **`Guid.NewGuid()`** — still unsafe if CHIP omits id (rarer than old Razorpay path, still wrong).
- `GatewayCustomerId` **always null**. Token = purchase id if `is_recurring_token`.
- Fees from `payment.fee_amount` / `net_amount` when present.

**Refunds:** `POST purchases/{id}/refund/` with optional `{ amount }` in sen. Empty body = full. **API yes; webhook no** → async/dashboard refunds never complete domain state unless the API path already emitted `GatewayRefundCompleted`.

**Off-session:** GET old purchase → new purchase with Commerce metadata → `POST …/charge/` `{ recurring_token }`. Status `paid` or `pending_charge`.

**Portal:** throws.

**CHIP product Pay does not wrap:** CHIP mini (in-person DuitNow), CHIP Send (payouts), CHIP Expense, CHIP Advance (capital), settlement reports, stablecoins as a first-class rail. File 05: those are rival *products*, not adapter tickets.

### Razorpay (`RazorpayGatewayAdapter`) — `LP-PAY-004`

**Checkout**

- API key stored as `keyId:keySecret`.
- One-off: Payment Link. `setupFutureUsage`: Invoice **registration link** (card mandate, `max_amount = amountPaise * 10`, 10-year expiry).
- Notes = metadata. `callback_url` is **browser success**, not Pay webhook — dashboard webhook required.
- Phone default `+60100000000` if missing — MY-shaped default on an IN processor.

**Webhook**

- `X-Razorpay-Signature` via official `Utils.verifyWebhookSignature`.
- Only `payment.captured` → completed.
- EventId: header then payment id; **never** new Guid (fixed).
- Fees/tax from payment entity. Currency from entity, default `"MYR"` if absent (wrong for INR merchants).
- No `payment.failed`, no refund events, no disputes.

**Refunds:** `Payment.Fetch(id).Refund(amount>0 ? {amount} : null)`. Partial supported.

**Off-session:** Order + `Payment.CreateRecurringPayment` with notes correlation. Email/contact **hard-coded** `billing@lazuar.com` / `0000000000`.

**Portal:** throws.

Do not market Razorpay to MY merchants as an FPX replacement. Keep the adapter so IN/global tenants and engineering spikes still work.

### Factory, cashier, M2M session

`CheckoutSessionCashier.ResolveGatewayNameAsync`: preferred → first **active** config → legacy `"BILLPLZ"` only when `requireActiveGateway=false` (Commerce string query). M2M requires an active gateway (`PAYMENTS_NOT_CONFIGURED`).

`CreateIntegrationCheckoutCommandHandler`:

- Validates amount/currency/URLs/email/description.
- Idempotency-Key + request fingerprint → replay or `IDEMPOTENCY_CONFLICT` 409.
- Persist session **before** provider call; unique `(OrganizationId, IdempotencyKey)`.
- Stamps `checkout_id`, `hub_workspace_id`, `hub_checkout_kind`, `tenant_id`.
- 24h TTL; lazy expire on get.

This is the **pending-session safety net** ADR 009 said Pay would never have. README is stale. File 14 owns the integrator contract; this file owns the money semantics.

### Config / secrets / ops UI — `LP-PAY-012`

- One row per `(OrganizationId, GatewayType)`.
- Encrypted `ApiKey`, `WebhookSecret`; `MerchantId` plaintext (collection/brand).
- `IsActive` soft-disable (webhooks + refunds still run).
- Stripe: `SecretKey` form field maps to `ApiKey`.
- Razorpay: concatenated secret.
- CHIP: auto RSA + webhook register.
- Ops: `PaymentSettingsPage.tsx` — four gateways, Billplz 128-char x-signature hint, vault warning.

**No method-level UI.** No “enable FPX / DuitNow / TnG.” No capability discovery on `GET /integrations/payments/me` beyond `has_active_gateway` + `gateway_names`.

### Adapter capability matrix (Pay code, not marketing)

| Capability | Stripe | Billplz | CHIP | Razorpay |
|------------|--------|---------|------|----------|
| Hosted checkout | Y | Y | Y | Y |
| Metadata round-trip in body | Y | **N** (query + session merge) | Y | Y (notes) |
| Signature verify | Stripe-Signature + timestamp | HMAC form, no timestamp | RSA PEM | HMAC header |
| Exact fees | Y (BT) | **N** (0) | Y if present | Y |
| FX rate | Y | 1 / MYR | 1 | 1 |
| Currency | Caller ISO | **Forced MYR** | From purchase | From entity (default MYR) |
| Method selection (FPX/QR/wallet) | Dashboard implicit | Collection implicit | Brand implicit | Account implicit |
| 3DS | Processor | Processor | Processor | Processor |
| `setup_future_usage` / vault | Y | **N** | Y | Y (registration link) |
| Off-session charge | Y | throws | Y | Y (placeholder PII) |
| Customer portal | Y (email) | throws | throws | throws |
| Refund API | Y partial | **false** | Y partial | Y partial |
| Refund webhook | **N** | N/A | Registered, **not parsed** | **N** |
| Void / cancel uncaptured | **N** | N | skip_capture only | N |
| Dispute webhook | `created` only | N | N | N |
| Payment failed mapped | **Not from Stripe events** | Unpaid callback | `payment_failure` | N |
| Auto webhook provision | N | N | **Y** | N |
| Sandbox switch | key prefix | env/host | single host | key |
| Apple/Google Pay | implicit | implicit | implicit | implicit |
| BNPL | N | implicit | implicit | IN EMI implicit |
| Settlement report API | N (use Stripe) | N | N (use CHIP dashboard) | N |

---

## Refunds/disputes

### Refund path (live) — `LP-PAY-009`

```text
Ops  POST /admin/commerce/transactions/{id}/refund
     body: { amount?, gateway_name?, subscription_id?, tax_amount? }
  → RecordRefundCommand
       load TransactionLog; reject already REFUNDED / missing ExternalReference
       amount default = log.Amount; reject <=0 or > original
       currency from log or MYR
       gateway_name default **STRIPE** if omitted  ← footgun for Billplz orgs
  → GatewayRefundRequestedIntegrationEvent (real amount)
  → Payments handler
       config may be soft-disabled (historical obligations still refundable)
       Amount <= 0 → GatewayRefundFailed
       adapter.IssueRefundAsync(key, GatewayTransactionId, Amount)
       success → GatewayRefundCompleted (RefundedFee = 0 always)
       fail → GatewayRefundFailed
  → Commerce: match log by ExternalReference == GatewayTransactionId
               OR ExternalReference == PaymentRecordId
               OR Id == PaymentRecordId
               → TransitionToRefunded()   (full status flip even if partial amount)
  → Billing: ledger reverse if RefundedAmount > 0; tax scaled from original GATEWAY_PAYMENT
  → LHDN: cancel <72h / credit note ≥72h keyed by PaymentRecordId
```

**What is true now (contra stale 06):**

- Publisher exists (`RecordRefundCommand`).
- Amounts are real.
- Billing will post non-zero refunds.
- Commerce match includes gateway transaction id.

**What is still broken or thin:**

| Gap | Evidence | Competitor bar |
|-----|----------|----------------|
| **Billplz always false** | `IssueRefundAsync` → `false` → `GatewayRefundFailed` | Stripe/CHIP/HitPay in-product refund; Billplz dashboard / Payment Order |
| **No M2M refund API** | TypeSpec payments: create + get checkout + `/me` only. Docs: `payment.refunded` “maturing” | Stripe/PayPal/HitPay refund-by-id |
| **No inbound refund webhooks** | Stripe `charge.refunded`, CHIP `payment.refunded`, Razorpay refund.* dropped | Async refunds / dashboard refunds never close Pay session |
| **No outbound `payment.refunded`** | `IntegrationCheckoutGatewayEventsHandler` only completed/failed | Integrators historically 422 unknown types |
| **`GatewayRefundFailed` has zero subscribers** | Grep: only the publisher | Ops blind; subscription/order still “paid” |
| **Partial refund flips Commerce to REFUNDED entirely** | `TransitionToRefunded()` no remaining-amount | Stripe keeps partial state |
| **Default gateway STRIPE** | `RecordRefundCommandHandler` | Billplz merchant refunds fail unless UI sends `gateway_name` |
| **RefundedFee always 0** | Handler comment | Stripe refunds often return MDR policy; CHIP may return fee |
| **No void** | No `PaymentIntent.cancel`, no CHIP uncapture, no Billplz delete-unpaid-bill productization | Card voids before capture; CHIP `skip_capture` exists but unused as void |
| **Idempotency of refund request** | Re-POST before completion can double-call gateway | Stripe idempotency keys |
| **LHDN key = PaymentRecordId** | Commerce sends log.Id — better than old design — but M2M checkouts have no Commerce log | Tax refunds only for Commerce-billed flows |
| **No refund of M2M cashier sessions** | Integration checkout has no refund command | Integrators cannot reverse via Hub |

### Voids vs refunds

| Action | When | Money | Pay |
|--------|------|-------|-----|
| **Void** | Auth hold not captured | Never settled | **N**. CHIP `purchase.preauthorized` is ignored (good) but there is no “cancel the hold” API from Pay. Stripe uncaptured PI cancel unused. |
| **Refund** | After capture | Reverses settlement | **P** as above |
| **Billplz delete/abandon unpaid bill** | Buyer never paid | None | Not exposed. Unpaid webhook → `payment.failed` for M2M session if bill id matches |

Do not teach merchants “void” on FPX. There is nothing to void. Unpaid FPX is just unpaid.

### Disputes / chargebacks — `LP-PAY-010`

**Inbound:** Stripe `charge.dispute.created` only → `GatewayDisputeCreatedIntegrationEvent`.

**Consumer:** Billing `ChargebackClawbackHandler` **only** if `metadata.type == utility_credit_topup`. Claws platform credits + reverses `SYSTEM_CREDIT_TOPUP` ledger. **Does not** suspend Commerce subscriptions, reverse GMV revenue, or emit outbound `payment.disputed`.

**Missing lifecycle:** `charge.dispute.updated` / `closed` / won / lost / `charge.dispute.funds_withdrawn`. No evidence upload. No representment UI. Schema seed: `LP-PAY-010` **absent**, Wave 0, `must-my` — because a card chargeback on a live Commerce subscription is a money-loop hole versus Stripe Billing.

**Billplz / CHIP / Razorpay:** no dispute mapping. FPX/DuitNow/wallets have **no card-network chargeback**. Fraud on those rails is a PayNet/bank complaint, not `charge.dispute.created`. Product must not show a “chargebacks” queue on a Billplz-only workspace.

**BNPL disputes** sit at Atome/Grab, not Pay.

### Payouts / split / marketplace (Connect) — refuse

| Temptation | Who has it | Why Pay must not |
|------------|------------|------------------|
| Stripe Connect Express / destination charges / `application_fee` | Marketplaces, Treatwell-shaped products | Payfac liability; GMV take-rate 0%; ADR 019 |
| CHIP Send | CHIP Control product | Payouts to vendors/affiliates. Different KYC. ADR 020 Phase 3 “Wise MassPay” is the same trap (`LP-XX-010`). |
| Billplz Payment Order | Billplz disbursement | Bank credit to suppliers. Using it as “refund” without matching the original instrument is a **new payment**, not a refund. |
| Razorpay Route / X | IN marketplaces | Wrong geography + marketplace shape |
| Affiliate mass payout | ADR 020 §9 | Phase 3; not cashier MVP |
| Split tender online (FPX+TnG on one Pay session) | Some POS | Cannot split one Billplz bill from Pay |

Pay may **link out** “refund this in Billplz dashboard” or “payouts live in CHIP Send.” It must not implement Connect (`LP-XX-007`).

### Multi-currency / FX

| Layer | Fact |
|-------|------|
| M2M `currency` | ISO-4217, length 3. Min RM 2 / else 0.50 |
| Stripe | Passes currency through; records `FxRate` + `BaseCurrency` from BT |
| Billplz | Ignores caller currency; MYR |
| CHIP | Uses purchase currency field |
| Razorpay | Uses payment currency; default string MYR |
| Pay FX product | **None** — no quote, no markup, no multi-currency settlement report |
| LHDN export / zero-rate | Billing/LHDN job, not Payments (ADR 021 Pillar 3) |

A USD Stripe charge into a MY workspace will fulfill. Billplz will still create a MYR bill if someone passes `USD` — **silent currency lie**. Cashier should reject non-MYR for Billplz (not done). Reserved commerce `LP-COM-015` is multi-currency checkout; this chapter’s job is **Billplz must not lie about currency**.

### Saved cards / tokenization / off-session — `LP-PAY-008`

| Gateway | How vault works | Pay flag | Recurring |
|---------|-----------------|----------|-----------|
| Stripe | Customer + PaymentMethod; Checkout `setup_future_usage=off_session` | `setup_future_usage` on M2M / Commerce interval ≠ one_time | `ChargeOffSessionAsync` |
| CHIP | Recurring token = original purchase id | `force_recurring` | Charge token |
| Razorpay | Registration link + `token_id` | registration link | `CreateRecurringPayment` |
| Billplz | **Impossible** | UI warns | throws |

M2M TypeSpec: `setup_future_usage?: boolean`. Integrators can request vault. Pay does not return `vault: false` as a **capability** on `/me` — only `gateway_names`. A Billplz workspace will accept `setup_future_usage=true` and the adapter will **ignore** it (parameter unused except Stripe/CHIP/Razorpay). Billplz still creates a normal bill. **Silent no-op.** Should 422 `VAULT_NOT_SUPPORTED`.

Customer portal is Stripe-only, email lookup (`GenerateCustomerPortalQueryHandler` forces `STRIPE`). No Pay-hosted “manage cards” for CHIP/Razorpay. File 11 owns portal product depth (`LP-COM-010`).

Off-session is a **Commerce / dunning** job (`ExecuteOffSessionChargeIntegrationEvent`), not an M2M public API. Correct: do not let integrators silently auto-renew on Billplz. File 12 owns the recovery loop.

### Settlement reports

Competitors (CHIP, Stripe, Razorpay, HitPay, Billplz dashboard) show T+N payout files: gross, MDR, net, payout id, bank arrival.

Pay has:

- Per-payment `GatewayFee` / `NetAmount` on the completed event (0 for Billplz).
- Billing double-entry per payment / refund.
- **No** settlement batch entity, **no** payout webhook (`payout.paid`), **no** CSV of “what landed in Maybank yesterday,” **no** reconcile-against-processor report.

Reconciliation today = sum of `GatewayPaymentCompleted` vs the processor dashboard. That is a **support burden**, not a product.

Later (not Wave 0): optional import or Stripe `payout.*` / CHIP settlement export. Do not build a general ledger inside Payments (README forbids it). A **read model** of processor payouts is allowed. Propose parent-mint `LP-PAY-019` if `00-evaluation` wants the row.

---

## Webhook integrity

Two hops. Two signatures. Two idempotency problems. File 14 owns integrator DX (keys, envelope, docs). This file owns **money correctness** of both hops.

### Hop A — provider → Pay — `LP-PAY-005` / `LP-PAY-006` / `LP-PAY-007`

**Route:** `POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}`  
Allow-list: stripe, billplz, razorpay, chip. Else 400.

**Pipeline (`ProcessGatewayWebhookCommandHandler`):**

1. Load config by tenant + gateway (IgnoreQueryFilters). Require webhook secret. Soft-disable still processes.
2. Decrypt secrets. `ParseWebhookAsync(..., 0,0,0)`.
3. Unverified → `InvalidOperationException` → typically **400**. Providers may **stop retrying**. Mis-set secret = permanent non-fulfillment until human replay (no admin redrive).
4. EventType not in `{PAYMENT_COMPLETED, DISPUTE_CREATED, PAYMENT_FAILED}` → **200 silent**.
5. `HasBeenProcessed(EventId, Provider)` → 200.
6. `HasBusinessKeyBeenProcessed(EventType:GatewayTransactionId)` → 200 (Stripe dual-event guard).
7. Merge `IntegrationCheckoutSession` metadata by `ProviderSessionId == GatewayTransactionId`.
8. Insert `PaymentWebhookLog`, publish integration event, `SaveChanges`. Unique violation → 200.

**What is good**

- Per-tenant BYOK secrets.
- Signature verify before side effects (all four).
- Transactional outbox write with the log.
- Business-key idempotency (`LP-PAY-006`).
- Session merge for Billplz strip.
- `PAYMENT_FAILED` now a first-class internal event (`LP-PAY-007`).
- Metrics: `LazuarMetrics.RecordWebhookFailed("payment")` on unexpected throw.
- Success log includes EventId, Provider, GatewayTransactionId, TenantId, EventType, CheckoutId.

**What is still weak**

| Issue | Detail | Tracker |
|-------|--------|---------|
| **No raw body archive** | Log is EventId + Provider + BusinessKey + ProcessedAt. Cannot replay, cannot forensic. | `LP-PAY-016` reserved |
| **No status enum** | Ignored types leave no row. | same |
| **400 on bad signature** | After secret rotation, gateways give up. Need admin replay + store-first. | `LP-PAY-017` reserved |
| **Sync Stripe expand** | Webhook latency + fail-closed if `ConstructEvent` fails. Fee expand is try/catch. | residual |
| **Billplz EventId = bill id** | Fine for paid-once bills. Failed then paid: different EventTypes, same bill id, **different business keys** (`PAYMENT_FAILED:bill` vs `PAYMENT_COMPLETED:bill`) — correct. | ok |
| **CHIP EventId fallback Guid** | Residual critical if id missing. | propose `LP-PAY-020` |
| **Stripe failures unmapped** | Hosted 3DS abandon / `payment_intent.payment_failed` never become outbound `payment.failed`. | propose `LP-PAY-021` (parent mint; do not overload `LP-PAY-007` which is “published into Commerce”) |
| **Tenant in URL** | Org id is path, not signed payload. Security = per-tenant webhook secret. | accepted BYOK |
| **Query metadata unsigned** | ADR 009; mitigated by session table. | accepted |
| **No IP allowlist / rate limit** | Public route. | DEV residual (file 14) |
| **Fee 0 for Billplz** | Economics wrong, not integrity. | `LP-PAY-011` |
| **`purchase.preauthorized` dropped** | Correct for money; CHIP still registers it (noise). | ok |

**Outbox (post-ACK reliability)**

Historical gap “always mark processed” is **fixed**: max 5, exponential minutes, then `Dead` + metric. Residual: Dead letters need **human redrive UI**. None in ops. A Dead `GatewayPaymentCompleted` after HTTP 200 to Billplz is still a silent non-fulfillment until someone runs SQL. That is `LP-OPS-005` (support timeline) more than a new PAY row.

### Hop B — Pay → integrator

`IntegrationCheckoutGatewayEventsHandler`:

- Resolve session by `metadata.checkout_id` or `ProviderSessionId`.
- Only `Status == open` emits outbound (idempotent).
- `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl: null` → One module fan-out.
- Events: **`payment.completed`**, **`payment.failed`** only.

Outbound signing (One dispatcher, documented in file 14):

- `X-Lazuar-Signature: t=<unix>,v1=<hex>` HMAC-SHA256 of `"{t}.{body}"`.
- `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`.
- Envelope `{ id, event_type, created_at, data }` (not the flat object TypeSpec `PaymentWebhookPayloadDto` still sketches — **runtime honesty**: nested `data`).
- Retries: One outbox, exponential, max 5 → FAILED.
- Same-URL webhook register is idempotent; rotate-secret exists; `whsec` encrypted.

**Not Svix-complete:** no public delivery log at Stripe Workbench quality (file 14 residuals), no `payment.refunded` / `payment.disputed`.

Do **not** emit `payment.refunded` into integrators that 422 unknown types until the contract is versioned and documented.

### Replay and reconciliation

| Tool | Exists? |
|------|---------|
| Provider retry | Yes (if we 5xx). 4xx stops Stripe/Billplz. |
| Pay admin replay by EventId | **No** (`LP-PAY-017`) |
| Raw payload store | **No** (`LP-PAY-016`) |
| GET checkout as money signal | **Explicitly not** — poll is UX/recon aid |
| Billing ledger dedupe | `GATEWAY_PAYMENT` + GatewayTransactionId |
| M2M session status | open/completed/failed/expired — good recon object |
| Settlement file vs ledger | **N** |
| Idempotency-Key on create | Yes |
| Signature fixtures in CI | Partial (Billplz HMAC + handler tests; not live vectors for all four) |

### Comparison to industry (Stripe / Svix) — updated 16 Aug 2026

| Best practice | Pay |
|---------------|-----|
| Verify signatures | Y |
| 2xx quickly; async heavy work | P (sync parse + Stripe expand) |
| Idempotent processing | P (event + business key; no payload store) |
| Store event payloads | N |
| Ignore unknown events with 200 | Y |
| Retry-safe outbox | Y (DLQ exists) |
| Replay from dashboard | N |
| Versioned outbound + timestamp | Y (`t=,v1=`) |
| Explicit event catalog | P (`completed`/`failed`; refunds maturing) |
| Tests | P |
| Separate received vs processed | N |
| Dead-letter + alert | P (Dead + metrics; no pager/UI) |

---

## Gap table

Legend: **Y** implemented in Pay or honestly inherited from hosted K2 and fulfilled; **P** partial; **N** no; **R** refuse.

Competitor columns here are **processors / CaaS**, not salon OS.

### Rails

| Capability | Stripe | Billplz | CHIP | Razorpay | HitPay | Pay (ours) | Seed / later |
|------------|--------|---------|------|----------|--------|------------|--------------|
| Cards Visa/MC | Y | Y | Y | Y | Y | **P** hosted | `LP-PAY-001` shipped *as Stripe path*; Billplz/CHIP implicit |
| Amex | Y | P | P | P | P | **P** | Leave at K2 |
| 3DS | Y | Y | Y | Y | Y | **P** processor | Never claim Pay-3DS |
| FPX retail | Y (dear) | Y | Y | N | Y | **P** hosted | `LP-PAY-002` partial |
| FPX corporate | N | Y | P | N | P | **P** hidden | Do not build B2B1 selector unless B2B ICP |
| DuitNow QR | N | Y | Y | N | Y | **P** hosted | reserved `LP-PAY-013` |
| DuitNow OBW | N | P | P | N | Y | **N** | Collapse into hosted |
| TnG / Boost / GrabPay / ShopeePay | GrabPay only | Y | Y | N | Y | **P** hosted | Honesty copy, not adapters |
| MAE as named rail | N | via QR | via QR | N | via QR | **N** | Do not add MAE adapter |
| Apple Pay | Y | P | P | P | P | **P** implicit Stripe | inside `LP-PAY-001` |
| Google Pay | Y | P | P | P | P | **P** implicit Stripe | same |
| BNPL Atome/Grab/SPay | N | Y | Y | IN EMI | Y | **P** hidden | Document one-capture; no BNPL ledger |
| PayPal adapter | P | P | N | N | P | **N** | Prefer Billplz `isupaypal` |
| Crypto / USDC | N | N | Y marketed | N | N | **R** | `LP-XX-010` |
| PayNow / SGD | SG | N | N | N | Y | **R** MY v1 | Until SG SKU |
| UPI marketing on MY | N | N | N | Y | N | **R** | `LP-PAY-004` keep, do not MY-market |
| Fiuu / SenangPay adapter | — | — | — | — | — | **N** | reserved `LP-PAY-014` / `015` Wave 4 |
| Method selector API | Y | Collection API | Brand | Dashboard | Y | **N** | `LP-PAY-018` |
| Rails logos on checkout | Y | Y | Y | Y | Y | **N** | File 09 / `LP-UX-*` |

### Money movement after capture

| Capability | Stripe | Billplz | CHIP | Razorpay | Pay | Seed / later |
|------------|--------|---------|------|----------|-----|--------------|
| Full refund API | Y | Dashboard / PO | Y | Y | **P** (3 adapters; Billplz false) | `LP-PAY-009` |
| Partial refund | Y | Dashboard | Y | Y | **P** amount accepted; Commerce status binary | same |
| Void / uncapture | Y | N/A | preauth | P | **N** | Only if auth-hold product |
| Refund webhook inbound | Y | N | registered | Y | **N** | propose `LP-PAY-022` |
| Outbound `payment.refunded` | — | — | — | — | **N** (docs: maturing) | same + file 14 |
| M2M refund endpoint | Y | N | Y | Y | **N** | after Commerce path honest |
| Dispute created | Y | N | N | Y | **P** Stripe + utility clawback | `LP-PAY-010` absent for GMV |
| Dispute won/lost | Y | N | N | Y | **N** | child of `LP-PAY-010` if split |
| Settlement report | Y | Y dash | Y | Y | **N** | propose `LP-PAY-019` |
| Payouts / split / Connect | Connect | Payment Order | CHIP Send | Route | **R** | `LP-XX-007` / `010` |
| Multi-currency | Y | MYR | P | Y | **P** field only; Billplz lies | reject non-MYR on Billplz |
| FX markup | Y | N | N | P | **N** | Never (not a dealer) |
| Saved card / token | Y | N | Y | Y | **P** | `LP-PAY-008`; 422 if Billplz + setup_future_usage |
| Off-session charge | Y | N | Y | Y | **P** Commerce only | `LP-PAY-008` |
| Customer portal | Y | N | N | N | **P** Stripe email | `LP-COM-010` |
| Idempotent webhooks | Y | bill id | P | Y | **P** | `LP-PAY-005/006` shipped slice |
| Replay / recon UI | Y | dash | dash | dash | **N** | `LP-PAY-017` |
| Failed-pay event (Stripe hosted) | Y | unpaid | Y | P | **P** (Billplz/CHIP/off-session; **not** Stripe PI failed) | propose `LP-PAY-021` |
| Encrypted secrets + soft-disable | P | P | P | P | **Y** | `LP-PAY-012` shipped |
| Fee fidelity | Y | dash | Y | Y | **P** Billplz 0 | `LP-PAY-011` |

### Company-shape traps (do not “gap close”)

| Trap | Competitor who profits | Pay verdict | ID |
|------|------------------------|-------------|-----|
| Marketplace take-rate / Discover | Fresha, Treatwell, Lemon-shaped | **R** | `LP-XX-007` |
| Pay as MoR of buyer GMV | Paddle, Lemon, Polar | **R** | file 07 / 19 |
| Stripe Connect platform | Stripe docs temptation | **R** | `LP-XX-007` |
| Locked acquirer | Square / Toast / Fresha Payments | **R** | file 19 |
| HitPay / Fiuu adapter inside a *consumer app* | Old Aura stub | **R** in the consumer; optional **Pay K2** later | `LP-PAY-014` |
| Pay-generated DuitNow merchant QR | Informal + CHIP mini | **R** (duplicate settlement) | — |
| Guest self-serve refund | Some e-com / MoR | **R** (chargeback magnet; merchant is MoR) | — |
| Auto-charge no-show on FPX | Card-on-file salon OS | **R** (physics) | file 12 / 19 |
| Mix SaaS fee into tenant Billplz | — | **R** | `LP-XX-012` |
| Crypto checkout in MY v1 | CHIP marketing, Web3 blogs | **R** Phase 3 | `LP-XX-010` |
| Rebuild Billplz Catalog / CHIP mini / POS | Those companies | **R** | file 05 / 19 |

### Residual engineering holes (not competitor features, but they block honesty)

1. Map Stripe `payment_intent.payment_failed` / `checkout.session.expired` → `PAYMENT_FAILED`.
2. Billplz `IssueRefundAsync` stays false — productize **dashboard SOP + mark-refunded**, do not fake Payment Order as card refund.
3. CHIP/Stripe/Razorpay refund webhooks → internal completed + (later) M2M `payment.refunded`.
4. `GatewayRefundFailed` consumer (ops toast / transaction stays PAID with error).
5. Default refund gateway ≠ STRIPE; use the log’s gateway / session.GatewayName.
6. `setup_future_usage` on Billplz → 422 `VAULT_NOT_SUPPORTED`.
7. Billplz non-MYR → 422.
8. CHIP EventId: fail-closed, no Guid.
9. Capability flags on `/me` (`supports_off_session`, `supports_refunds`, `supports_disputes`, `rails: hosted_opaque`) — `LP-PAY-018`.
10. Raw webhook intake table + admin replay — `LP-PAY-016` / `017`.
11. Restore Billplz fee schedule (config or post-fetch) — `LP-PAY-011`.
12. `IPaymentGatewayAdapter` + CT + capability interface.
13. Zero-decimal money helper used by **Stripe** too.
14. README: delete “does not store pending checkouts”; say M2M sessions exist; say encryption is real; delete Curlec-as-adapter.

---

## Tracker IDs

Schema authority: [`20-sequencing-and-tracker-schema.md`](./20-sequencing-and-tracker-schema.md).  
**This chapter does not mint IDs.** It fills cells and recommends promotions. Parent patches `00-checklist-tracker.md`. Next free reserved IDs already named in §PAY: `LP-PAY-013`…`018`. Further IDs are **proposals** for parent to mint (`max+1`).

Money plane for every `LP-PAY-*` row: **G. Merchant GMV** unless noted.

### Existing seed rows (do not renumber)

| ID | Feature | Ours (this chapter) | V | W | P | Class | What 13 changes vs seed |
|----|---------|---------------------|---|--:|--:|-------|-------------------------|
| LP-PAY-001 | BYOK Stripe hosted checkout (cards, Apple/Google Pay via Stripe) | **shipped** as adapter; wallets **implicit** | Both | 1 | 2 | table-stakes | Confirm: no `payment_method_types`; wallets are Dashboard, not Pay |
| LP-PAY-002 | BYOK Billplz hosted bill — FPX / MYR path a merchant can sell | **partial** | Partial | 1 | 0 | must-my | FPX is collection-config, not Pay. Currency forced MYR. Fee 0. Refund false. Sandbox≠K1 prefix |
| LP-PAY-003 | BYOK CHIP Collect hosted checkout + recurring token | **partial** | Partial | 1 | 1 | must-my | Token works; customer id null; refund webhook unmapped; EventId Guid fallback |
| LP-PAY-004 | BYOK Razorpay | **partial** | Later | 4 | 3 | table-stakes* | *Only if IN ICP. Keep adapter. Do not MY-market UPI. Default currency MYR is a bug |
| LP-PAY-005 | Inbound webhook verify, persist, structured process log | **partial → near shipped** | Partial | 0 | 1 | must-my | Verify + log exist; **no raw body / status enum** — do not mark fully shipped until `016` or accept “structured log without payload” |
| LP-PAY-006 | Business-key idempotency | **shipped** | Both | 0 | 0 | must-my | Dual Stripe events covered |
| LP-PAY-007 | Payment-failed published into Commerce | **shipped** for Billplz/CHIP/off-session | Partial | 0 | 0 | must-my | Stripe hosted fail **unmapped** — keep Partial until `021` or map under this row |
| LP-PAY-008 | Off-session / vaulted renewal with metadata | **partial** | Partial | 0 | 1 | must-my | Metadata now present. Billplz throws. `setup_future_usage` silent no-op on Billplz |
| LP-PAY-009 | Full/partial refunds + ledger + tax reverse | **partial** | Partial | 0 | 0 | must-my | Publisher + amounts real. Billplz false. No refund webhooks. Default STRIPE. Binary REFUNDED |
| LP-PAY-010 | Disputes first-class on **commerce GMV** | **absent** | Theirs | 0 | 0 | must-my | Only utility clawback. Seed stands |
| LP-PAY-011 | Gateway fee fidelity | **partial** | Partial | 1 | 0 | must-my | Stripe/CHIP/Razorpay yes; Billplz always 0 |
| LP-PAY-012 | Encrypted secrets + soft-disable | **shipped** | Both | 0 | 1 | table-stakes | Seed stands |

### Reserved IDs (schema already named — promote, do not rename)

| ID | Feature | Ours | V | W | P | Class | Recommendation |
|----|---------|------|---|--:|--:|-------|----------------|
| LP-PAY-013 | DuitNow QR as first-class rail | absent as Pay method; **partial** via hosted | Later | 4 | 2 | must-my | Honesty + optional method lock. **Never** a new adapter or Pay-generated QR |
| LP-PAY-014 | Fiuu adapter | absent | Later | 4 | 3 | later | File 05: rail, not rival OS. After Billplz/CHIP sellable |
| LP-PAY-015 | SenangPay adapter | absent | Later | 4 | 3 | later | Same |
| LP-PAY-016 | Two-phase raw intake vs fulfill | absent | Later | 1 | 2 | must-my | Persist raw → 200 → worker. Unlocks replay |
| LP-PAY-017 | Webhook replay UI | absent | Later | 1 | 2 | must-my | After 4xx secret-fix; ops support |
| LP-PAY-018 | Capability matrix (portal / off-session / refund / vault flags) | absent | Later | 1 | 1 | must-my | Replace try/catch; 422 `VAULT_NOT_SUPPORTED` |

### Proposed next IDs (parent mints; this file only recommends)

| Proposed ID | Feature | Ours | V | W | P | Class | Why it is a new job |
|-------------|---------|------|---|--:|--:|-------|---------------------|
| LP-PAY-019 | Settlement / payout read-model (not a ledger rewrite) | absent | Later | 4 | 3 | later | Competitors sell T+N files; Pay has per-tx fees only |
| LP-PAY-020 | CHIP EventId fail-closed (no Guid) | absent | Later | 1 | 0 | hygiene | Integrity hole leftover from Razorpay fix |
| LP-PAY-021 | Map Stripe `payment_intent.payment_failed` + `checkout.session.expired` | absent | Later | 1 | 0 | must-my | Split from `007` if parent wants `007` to stay “Commerce consumes failed” |
| LP-PAY-022 | Inbound refund webhooks + outbound `payment.refunded` | absent | Later | 1 | 1 | must-my | Split from `009` (API path vs async dashboard refunds) |
| LP-PAY-023 | Billplz refund SOP + mark-refunded (no fake API) | absent | Later | 1 | 0 | must-my | Split from `009` so Stripe/CHIP API refunds can ship independently |
| LP-PAY-024 | Reject non-MYR on Billplz; fix Razorpay default currency | absent | Later | 1 | 2 | hygiene | Currency lie |
| LP-PAY-025 | HitPay as Pay K2 (not a consumer-app adapter) | absent | Later | 4 | 3 | later | File 06 teacher; same port as the four |

If parent refuses to mint, fold `021`–`023` into notes on `LP-PAY-007` / `LP-PAY-009`. Do **not** invent a second family (`RL-*`).

### Refuse rows already in schema (cite, do not duplicate)

| ID | Why this chapter cares |
|----|------------------------|
| LP-XX-007 | Marketplace / take-rate / Connect |
| LP-XX-010 | Affiliate mass-payouts / BNPL-as-Pay / Web3 settlement |
| LP-XX-012 | Pro plan billed through tenant Billplz |

### Wave mapping (align with schema §waves)

| Wave | PAY jobs this chapter cares about | Why |
|------|-----------------------------------|-----|
| 0 | `005`–`010`, `012`; residuals `020`, `021`, `023` | Money loop: fail, refund, dispute GMV, no Guid EventId |
| 1 | `001`–`003`, `011`, `016`–`018`, `022`, `024` | Sellable MY rails + honesty + intake/replay + fees |
| 4 | `004`, `013`–`015`, `019`, `025` | Extra processors / DuitNow-as-named / settlement |
| Never | `LP-XX-007/010/012`; Pay-native QR; guest self-refund; MAE adapter; PayNow-in-MY | Traps |

### Promotion rule

A cell in `00-checklist-tracker.md` may change only with a **path or URL**. `LP-PAY-009` is not `shipped` while Billplz `IssueRefundAsync` returns false. `LP-PAY-002` is not `shipped` because Pay does not implement FPX — it is **partial** (hosted). `LP-PAY-001` may stay `shipped` as “Stripe Checkout works” without claiming Pay is an Apple Pay company. Never rows do not get a wave.

---

## Appendix A — File index (Pay)

### Domain / application

| Path | Role |
|------|------|
| `Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs` | BYOK + IsActive + encrypted secrets |
| `Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs` | M2M pending session, 24h TTL |
| `Modules/Payments/Domain/Entities/PaymentWebhookLog.cs` | EventId + BusinessKey |
| `Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | Port + DTOs |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler*.cs` | Verify, filter, idempotency, merge, publish |
| `Modules/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` | M2M create |
| `Modules/Payments/Application/Services/CheckoutSessionCashier.cs` | Gateway resolve + KEY_MODE_MISMATCH |
| `Modules/Payments/Application/Services/CheckoutAmountRules.cs` | RM 2 / 0.50 mins |

### Adapters / HTTP

| Path | Role |
|------|------|
| `Infrastructure/Gateways/StripeGatewayAdapter.cs` | Cards/global, disputes, refunds, portal, off-session |
| `Infrastructure/Gateways/BillplzGatewayAdapter.cs` | MY hosted, HMAC, no vault/refund |
| `Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | MY + token + refund API |
| `Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | IN/global links + refunds |
| `Infrastructure/Gateways/BillplzPublicBase.cs` | Prod host allow-list |
| `Infrastructure/Gateways/GatewayCommon.cs` | Minor units |
| `Infrastructure/Endpoints.cs` | Hop A |
| `Infrastructure/IntegrationEndpoints.cs` | M2M |
| `Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | Encrypt + CHIP auto-webhook |
| `Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` | Execute refund |
| `Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Dunning charge + failed event |
| `Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | Hop B enqueue |

### Contracts

| Event | Direction |
|-------|-----------|
| `GatewayPaymentCompletedIntegrationEvent` | Out (webhook) |
| `GatewayPaymentFailedIntegrationEvent` | Out (webhook + off-session fail) |
| `GatewayDisputeCreatedIntegrationEvent` | Out (Stripe) |
| `GatewayRefundRequestedIntegrationEvent` | In (Commerce) |
| `GatewayRefundCompletedIntegrationEvent` | Out |
| `GatewayRefundFailedIntegrationEvent` | Out, **no consumers** |
| `ExecuteOffSessionChargeIntegrationEvent` | In (Commerce jobs) |
| `ApiCreditPurchasedIntegrationEvent` | Dead leftover |

### Downstream

| Path | Role |
|------|------|
| `Modules/Commerce/.../RecordRefundCommandHandler.cs` | Refund publisher |
| `Modules/Commerce/.../TransactionEndpoints.cs` | `POST /transactions/{id}/refund` |
| `Modules/Commerce/.../GatewayRefundCompletedIntegrationEventHandler.cs` | Status REFUNDED |
| `Modules/Billing/.../ChargebackClawbackHandler.cs` | Utility only |
| `Modules/Billing/.../GatewayRefundCompletedHandler.cs` | Ledger reverse |
| `Modules/Lhdn/.../GatewayRefundCompletedIntegrationEventHandler.cs` | Cancel / CN |
| `apps/lazuar-ops/.../PaymentSettingsPage.tsx` | Four-gateway BYOK UI |

---

## Appendix B — Verdict for 00-evaluation

Lazuar Pay is a **credible four-processor BYOK cashier** for **hosted checkout + signed dual-hop webhooks + M2M sessions**. Malaysian rails that matter (FPX, DuitNow, TnG, GrabPay, cards, Atome) already appear on **Billplz/CHIP hosted pages**. Pay’s gap versus HitPay/CHIP/Stripe-the-product is not “add a DuitNow adapter.” It is:

1. **Honesty** — name the rail as the processor’s, map Stripe failures, 422 vault on Billplz, stop STRIPE-default refunds, reject Billplz non-MYR.
2. **Refunds that move money** — API path works for Stripe/CHIP/Razorpay; Billplz needs SOP; inbound/outbound refund events are missing (`LP-PAY-009` stays partial).
3. **Disputes** — Stripe created + utility clawback only (`LP-PAY-010` absent for GMV).
4. **Settlement** — none.
5. **Refuse** Connect, take-rate, Pay-native QR, crypto-now, PayNow-in-MY, guest self-refund, MAE-as-adapter.

File 05 remains the *which Malaysian PG to wrap* authority. File 09 remains checkout UX. File 12 remains dunning. File 14 remains integrator DX. File 19 remains refuse constitution. **This file is the rail-and-after-capture authority.** Do not implement Apple Pay logos to look like Stripe Checkout before `LP-PAY-009` / `LP-PAY-023` Billplz refund honesty and `LP-PAY-010` GMV disputes.
