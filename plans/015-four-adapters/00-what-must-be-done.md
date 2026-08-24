# 00 — What must be done: four adapters, no tax

**Date:** 24 August 2026  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**HEAD:** `ee2db8e5` — `feat(pay): Bar B receipts, webhook secret, merchant money UI`  
**Type:** Evaluation. **Not** an implementation. **Not** a project reference into `apps/lazuar-api`.

---

## 0. The ask, interpreted

> Implement 4 adapters and avoid implementing tax in new Lazuar Pay.

**Four adapters** = the Hub rails **not** on 8081 today: **CHIP Collect, Billplz, Xendit, Razorpay**. Stripe already exists as `StripeHosted` (capability `hosted_link`). After this program the wrap set is five names. If you meant *four total including Stripe*, drop Razorpay (least Malaysian). This paper assumes the remaining four.

**Avoid tax** = do **not** implement SST (registration field, fail-closed throw, exclusive-on-unit × seats, tax journal lines), do **not** implement LHDN / MyInvois / VALID, do **not** title a receipt Tax Invoice. Amount charged is the amount booked. Receipt stays Official Receipt `RCPT-…`.

**Slice for each adapter** = the same two verbs Stripe has today:

1. Create a **hosted** processor URL (`CreateHostedUrl`).
2. Verify a **Plane B** webhook and call existing `Fulfillment.FulfillPaidAsync`.

