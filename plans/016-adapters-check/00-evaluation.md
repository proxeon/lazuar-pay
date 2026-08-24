# 00 — Parent evaluation: five hosted_link rails on 8081 after 015, Hub as HTTP judgment, tests vs ticks

**Date:** 24 August 2026  
**Branch:** `feat/015-four-adapters`  
**HEAD:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` (`c621ceba`) — `docs(015): check off implemented T–Q phases`  
**This file is the parent judgment.** The ten reports `01`–`10` are the uncondensed evidence. **Do not treat this file as a substitute for those reports.** Do not skip a report because a table below has a one-liner.

014-evals froze the **new** stack at `ee2db8e5` (Stripe-only Bar B) and the **Hub** adapters as HTTP judgment. 015 then implemented four extra rails plus tax-out and merchant/checkout wiring, and ticked T–Q checklists. Live files on **this** SHA are authority. 015 checkboxes are a **map**, not proof. 011/11 tracker cells are **not** flipped by this paper.

---

## 1. Verdict

015 did the job 014 said must happen **before** copying four Hub adapter files: allow-list of five lowercase names, per-org webhook ciphertext, one DB transaction around fulfill (coded, unproven on InMemory), SST throw gone, writer-gated checkout create, `IHostedRail` plus a switch of known names, merchant rail picker, checkout `email_required` plus verifying poll. IsolationTests still fail the cathedral strings. There is still no `PaymentGatewayFactory`, no `ChipWebhookRegistrar`, no `PublicDnsFallback`, no SST math, no e-mandate.

That is **not** “Bar B is closed,” **not** “five rails are production BYOK,” and **not** “A99.2 paid + replay + not-paid for all five names.”

**On the user’s three questions:**

1. **Do the new gateway adapters on 8081 actually work, and do `:5178` / `:5179` call them?**  
   **Host: yes as HTTP extracts.** PUT/GET speak five names. Start switches five concrete `IHostedRail`s. Webhooks switch five parsers. Same-handler `Fulfillment` writes Official Receipt. Hermetic suite is **58** NUnit methods; **stripe** paid+replay+setup and **chip** paid+replay+preauth are real; **billplz / xendit / razorpay** are one happy path each.  
   **Frontends: wired for paste and poll, not “done.”** Staff PUT fields match the host for the five names. Buyer page reads `email_required` and `?status=verifying`. Remaining SPA lies: CHIP PEM is a single-line `<input>`, GET does not hydrate Billplz `environment` / `webhook_configured`, pay links and hosted success URLs are hardcoded `http://localhost:5179`, all start 400s collapse to one sentence, catalog create is decorative, start is not idempotent.

2. **How does this compare to Hub `Modules/Payments/Infrastructure/Gateways/`?**  
   Steal **HTTP**: Session/bill/invoice/link create, verify (HMAC / RSA / callback token), event-id grain, email refuse, public-https callback for Billplz.  
   **Refuse the type graph:** `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, MediatR, outbox, `PAYMENT_COMPLETED` for setup / CHIP `purchase.preauthorized`, CHIP registrar, `PublicDnsFallback`, Connect fee, Stripe Billing as SoT, refunds, off-session, SST. Live Pay matches that refuse.

3. **Which tests exist, and which must still be written?**  
   Named in [09](./09-tests-inventory.md) §9–§10 — **one method per remaining gap**, plus strengthen eight existing methods. 015 C32/B28/X23/R25/A99.2 ticks that those methods do not exist are **false**. Do not implement factory/registrar/SST tests. Do not call live PSP from `task pay:test`.

**Fix money first, then write the tests that would have caught the remaining holes.** Do not staff a factory, a registrar, DNS folklore, SST, or e-mandate in the same slice.

---

## 2. Where we actually are (three new apps, five names)

| App | Port | What it is on `c621ceba` | What 014 said at `ee2db8e5` |
|-----|------|--------------------------|------------------------------|
| `apps/lazuar-pay` | **8081** | Five `hosted_link` rails, per-org webhook ciphertext, writer mint, tax throw gone, same-handler Official Receipt | Stripe only; process `whsec_`; member mint; SST seed |
| `apps/lazuar-pay-merchant` | **5178** | OIDC PKCE; five-name `<select>`; per-rail paste; wrap copy; last4 for members | Stripe `sk_test_` paste only |
| `apps/lazuar-pay-checkout` | **5179** | Public GET/start; `email_required`; `?status=verifying` poll 2s×15; no OIDC, no PAN | GET once; query ignored; no poll |

Old stack is **museum, still running in root compose**: `lazuar-api` on **8080** (collides with One), ops **3003**, portal **3004**. CORS on 8081 denies 3003/3004. Do not retarget them. Hub adapters under `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` remain the **judgment library**. They are not a package.

### Live `/v1` doors (host)

```
GET  /health  /v1/health                 liveness; never One
GET  /ready                              Postgres CanConnect
GET  /v1/whoami                          Bearer → One /me
GET  /v1/orgs/{id}/ready                 dummy ready:true after member
POST /v1/checkouts                       writer; Postgres + idempotency
GET  /v1/checkouts/{id}                  member of session org
POST /v1/orgs/{id}/products              writer; MYR
GET  /v1/orgs/{id}/products              member
PUT  /v1/orgs/{id}/gateway               writer; five names; AES-GCM secret + webhook
GET  /v1/orgs/{id}/gateway               member; last4; capability hosted_link; optional ?provider=
GET  /v1/pay/{token}                     public; email_required from active/started rail
POST /v1/pay/{token}/start               public; switch of five IHostedRail
POST /v1/webhooks/{provider}/{orgId}     stripe|chip|billplz|xendit|razorpay → Fulfillment
POST /v1/one/webhooks                    HMAC → ChargesPaused (dialect still wrong)
GET  /v1/orgs/{id}/payments              member
GET  /v1/orgs/{id}/receipts[/id]         member; RCPT- / PENDING
```

`packages/pay-spec` now lists gateway PUT/GET and `email_required`. It still omits payments, receipts, unversioned `/ready`, start **body**, webhook `{duplicate}` / `{ignored}`. TypeSpec `start(@path token)` has no request body; both SPAs and the host send/accept `{ name, email }`.

### Dispatch, not a factory

`Program.cs` `AddScoped` five concretes. `PublicPayEndpoints.Start` and `WebhookEndpoints.Handle` `switch` on `PayProviders.*`. Unknown → 400. Capability is always `"hosted_link"`. One `org_settings.active_provider` per org; PUT always flips it. Buyer page has no PSP picker.

---

## 3. Evidence map

Do not skip a report because this table has a one-liner. Line counts are approximate at write time.

| Report | Slice | One-line take |
|--------|-------|---------------|
| [01](./01-new-host-seams.md) | Host seams | Must-do 0 landed in source. Residual: InMemory TX, webhook not bound to `checkout.Provider`, wrap-key git default, start `_ => stripe`, PUT always flips active. |
| [02](./02-merchant-frontend.md) | Merchant `:5178` | PUT fields match five names. PEM is `<input>` not textarea. GET does not hydrate `environment` / `webhook_configured`. Billplz re-save can flip live→test. Pay link hardcoded 5179. Catalog decorative. |
| [03](./03-checkout-frontend.md) | Checkout `:5179` | Poll exists (014 stale). 400 copy conflates callback-base and email. Poll dies at 30s. Placeholder email passes SPA. Preview 4179 not in CORS. |
| [04](./04-stripe-crosscheck.md) | Stripe Hub vs Pay | Right hosted extract. Steal card wrap / `payment_status==paid` / fail-closed currency next. Refuse Billing SoT, setup-as-`PAYMENT_COMPLETED`, Connect fee. Production-ready Stripe: **no**. |
| [05](./05-chip-crosscheck.md) | CHIP Hub vs Pay | Real purchases HTTP + RSA. Preauthorized **not** paid (inverts Hub). Registrar refused. Tests: paid+replay+preauth; missing bad RSA, currency, failure-then-paid. |
| [06](./06-billplz-crosscheck.md) | Billplz Hub vs Pay | JSON bills + dual HMAC stolen. Unpaid ignored (better than Hub `PAYMENT_FAILED`). Localhost fail-closed in **code**; named test does **not** prove it. Currency hardcoded MYR. |
| [07](./07-xendit-crosscheck.md) | Xendit Hub vs Pay | Invoices + callback token. SETTLED is **not** paid (money-safety win vs Hub). Token compare is not hash-first. One test: PAID+SETTLED. |
| [08](./08-razorpay-crosscheck.md) | Razorpay Hub vs Pay | Payment links over HttpClient, no `Razorpay.Api`. Captured only. Join is `notes.checkout_id` only (P0-C). Tax/fee unbooked. One test. |
| [09](./09-tests-inventory.md) | Tests | 58 `[Test]` methods catalogued. A99.2 false for three rails. Named strengthen list + ~70 methods to write. |
| [10](./10-honesty-frontend-risks.md) | Honesty / risks | 014 P0s scored. New P0-A start double-charge. Fix order. Sales-script / do-not-say. |

Binding plans this evaluation does **not** replace: [011](../011-new-lazuar-pay/README.md) (cells not flipped), [012](../012-one-to-pay/README.md), [013](../013-prods/README.md) (B99 unlived), [015](../015-four-adapters/README.md) (checklists are a map). 014 remains historical for `ee2db8e5`.

---

## 4. 014’s six P0s after 015 code

014/00 §6 ranked six P0s on `ee2db8e5`. Live files on `c621ceba` ([10](./10-honesty-frontend-risks.md) §1, [01](./01-new-host-seams.md)):

| 014 | Problem | After 015 | Status |
|-----|---------|-----------|--------|
| P0-1 | Process-wide `Pay:StripeWebhookSecret` | PUT requires per-org `webhook_secret`. Production empty ciphertext → 503. Non-Production empty row still falls back to process env. CHIP/Billplz/Xendit/Razorpay have **no** process fallback. | **Mostly closed.** Residual = P0-E. |
| P0-2 | Event insert committed before fulfill; path org unbound | Handler `BeginTransaction` + insert + `FulfillPaidAsync` + commit. Org bind tested (`Cross_org_checkout_is_400`). InMemory **ignores** transactions. No fulfill-throw test. | **Coded closed on Npgsql; unproven in CI.** |
| P0-3 | SST throw defeated by seed `false` | Throw removed. No SST read on pay path. Official Receipt. Column leftover unused. | **Closed as a money bug** (tax out, not fail-closed SST). Do not re-open as “compute SST.” |
| P0-4 | One HMAC dialect wrong; fulfill ignores pause | Unchanged. Body-only uppercase hex vs One `t=,v1=` over `{unix}.{body}`. Real `tenant.suspended` is 401. Fulfill does not read `ChargesPaused`. Zero HMAC tests. | **Still open, P0.** Four new rails inherit it. |
| P0-5 | Member can mint checkout | `RequireWriterAsync` + `CheckoutTests.Member_cannot_create_checkout`. | **Closed.** |
| P0-6 | Setup-not-paid untested | `Setup_mode_is_ignored`, `Zero_amount_session_is_ignored` (partial asserts), `Chip_preauthorized_is_ignored`. | **Closed as proof** for Stripe setup/zero and CHIP preauth. Family still missing: Billplz unpaid, Xendit EXPIRED, Razorpay `payment.failed`, amount/currency mismatch. |

---

## 5. New P0s 015 introduced or left next to the five rails

From [10](./10-honesty-frontend-risks.md) §6. Cash blast radius, not Hub parity.

### P0-A — Public start is not idempotent (two processor charges, one Official Receipt)

`PublicPayEndpoints.Start` always calls `CreateHostedUrlAsync` and overwrites `ProviderSessionId`. Buyer Pay → session A → lands without `?status=verifying` (cancel, stripped query, refresh) → form comes back → Pay again → session B. First paid webhook fulfills; second hits non-open checkout, still inserts that event id, `{ ok: true }`. Ledger is not double-booked. **The merchant’s PSP is.** Ada is charged twice. One receipt.

This was a 014 P1 with one rail. It is worse with five rails and a verifying screen that **only** hides Pay while the query param is present.

**Fix before writing more rail tests:** if `PspRedirectUrl` is set and status is `open`, return it (or 409 already started). Optionally 409 when `ProviderSessionId` exists. SPA: after back, GET again.

### P0-B — Plane A HMAC + pause-on-fulfill (014 P0-4, still)

Quoted in [10](./10-honesty-frontend-risks.md) §1.4. Steal One `OutboundWebhookSignature` judgment (`t=`/`v1=`, `{unix}.{body}`, lowercase hex, skew). Read `tenant_id` **and** `org_id`. Fulfillment must not book when `ChargesPaused` — prefer **not** consuming the paid event id so PSP retry after unsuspend still works.

### P0-C — Razorpay paid join is `payment.entity.notes.checkout_id` only

[08](./08-razorpay-crosscheck.md). Hosted create puts notes on the **payment link**. Webhook reads notes on **`payload.payment.entity`**. If Razorpay does not copy notes (or the merchant enabled `payment_link.paid` which this handler ignores unless `event == payment.captured`), `CheckoutId` is null → 400 before unique insert → retries forever → buyer paid, no `RCPT-`. `RailTests.Razorpay_captured` **injects** notes. Stripe / CHIP / Billplz / Xendit joins are stronger.

### P0-D — Parser mismatch 400 does not consume the event (lost cash if **we** are wrong)

Amount/currency mismatch → 400, **no** unique insert. Fail-closed if the payload is hostile. Lost cash if our units are wrong: CHIP `total` as minor (test `1000` for RM10); Xendit `paid_amount` as major then `ToMinor`; Billplz `paid_amount` as minor; Stripe cents. Pin against Hub’s documented units **and** one lived payload fixture per rail (runbook JSON, FakePsp in CI).

### P0-E — Residual Stripe platform `whsec_` on empty ciphertext, non-Production

New PUT always writes `WebhookCiphertext`. Pre-015 nullable rows, or anyone who NULLs the column, still verify from `Pay:StripeWebhookSecret` outside Production. Tighten fallback to **Testing only**.

---

## 6. Frontend connection (`:5178` / `:5179` ↔ 8081)

Detail lives in [02](./02-merchant-frontend.md) and [03](./03-checkout-frontend.md). Cross-cut table in [10](./10-honesty-frontend-risks.md) §4.

### What matches

- Origins: Vite 5178 / 5179 `strictPort`; host CORS allow-list those plus `127.0.0.1` twins; denies ops 3003 / portal 3004.
- Auth: merchant OIDC PKCE, JWT `access_token` Bearer, never `id_token`. Checkout public, no One.
- Five lowercase names on staff `<select>` = `PayProviders.All`. Buyer has **no** picker. Wallet/PAN greps hold.
- PUT JSON: `provider`, `secret` (Razorpay SPA joins `key_id:key_secret`), `webhook_secret`, CHIP/Billplz `public_merchant_id`, Billplz `environment`. Host is a superset (`key_id`/`key_secret` split unused by SPA).
- Writer paste / member lists + last4. Host writer PUT and checkout create; member GET.
- Public GET `email_required`; SPA disables Pay when flag and email empty.
- Default hosted success URL `?status=verifying`; SPA polls GET `/v1/pay/{token}` 2s × 15. 014 “checkout never reads the query” is **stale**.

### What does not match (do not demo)

| Mismatch | What happens |
|----------|----------------|
| CHIP PEM widget | U12 asked textarea. Live is `<input>`. PEM paste is hostile. |
| GET hydrate | SPA never `setEnvironment` / `webhook_configured` from GET. Billplz select always shows **test** after reload; re-save with rotated secrets **overwrites live → sandbox**. |
| Webhook URL copy | `{VITE_PAY_API_URL}/v1/webhooks/{provider}/{orgId}` defaults to `http://localhost:8081`. Billplz **callback** is `Pay:PublicBaseUrl` + `?checkout_id=`. Staff who paste the `<code>` into Billplz paste the wrong origin. |
| Pay link + success URL | Merchant mints `http://localhost:5179/c/{token}`. All five rails default success to the same host. No `VITE_CHECKOUT_ORIGIN` / `Pay:CheckoutBaseUrl`. Phone / deployed checkout never sees Paid even if Plane B fulfills. |
| Catalog | POST product then POST checkout `{ org_id, amount, currency: MYR }` — no `product_id`. Interval always `one_off`. “Product + pay link” is two independent rows. |
| Errors | Merchant: `keys ${status}`. Buyer: every 503 = “rail not configured”; every 400 = “callback base not public or email required.” Host `detail` discarded. |
| Placeholder email | SPA only checks `!email.trim()`. Host `BuyerEmail.IsUsable` rejects `customer@example.com`. Buyer sees the Billplz sentence. |
| Start idempotency | SPA always POSTs start. Host always creates a new PSP session (P0-A). |
| Verifying cap | After 15 ticks the interval clears; UI stays on “Verifying…” with no escape. Late Stripe retry looks hung. Opposite of P0-A. |
| Preview CORS | Checkout preview **4179** is not allow-listed. `pnpm preview` against 8081 hangs. |
| Spec | TypeSpec start has no body. Both apps send a body. |