Out of this program: off-session / vault auto-debit, customer portal, refunds, disputes, fees-as-known-zero, CHIP surprise registrar, `PublicDnsFallback`, MediatR, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`.

---

## 1. Standing law this program does not reverse

| Keep | Meaning |
|------|---------|
| Steal HTTP judgment | Read Hub adapters. Do not copy `Modules/Payments`, MediatR, outbox, `PaymentsDbContext`. IsolationTests stay red on those strings. |
| Same-handler fulfillment | Verified PSP event → journal + `RCPT-` in-process. Rails do not book cash. |
| Wrap-rails honesty | CHIP *can* vault later; this slice still labels it `hosted_link`. Billplz / Xendit / Razorpay **never** silent-debit. `SupportsEmandate` remains false. |
| Setup ≠ paid | Stripe `mode=setup` / amount 0, CHIP `purchase.preauthorized`, unpaid Billplz, non-PAID Xendit, non-`payment.captured` Razorpay → **do not fulfill**. |
| BYOK | Merchant’s keys. Not Connect `application_fee`. Not Lazuar as MoR. |
| Buyers are not One humans | `:5179` stays public. |
| One active rail per org | Four adapters in the **code**. Not four logos on the buyer page. Merchant picks **one** provider. Hub was one `GatewayType` per tenant; keep that. |
| Receipt ≠ tax invoice | Stronger now: we are **not shipping tax at all**. |

**Amend (explicit):** 013/014 “one Malaysian rail, not five” and “SST fail closed.” This folder is the written amendment. Do not silently un-refuse `NP-XX-001` (homemade LHDN) or `NP-XX-011` (FPX e-mandate).

---

## 2. Why you cannot “just add four classes”

Live 8081 is Stripe-shaped and **too thin** for a second rail, let alone four.

| Live fact | Blocks 4 adapters until fixed |
|----------|-------------------------------|
| `PUT /v1/orgs/{orgId}/gateway` 400s anything except `"stripe"` | Allow-list of one |
| `GET` always `FindAsync([orgId, "stripe"])` | Cannot describe CHIP/Billplz rows |
| `PutGatewayRequest` is `{ provider, secret }` | CHIP needs Brand ID + PEM; Billplz needs Collection ID + HMAC secret; all four need a **per-org webhook secret** |
| `Pay:StripeWebhookSecret` is process-wide | Forges every org; CHIP PEM / Billplz X-Signature / Xendit callback token / Razorpay `whsec` are **per merchant** |
| `PublicPayEndpoints.Start` injects `StripeHosted` only | No dispatch |
| `GatewayCredentialRow` = ciphertext + last4 | Missing webhook ciphertext, public merchant id, test\|live |
| `CheckoutRow` has no `Provider` | Billplz strips metadata; start cannot remember which rail |
| `psp_webhook_events` insert **commits before** fulfill | All four rails inherit lost-cash on throw |
| `Fulfillment` throws if `SstRegistered is null` | Tax code you asked **not** to implement; also 500s after the unique insert |
| Checkout create is `RequireMemberAsync` | Member can mint links |
| Checkout SPA email is optional | CHIP / Billplz / Xendit **refuse** placeholder email |

Shared host work is **must-do 0**. The four HTTP extracts are must-do 1–4. Skipping 0 and pasting Hub files into `Gateways/` will fail IsolationTests or recreate Hub’s cashier port.

---

## 3. Must-do 0 — shared host (do this first)

One PR (or stacked commits) **before** CHIP/Billplz/Xendit/Razorpay HTTP. Stripe dogfood must still work after it.

### 3.1 Strip tax from the money path

Live tax residue (all of it, new Pay only):

- `OrgSettingsRow.SstRegistered` (`bool?`) and the column in `org_settings`
- `Fulfillment`: throw `"SST registration unknown; fail closed"`
- `CheckoutEndpoints.Create`: seed `SstRegistered = false` (this was fail-**open** anyway)
- `OneWebhookEndpoints` on insert: `SstRegistered = false`

**Do:**

1. Remove the throw in `Fulfillment`. Book cash debit + revenue credit for `checkout.Amount`. No tax line. No fee line (`unknown ≠ 0`: do not invent 0).
2. Stop reading `SstRegistered` on the pay path. Leave the column in place (do not spend a migration on drop unless you want it). Stop seeding it as a business signal.
3. Do **not** add a merchant SST yes/no field. Do **not** port Hub `SstTaxMath`. Do **not** add LHDN types.
4. Keep title `"Official Receipt"`. Keep `PENDING` if number missing. Keep refuse: never print VALID, never title Tax Invoice.
5. Merchant + checkout copy: amount is GMV as charged; this is not an e-invoice; SST is the merchant’s problem with LHDN later.

Hub adapters take `taxRate` / `taxAmount` on parse and off-session. **Do not port those parameters.**

### 3.2 Credential row that can hold any of the four

Steal Hub `TenantPaymentConfiguration` **fields**, not the type:

| Column | Encrypt? | Who uses it |
|--------|----------|-------------|
| `OrgId` + `Provider` PK | — | all (`stripe` \| `chip` \| `billplz` \| `xendit` \| `razorpay`) |
| `Ciphertext` (API key) | yes | Stripe `sk_`, CHIP Bearer, Billplz secret, Xendit secret, Razorpay `key_id:key_secret` |
| `Last4` | no | GET hint of API key |
| `WebhookCiphertext` | yes | Stripe `whsec_`, CHIP PEM, Billplz X-Signature secret, Xendit callback token, Razorpay webhook secret |
| `PublicMerchantId` | **no** (not a secret) | CHIP Brand ID, Billplz Collection ID. Null for Stripe/Xendit/Razorpay |
| `Environment` | no | Billplz `test`\|`live` (host selection). Others may ignore |
| `UpdatedAt` | no | |

`org_settings.active_provider` (or equivalent): **one** name the org charges with. PUT gateway sets it. Public start uses it. Buyer page does not offer a dropdown of four PSPs.

Move Stripe verify off `Pay:StripeWebhookSecret`. Process env may remain a **dev fallback** for Stripe only, and must 503 in Production if the org row has no `WebhookCiphertext`.

### 3.3 One DB transaction on Plane B

`WebhookEndpoints` today: insert `psp_webhook_events` → `SaveChanges` → `FulfillPaidAsync` (own TX).

**Must:** verify → parse → insert unique → fulfill → **one** commit. Unique hit → 200 `{ duplicate: true }`. Fulfill throw → rollback event id → PSP retry is correct. Bind `checkout.OrgId == path orgId` before fulfill. Match amount when the PSP sent one (minor units) or refuse.

Unhandled event types must **not** consume the unique grain unless you store them as `ignored` and still no-op fulfill. Better: only insert when you will fulfill **or** when you have a stable event id you must never fulfill (setup/preauth) — and test both.

### 3.4 Dispatch without a factory of five

Do **not** add `IPaymentGatewayAdapter` or `PaymentGatewayFactory`.

Do add a **small** hosted-rail shape (when the second class exists), two methods only:

- `string Provider { get; }`
- `Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)`
- Parse stays next to the webhook route **or** `bool TryParsePaid(raw, headers, creds, out PaidWebhook)` — verify + event id + checkout id + provider ref + ignore.

`WebhookEndpoints`: `switch (provider)` on the **allow-list** (`stripe|chip|billplz|xendit|razorpay`). Unknown → 400. A switch of five **known** names is not Hub’s `GetAdapter` over `IEnumerable`. Do not register unused names “for later.”

`PublicPayEndpoints.Start`: load `active_provider` (or `checkout.Provider` if already set). Call that rail. Persist `checkout.Provider` + `PspRedirectUrl` + provider session id (CHIP purchase id, Billplz bill id, Xendit invoice id, Razorpay `plink_`, Stripe `cs_`). Missing email: **400** for CHIP/Billplz/Xendit (Hub `TryResolveEmail`). Stripe may keep optional email.

`GatewayEndpoints.Put`: allow the five names. Writer only. Per-rail required fields (see §5). `Get`: return the **active** rail’s metadata (`provider`, `last4`, `configured`, `capability: "hosted_link"`, `public_merchant_id` if any). Optional `GET ?provider=` for a specific row.

`POST /v1/checkouts`: `RequireWriterAsync` (closes the member-can-charge hole). Store `Provider` only at start, not at create (merchant may switch rails before the buyer pays).

### 3.5 IsolationTests extra greps (same PR)

Ban in `src/`: `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`.

Do **not** add a csproj reference to Hub. CHIP/Billplz/Xendit = `HttpClient`. Razorpay = `HttpClient` to Payment Links (do **not** add `Razorpay.Api` unless HTTP is blocked; Hub’s SDK is gravity, not a requirement). Stripe.net stays.

### 3.6 Still recommended on the Stripe path (not “tax”, still money)

These are not adapters, but four new rails will copy the holes if Stripe still has them:

- Per-org `whsec_` (3.2)
- One TX (3.3)
- `mode=setup` / amount 0 **test**
- Wrap key: no git-known default outside Testing
- `:5179` verifying poll (success URL is not paid)

If you skip these, CHIP RSA will be BYOK while Stripe verify stays a platform secret. That is a worse product than Hub.

---

## 4. Tax: what “avoid” means in files

| Do | Do not |
|----|--------|
| Book `checkout.Amount` as cash/revenue | Compute SST 8% / exclusive-on-unit × seats |
| Title Official Receipt | Title Tax Invoice / print VALID |
| Leave LHDN in Hub museum | Port `Modules/Lhdn` or UBL |
| Ignore Hub `taxRate` / `TaxAmount` on parse | Thread tax through `PaidWebhook` |
| Copy: “not an e-invoice” | Merchant SST registration UI |
| Keep `NP-XX-001` refuse | A “tax later” table that pretends to be LHDN |

Hub `PaymentGatewayCapabilities.SupportsDuitNowQr` is **hosted-page** honesty, not tax. You may restate “wallets appear on the processor page.” You must not render QR/FPX tiles on `:5179`.

Razorpay’s webhook JSON has a `tax` field (processor GST on MDR). **Do not book it.** Same as fees: `unknown ≠ 0`.

---

## 5. Must-do per adapter (HTTP extract only)

All four: empty body **400**, bad signature **400**, missing org creds **400**, unique `(org_id, provider, event_id)`, same `Fulfillment`, capability `"hosted_link"`, writer paste, member GET metadata only.

Provider strings stay **lowercase** in path and PK (`chip`, not `CHIP`).

### 5.1 CHIP Collect (`chip`) — do first of the four

Hub file: `ChipCollectGatewayAdapter.cs` (judgment). Hub also has `ChipWebhookRegistrar` — **refuse silent register**.

| | |
|--|--|
| Create | `POST https://gate.chip-in.asia/api/v1/purchases/` `Authorization: Bearer {sk}` JSON: `brand_id`, client email/name, purchase products `price` in **cents**, `success_redirect` / `failure_redirect` / `cancel_redirect`, metadata `checkout_id` + `org_id`. Return `checkout_url` + purchase `id`. |
| Secrets | API Bearer (ciphertext), **Brand ID** (plaintext), webhook **PEM** (ciphertext). Brand ID required or 400. |
| Verify | Header `X-Signature` base64, `RSA.ImportFromPem`, SHA256 PKCS1. |
| Fulfill | `event_type == purchase.paid` **and** amount > 0. Event id: `paid:{purchaseId}` (namespace; Hub used `{mapped}:{id}`). Checkout id from purchase metadata. |
| Ignore | `purchase.preauthorized` (vault, **not** cash — Hub mapped this to `PAYMENT_COMPLETED` if a token existed; **do not steal the event name**). `purchase.payment_failure`. Refunds. |
| Email | Required on start. |
| Do not | `force_recurring` / `skip_capture` in this slice. Off-session. Registrar `POST /webhooks/` on PUT. |