Vitest: checkout 2 greps (OIDC, wallets/PAN); merchant 2 greps (password form, Hub types) + 4 bearer-token units. **No** component test of `App.tsx` / `WorkspacePage.tsx`. Named greps to add: [09](./09-tests-inventory.md) §10.9.

---

## 7. Hub HTTP vs Pay — steal / refuse (all five)

Do not copy `IPaymentGatewayAdapter`. Hub’s five-method port **lied** for Billplz/Xendit/Razorpay (off-session and refund return false / throw). Pay’s `IHostedRail` is one create method; parse is static next to the webhook switch. That is the stronger fence.

### 7.1 Already stolen (keep)

| Steal | Stripe | CHIP | Billplz | Xendit | Razorpay |
|-------|--------|------|---------|--------|----------|
| Create hosted URL with merchant key | Checkout Session `mode=payment` | POST purchases Bearer | POST v3 bills Basic `{key}:` JSON | POST `/v2/invoices` Basic major units | POST `/v1/payment_links` Basic `key_id:key_secret` |
| Verify Plane B | Stripe.net `EventUtility` | RSA PKCS1 PEM | Dual form HMAC extra then without | `x-callback-token` | HMAC-SHA256 raw body |
| Join checkout | `client_reference_id` / metadata | purchase metadata | query `checkout_id` then form then `reference_1` | `external_id` = checkout id | **notes only** (weak — P0-C) |
| Event grain | Stripe `evt_` | `paid:` / `preauth:` | `paid:` / `unpaid:` | `paid:` / `settled:` | `captured:` / `failed:` or header |
| Not paid | setup / amount 0 | `purchase.preauthorized` | unpaid form | SETTLED / EXPIRED | `payment.failed` |
| Email | optional | required; placeholder refuse | same | same | same |
| Capability | `hosted_link` | same; no `force_recurring` | never silent-debit | no channel list | no e-mandate payload |
| Fees / processor tax | not booked | not booked | not booked | `fees_paid_amount` unread | JSON `tax`/`fee` unread |
| Same-handler fulfill | yes | yes | yes | yes | yes |

### 7.2 Steal next (gaps, not new rails)

From [04](./04-stripe-crosscheck.md) §13.2 and siblings:

1. Stripe `PaymentMethodTypes = ["card"]`; `payment_status == paid`; copy metadata onto PI; `Idempotency-Key = lazuar-checkout:{id}` (also the P0-A belt).
2. Fail-closed missing currency (Hub). Stripe currently **skips** when parse emits null. Billplz currently **defaults MYR** ([06](./06-billplz-crosscheck.md), P1-2).
3. `GatewayCommon.ToMinorUnits` zero-decimal table **or** refuse non-MYR on checkout create. Always ×100 is a JPY footgun.
4. Xendit token compare **hash-first** (Hub 073) — timing leak on length is residual.
5. Razorpay join fallback via stored `plink_` / confirm lived `payment.captured` notes (P0-C).
6. Product name on hosted description, not `"Pay"`.
7. Isolation extra greps: `ChipWebhookRegistrar`, `PublicDnsFallback`, `application_fee` / `TransferData`, LHDN tokens ([09](./09-tests-inventory.md) §9.8). Do **not** grep `lazuar-local-dev.com` (Billplz **block** list contains it).

### 7.3 Refuse (must never be copied)

Full lists: [04](./04-stripe-crosscheck.md) §14, [05](./05-chip-crosscheck.md) §18, [06](./06-billplz-crosscheck.md) §14.3, [07](./07-xendit-crosscheck.md) §16, [08](./08-razorpay-crosscheck.md) §10, [10](./10-honesty-frontend-risks.md) §5, 015 `parked-*.md`.