No extra NuGet.

### 5.2 Billplz (`billplz`)

Hub: `BillplzGatewayAdapter.cs`, `BillplzPublicBase.cs`. `PublicDnsFallback` is Billplz-only folklore — **do not port unless** `www.billplz.com` fails to resolve from the Pay host.

| | |
|--|--|
| Create | `POST {sandbox\|www}.billplz.com/api/v3/bills` Basic `{secret}:`. JSON: `collection_id`, email, name, `amount` cents, `callback_url`, `redirect_url`, `reference_1` = **checkout id**. Return `url` + bill `id`. |
| Callback URL | `https://{public-pay}/v1/webhooks/billplz/{orgId}?checkout_id={id}`. Billplz **strips body metadata**. Query `checkout_id` is the join. Local dogfood **needs a public HTTPS tunnel**. Fail create if callback base is `localhost`. Steal that fail-closed from `BillplzPublicBase`, not the DNS rewriter. |
| Secrets | API secret, Collection ID, **X-Signature secret** (often same as API or a collection signature key — store separately). `Environment` test\|live picks host. |
| Verify | Form body (not JSON). Field `x_signature`. HMAC over sorted fields. Hub tries with-extra then without-extra. Steal that. |
| Fulfill | `paid=true` or `state=paid`. Event id: `paid:{billId}`. Checkout id from query `checkout_id` then `reference_1`. |
| Ignore | unpaid callbacks (verified but not paid). |
| Email | Required. |
| Do not | `ChargeOffSession` (Hub returns false). `IssueRefund` (Hub returns false — Payment Order is a disbursement). Agreements v5 / e-mandate. |