Do not:

1. `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup of unused names.
2. ProjectReference `apps/lazuar-api`. MediatR, outbox, `GatewayPaymentCompletedIntegrationEvent`.
3. Silent `ChipWebhookRegistrar` on PUT/boot.
4. `PublicDnsFallback` / rewrite to `lazuar-local-dev.com`.
5. Hub `PAYMENT_COMPLETED` for Stripe setup, CHIP `purchase.preauthorized` (Hub **tests lock that lie** — invert, do not copy).
6. Map Xendit SETTLED to paid because Hub did.
7. Stripe Billing as SoT (`customer.subscription.updated`, `invoice.paid`, `mode=subscription`).
8. Connect `application_fee` / `TransferData` / `Stripe-Account`.
9. `Razorpay.Api` SDK. Billplz Payment Orders as refunds. Agreements v5 / e-mandate.
10. SST / LHDN / Tax Invoice / VALID / `SstTaxMath`.
11. GrabPay / TnG / FPX tiles on `:5179`. Channel allow-lists on Xendit create.
12. `DecryptOrPlaintext`. Vite `sk_live_` / PEM defaults.
13. Treat 015 `[x]` as evidence the test was written.

---

## 8. Tests: what exists vs what to write

Authority: [09](./09-tests-inventory.md). This section is an index, not a second inventory.

### 8.1 What 58 NUnit methods actually lock

| File | Count | Honest coverage |
|------|------:|-----------------|
| `WebhookTests` | 7 | Stripe empty-secret 503, bad sig 400, paid+replay+`RCPT-`, setup ignored, zero-amount ignored (partial), cross-org 400, unknown provider 400 |
| `RailTests` | 7 | CHIP start+paid+replay+no `force_recurring`; CHIP preauth ignore; CHIP missing email; CHIP empty body; Billplz **paid only** (name lies about localhost); Xendit PAID+SETTLED; Razorpay captured + 2 journal lines |
| `GatewayTests` | 4 | Member PUT 403; webhook_secret required; GET no echo; CHIP Brand ID required |
| `PublicPayTests` | 3 | Public GET no Bearer; missing 404; Stripe empty webhook |
| `IsolationTests` | 6 | Hub types, MediatR, Hub csproj, Vite `@repo/api-types-ts`, `Razorpay.Api` |
| `CheckoutTests` | 10 | Writer mint, org bind, idempotency, health skips One |
| `CatalogTests` | 2 | Writer product |
| `CorsTests` | 4 | 5178/5179 allow; 3003/3004 deny |
| Plus health / org-ready / whoami | rest | One façade, not rails |

A99.2 claimed paid + replay + not-paid for **five** names. Live: **stripe yes**, **chip yes**, **billplz paid only**, **xendit paid+settled only**, **razorpay paid+tax-lines only**.

`PayApiFactory` uses EF InMemory and **ignores transactions**. `Pay:PublicBaseUrl=https://pay.test.example` so the Billplz method **cannot** prove B15. FakePsp captures `LastUri`/`LastBody`; only CHIP body and Billplz sandbox host are asserted.

### 8.2 Strengthen first (do not clone a weak paid test five times)

[09](./09-tests-inventory.md) §9: edit eight existing methods — zero-amount asserts `ignored` + checkout `open`; paid Stripe asserts Official Receipt title + checkout `paid` + SST null; CHIP start asserts `redirect_url` / `Provider` / `ProviderSessionId`; Billplz paid asserts `RCPT-` + replay (**do not** assert localhost there); Xendit paid asserts `RCPT-` + SETTLED ignored token; Razorpay asserts `RCPT-` + debit=credit; Isolation `BannedSrc` grows registrar/DNS/Connect/LHDN tokens.

### 8.3 Then write — one method per gap

Full names, fixtures, and assert strings: [09](./09-tests-inventory.md) §10. Count ~70 including frontend greps. Priority from [10](./10-honesty-frontend-risks.md) §8.2 after **product** fixes:

| Pri | Test family | Why |
|-----|-------------|-----|
| T0 | Postgres (or SQLite with real TX) fulfill-throw rolls back event id | Closes 014 P0-2 as **proof**. InMemory cannot. |
| T1 | Start twice → same `redirect_url` or 409; FakePsp send count 1 | P0-A |
| T2–T3 | One HMAC Standard Webhooks vector; paused org does not mint `RCPT-` | P0-B |
| T4 | Amount mismatch 400, documents 0, **event row absent**; currency omit per rail | P0-D / P1-2 / P1-3 |
| T5 | Razorpay captured **without** notes; `payment.failed`; header Event-Id | P0-C |
| T6 | Placeholder `customer@example.com` start 400 on four rails | P20 |
| T7 | Billplz `PublicBaseUrl=http://localhost:8081` start 400, no PSP HTTP | B15; kill the lying method name |
| T8 | Production missing wrap key; empty org ciphertext 503 even if process Stripe secret set | H16 / P0-E |
| T9 | Member GET metadata; PUT unknown 400; GET `?provider=` does not flip active | H18 / P15 / P22 |
| T10 | Checkout vitest greps for verifying / email_required / 503 copy | after T0–T7 |