### 5.3 Xendit (`xendit`)

Hub: `XenditGatewayAdapter.cs` — already a hosted **invoice**, reminder-only.

| | |
|--|--|
| Create | `POST https://api.xendit.co/v2/invoices` Basic `{secret}:`. Amount + currency + success/cancel + external id / metadata `checkout_id`. Return `invoice_url` + invoice `id`. Discard `setupFutureUsage`. |
| Secrets | Secret key, **callback token** (`x-callback-token`). No Brand ID. |
| Verify | Header `x-callback-token` equals stored token (fixed-time). |
| Fulfill | Invoice `status` **PAID** (steal Hub `MapStatus`). Event id: invoice id + status namespaced (`paid:{id}`). |
| Ignore | PENDING / EXPIRED / SETTLED-as-paid-already. Do not fulfill twice. |
| Email | Required. |
| Do not | xenPlatform, wallet tiles on `:5179`, off-session (Hub comment: no token vault in v1). Refunds this slice. |

Wallets/DuitNow appear on **Xendit’s** page when the merchant enabled them there. Pay copy may say that. Pay must not draw tiles.

### 5.4 Razorpay (`razorpay`)

Hub: `RazorpayGatewayAdapter.cs` + `Razorpay.Api` SDK. Payment **link**, not invoice. `SetupFutureUsage` **discarded**. Off-session is a dead pipe.