**Do not** write: registrar tests, factory tests, SST math tests, refund tests, e-mandate tests, live CHIP in `task pay:test`, Stripe.net start without a client seam.

Lived PSP payloads for units (P0-D / T5) belong as **checked-in JSON** driven through FakePsp, not a network call in CI.

---

## 9. What you may say vs must not say

Full scripts: [10](./10-honesty-frontend-risks.md) §2–§3. Short form:

**Say (CI unless marked runbook):**

- Focused Pay is a separate `net10.0` host on 8081. IsolationTests fail Hub csproj / MediatR / factory types / `Razorpay.Api`.
- Five lowercase wrap names; capability `hosted_link` for all five; one active rail per org; buyer has no picker.
- Dispatch is a switch, not a factory.
- Owner/admin paste keys; member cannot PUT or mint.
- BYOK API + webhook secrets AES-GCM. GET never echoes them. Production wrap requires `Pay:WrapKey`.
- Stripe `mode=payment` hosted; CHIP/Billplz/Xendit/Razorpay HttpClient hosted links; email required except Stripe.
- Billplz localhost callback **fails closed in code**. We did not port DNS fallback. (Do **not** say the named test proves it.)
- Verified paid webhooks write Official Receipt `RCPT-` + two-line journal of `checkout.Amount`. Replay after success is duplicate. Setup / preauthorized / SETTLED are not paid. Fees and processor tax are not booked.
- `:5179` polls `?status=verifying`. Success URL is not paid. Buyers have no One account.

**Do not say:**

| Lie | Why |
|-----|-----|
| We replaced Hub | Root compose still boots `lazuar-api`. Cutover parked. |
| Pay v1 / Bar B / A99 is done | B99 unlived. Plane A HMAC wrong. Start not idempotent. |
| We have a factory of five | Switch of known names. Isolation bans the type. |
| CHIP/Billplz/Xendit/Razorpay are not on 8081 | They are, as `hosted_link`. 014 parent is stale. |
| Five logos / wallets on the buyer page | Staff select only. Wallets live on the **processor** page. |
| Off-session / e-mandate / auto-debit | Capability `hosted_link`. CHIP copy says later. |
| Pay registers CHIP webhooks | No registrar. Ada pastes PEM. |
| We rewrite Billplz DNS | Predicate **rejects** `lazuar-local-dev.com`. |
| We file MyInvois / this is a Tax Invoice | Tax out. Official Receipt. |
| Webhook secret is a platform env for every rail | PUT requires per-org. Stripe **dev** fallback only. |
| `:5179` ignores verifying | It polls. 014 stale. |
| Member can mint via curl | Writer gate + test. 014 stale. |
| `tenant.suspended` stops in-flight fulfill | HMAC never verifies; fulfill ignores pause. |
| InMemory tests prove one DB transaction | Factory ignores `TransactionIgnoredWarning`. |
| Lived Billplz works from the merchant webhook `<code>` | That string is localhost 8081. Real callback is PublicBaseUrl + query. |
| Start is safe to double-click | P0-A. |
| Razorpay is done | Notes join unproven on lived `payment.captured`. |
| Host README still says in-memory fixture | 014 smear **retired** — README now describes Postgres + five rails. |

---

## 10. What to do next (order)

Product code **before** a 70-method test dump. From [10](./10-honesty-frontend-risks.md) §8.1:

1. **Start idempotency (P0-A).** Return existing hosted URL or 409. Cheapest cash bug.
2. **Fulfillment respects `ChargesPaused` (P0-B part 2).** Prefer not consuming the paid event id.
3. **One HMAC dialect (P0-B part 1).** Steal One’s signer judgment. Not an adapter.
4. **Razorpay join (P0-C).** Confirm lived notes; else join via `plink_` stored as `ProviderSessionId`.
5. **Pin units (P0-D)** in parser comments + mismatch tests + one JSON fixture per rail.
6. **Empty webhook ciphertext: Testing-only Stripe process fallback (P0-E).**
7. **`Pay:CheckoutBaseUrl` / merchant copy link (P1-5).** One config for mint + hosted defaults. Billplz `redirect_url` must not stay laptop-localhost when callback is a tunnel.

Then write tests in §8 order. **Do not** implement tax, factory, registrar, DNS, refunds, or off-session in that list.

Lived dogfood (A99.1 / B99) remains a **human** loop: One on 8080 with Hub **off**, Pay 8081 + Postgres 5435 + `FourAdaptersHostedRails`, tunnel for Plane B, **one** rail per demo. Five names in a `<select>` are not five lived loops. `NP-GW-003` still wants a lived CHIP payment before anyone honest flips 011.

---

## 11. Closing

015 moved new Pay from “one thin Stripe class with platform `whsec_` and a two-TX fulfill” to “five hosted_link HTTP extracts, per-org webhook ciphertext, writer mint, tax out, a verifying poll.” Isolation still holds. The cathedral types still fail CI. The refuse list still binds.

014’s P0 list is **not** fully retired. Member mint, setup-not-paid-as-proof, and SST-seed are. Process `whsec_` is retired for **new** PUT rows in Production. One HMAC + pause-on-fulfill is not. One-TX is a Postgres reading of the handler, not a test.

The new shared P0 is **start mints a new processor session every time**. Razorpay’s paid join is the weakest of the five. Billplz’s localhost 400 is real in `TryPublicBase` and a **lie** as a test name. Xendit’s SETTLED-not-paid is a money-safety win versus Hub.

Frontends are closer to the host than 014 found. They are not “wired” in the sense that would survive a phone, a PEM paste, a Billplz live re-save, or a late webhook.

**Fix start idempotency, One HMAC, pause-on-fulfill, and Razorpay join. Prove rollback on a real transaction. Then write the mismatch tests named in 09. Do not staff a factory, a registrar, DNS, tax, or e-mandate.**

This paper does not flip 011/11 cells. It does not implement the tests. Implementation of [09](./09-tests-inventory.md) §9–§10 is the next **engineering** slice; this directory is the evaluation that slice must not contradict.

Read the ten reports. This file is the judgment, not the evidence.