| | |
|--|--|
| Create | `POST https://api.razorpay.com/v1/payment_links` Basic `key_id:key_secret`. Amount minor units, notes `checkout_id` / `org_id`, callback URL. Return `short_url` + `id`. Prefer raw HTTP. |
| Secrets | Store API as `key_id:key_secret` (Hub split on `:`), plus webhook secret. |
| Verify | Header `X-Razorpay-Signature`, HMAC-SHA256 of **raw body** with webhook secret (Hub `Utils.verifyWebhookSignature`). |
| Fulfill | `event == payment.captured` only. Event id: header `X-Razorpay-Event-Id` if present, else `captured:{pay_id}` — **never** bare `pay_` (fail+capture collision). Checkout id from notes. |
| Ignore | `payment.failed` (no fulfill). Other events 200 without unique-as-paid. |
| Email | Required if the Payment Link API requires customer — match Hub (`BuildPaymentLinkRequest`). |
| Do not | Official SDK unless HTTP is blocked. E-mandate / registration links. `ChargeOffSession`. Claiming INR vs MYR without the merchant’s Razorpay account actually supporting the currency — fail if currency missing, do not default MYR. |

Razorpay is the weakest MY dogfood. Ship it as a **labelled** wrap for merchants who already have keys, not as “we launched in India.”

---

## 6. Frontends

### 6.1 Merchant `:5178`

Today: hard-coded `provider: 'stripe'` and one `sk_test_` box.

**Must:**

- Provider select: `stripe | chip | billplz | xendit | razorpay` (staff, not buyer).
- Field sets per name (secret + webhook secret; Brand/Collection when needed; test/live for Billplz).
- Honest one-line capability: all five `hosted_link` in this program. Extra sentence: Billplz/Xendit/Razorpay = reminder + hosted page, never auto-debit. CHIP = hosted page now; vault later (not this slice).
- Do not show a five-logo “we take FPX/GrabPay/TnG” wall. Steal ops amber copy, not ops routes.
- Writer only on save. Member sees last4 + provider.
- Pay link still `http://localhost:5179/c/{token}`.

### 6.2 Checkout `:5179`

- Require name+email when active provider is chip/billplz/xendit (and razorpay if API needs it). Stripe may stay optional.
- No provider picker. No wallet tiles. No OIDC.
- Poll after return (`verifying`). Success URL is not paid.
- 503 `rail not configured` already exists; keep it for missing CHIP Brand ID etc.

---

## 7. Tests (must, hermetic)

Shared:

- Isolation greps in §3.5.
- Writer 403 on PUT gateway and POST checkouts for `member`.
- SST throw **gone**: fulfill with `SstRegistered` null still writes `RCPT-` (proves tax is out).
- One TX: if you can, fail fulfill and assert replay does **not** `{ duplicate: true }` without a document — or assert both event+document commit together.

Per provider (clone `WebhookTests` + a start test):

| Case | stripe | chip | billplz | xendit | razorpay |
|------|--------|------|---------|--------|----------|
| Empty body 400 | exists | must | must | must | must |
| Bad signature 400 | exists | RSA | HMAC form | token | HMAC JSON |
| Paid → one `RCPT-`, balanced journal | exists | `purchase.paid` | form paid | PAID | `payment.captured` |
| Replay `{ duplicate: true }`, still one doc | exists | must | must | must | must |
| Not-paid ignored, zero docs | setup/zero **missing today** | preauthorized | paid=false | PENDING | `payment.failed` |
| Start returns `redirect_url` (HTTP mocked) | Stripe.net hard | mock purchases | mock bills | mock invoices | mock payment_links |
| Missing Brand/Collection 400 | n/a | must | must | n/a | n/a |

Do **not** call live CHIP/Billplz/Xendit/Razorpay in `task pay:test`.

`pay-spec`: grow PUT/GET gateway fields, webhook path, public start. Do not import Hub `api-spec`. Comment must stop saying “fixture, not a charge.”

---

## 8. Sequence (even though you want all four)

Doing four HTTP extracts in one PR on a Stripe-only seam is how Hub shipped a factory before one honest loop. Sequence **inside** this program:

| Step | Intent | Exit |
|------|--------|------|
| **0** | Shared host: tax strip, credential columns, per-org webhook secret, one TX, writer checkout, `active_provider`, Isolation greps, Stripe verify from row | Stripe dogfood still mints `RCPT-`; SST throw gone; `whsec_` per org |
| **1** | CHIP hosted + RSA webhook + merchant fields + tests | `PUT provider=chip` works; preauthorized does not pay |
| **2** | Billplz hosted + form HMAC + `checkout_id` query + test\|live host + localhost callback **400** | Tunnel runbook noted; no DNS fallback |
| **3** | Xendit invoice + callback token | Reminder-only copy |
| **4** | Razorpay payment link + signature (HTTP) | Reminder-only copy; no SDK unless blocked |
| **5** | Merchant picker + checkout email required-by-rail + pay-spec + README honesty | Staff can switch `active_provider` |

1–4 after 0 can be parallel **only** if step 0 has landed (otherwise four copies of the thin Stripe webhook). Prefer 1 then 2 then 3+4.

**Do not** couple this to: Hub cutover, refunds, off-session, magic-link portal, LHDN, Go rewrite, retargeting `lazuar-ops`.

---

## 9. Refuse list for the implementing PRs

1. `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup of unused names.
2. ProjectReference `apps/lazuar-api` or `Modules.*`.
3. MediatR, outbox, `GatewayPaymentCompletedIntegrationEvent`.
4. Silent `ChipWebhookRegistrar` on PUT/boot.
5. `PublicDnsFallback` / `lazuar-local-dev.com`.
6. CHIP `purchase.preauthorized` as paid.
7. Stripe `mode=setup` as paid (keep ignore; **add the test** in step 0).
8. Off-session, Billing Portal, refunds, disputes.
9. SST field, `SstTaxMath`, tax journal, Tax Invoice, VALID, LHDN.
10. Booking processor `tax` / `fee` as 0.
11. Wallet / FPX / DuitNow tiles on `:5179`.
12. Buyer-facing provider dropdown.
13. Placeholder `customer@example.com` to CHIP/Billplz/Xendit.
14. Razorpay e-mandate / `ChargeOffSession`.
15. Default missing currency to MYR.
16. ACK 200 **before** unique insert; signature fail 500.
17. Fulfill inside the rail class.

---

## 10. Honest sales script after this program

You may say:

> Merchants paste **one** of Stripe, CHIP, Billplz, Xendit, or Razorpay. Buyers pay on **that processor’s hosted page**. Pay verifies the webhook, writes an Official Receipt and a two-line journal. We do not compute SST or file e-invoices. Billplz, Xendit, and Razorpay never auto-debit. CHIP hosted is not off-session until we say it is.

You may **not** say: five auto-debit rails; we take FPX ourselves; we are an acquirer; receipts are tax invoices; Hub is replaced.

---

## 11. Effort shape (so it can be staffed)

| Bucket | Why it is mandatory |
|--------|---------------------|
| Schema + PUT/GET + Stripe `whsec_` on the row | Four rails cannot share one process secret |
| Fulfillment: drop SST throw; one TX with event row | Tax out; money safety in |
| Dispatch: `active_provider` + switch + `checkout.Provider` | Start is Stripe-hardcoded today |
| CHIP HTTP + RSA + tests | First Malaysian rail; hardest verify |
| Billplz HTTP + form HMAC + public callback | Metadata-less join; tunnel |
| Xendit HTTP + callback token + tests | Smallest JSON wrap |
| Razorpay HTTP + HMAC + tests | Later wrap; easy to over-SDK |
| Merchant field sets + checkout email rules | Otherwise 4 adapters are API-only |
| Isolation + pay-spec + README | Stop Hub-README disease |

That is the whole program. The Hub `.cs` files are **read-only judgment**. The implementation lives only under `apps/lazuar-pay`, `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout`, and `packages/pay-spec`.
