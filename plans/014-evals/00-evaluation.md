# 00 — Parent evaluation: new Lazuar Pay after Bar B, and Hub adapters as HTTP judgment

**Date:** 24 August 2026  
**Branch:** `main`  
**HEAD:** `ee2db8e5758305089a38298456c456d6bf0e97ca` (`ee2db8e5`) — `feat(pay): Bar B receipts, webhook secret, merchant money UI`  
**This file is the parent judgment.** The ten reports `01`–`10` are the uncondensed evidence (~11,700 lines). Do not treat this file as a substitute for those reports.

013-prods froze at `6f866ff0` (“whoami, in-memory checkout, health-probe Vite”). 008-evals froze the **Hub** tree after Waves 0–4. Both are historical. Live files on this SHA are authority.

---

## 1. Verdict

New Lazuar Pay is a **real focused cashier**, not a whoami fixture, and **not** Hub Payments moved to 8081.

It is a single `net10.0` process on **8081** that:

- talks to **lazuar-one** over HTTP (`/me`, `authz/check`, `POST /tenants` from the merchant SPA);
- persists into **one** Postgres (`lazuar_pay` on **5435**) through **one** `PayDbContext` / `public` schema;
- encrypts a per-org Stripe `sk_` with AES-GCM;
- creates a Stripe Checkout Session in `mode=payment`;
- verifies a Stripe webhook and, in the same HTTP request, writes `paid` + a two-line journal + Official Receipt `RCPT-{MYT year}-#####`.

Merchant staff sign in on **`:5178`** through One OIDC. Buyers pay on **`:5179/c/{token}`** with **no** One account. IsolationTests still ban MediatR, BuildingBlocks, `Modules.`, and a project reference into `apps/lazuar-api`.

It is **not** production BYOK Stripe. The webhook signing secret is a **process** env var (`Pay:StripeWebhookSecret`), not per-org. The PSP event row is committed **before** fulfillment, so a throw after insert turns Stripe’s retry into a permanent `{ duplicate: true }` with no `RCPT-`. SST “fail-closed” is defeated by auto-seeding `SstRegistered = false`. `tenant.suspended` HMAC does not match One’s real envelope. Bar B’s lived sentence ([B99](../013-prods/checklists/b99-bar-b-done.md)) is still all unchecked: hermetic tests ≠ Ada paid on Stripe.

**On the user’s belief — “we can implement these adapters into new Pay”:**

> **Yes**, as **HTTP judgment**, **one rail at a time**, as small classes next to `StripeHosted`. Next Malaysian rail is **CHIP Collect**, after Stripe’s money-safety holes are closed.  
> **No**, as a copy of `Modules/Payments/Infrastructure/Gateways/` plus `IPaymentGatewayAdapter` plus `PaymentGatewayFactory` plus five names on day one. That is the cathedral IsolationTests exist to kill.

Do not start CHIP, Xendit, Razorpay, or a factory in the same slice as the P0s below.

---

## 2. Where we actually are (three new apps)

| App | Port | What it is on `ee2db8e5` | What 013 said at `6f866ff0` |
|-----|------|---------------------------|------------------------------|
| `apps/lazuar-pay` | **8081** | One host, Postgres, Stripe hosted + webhook, same-handler fulfillment, One HMAC route | In-memory `ConcurrentDictionary`, zero packages, no Stripe |
| `apps/lazuar-pay-merchant` | **5178** | OIDC PKCE staff shell: workspaces, paste `sk_test_`, MYR product + pay link, list payments/receipts | Health probe, no OIDC |
| `apps/lazuar-pay-checkout` | **5179** | `/c/{token}` cash register: public GET/start, redirect to Stripe. No OIDC, no PAN | Health probe |

Old stack is **museum, still running in root compose**: `lazuar-api` on **8080** (collides with One), ops **3003**, portal **3004**, admin **3005**. CORS on 8081 denies 3003/3004. Do not retarget them.

### Live `/v1` doors (host)

```
GET  /health  /v1/health          liveness; never One
GET  /ready                       Postgres CanConnect
GET  /v1/whoami                   Bearer → One /me
GET  /v1/orgs/{id}/ready          dummy ready:true after member check
POST /v1/checkouts                member; Postgres + idempotency
GET  /v1/checkouts/{id}           member of session org
POST /v1/orgs/{id}/products       writer; MYR
GET  /v1/orgs/{id}/products       member
PUT  /v1/orgs/{id}/gateway        writer; stripe only; AES-GCM sk_
GET  /v1/orgs/{id}/gateway        member; last4; capability hosted_link
GET  /v1/pay/{token}              public
POST /v1/pay/{token}/start        public; Stripe Checkout URL
POST /v1/webhooks/{provider}/{orgId}  stripe only → Fulfillment
POST /v1/one/webhooks             HMAC → ChargesPaused (dialect wrong)
GET  /v1/orgs/{id}/payments       member
GET  /v1/orgs/{id}/receipts[/id]  member; RCPT- / PENDING
```

`packages/pay-spec` lists health, whoami, ready, checkouts, public pay, catalog, webhooks. It does **not** list gateway PUT/GET, payments, receipts, or unversioned `/ready`. Host README still says checkout is an in-memory fixture. That is Hub-class README disease on the new stack.

### One rail, not five

`GatewayEndpoints.Put` 400s anything except `"stripe"` (`"Bar B first rail is stripe"`). `WebhookEndpoints` 400s unknown provider. Grep of `apps/lazuar-pay` for CHIP / Billplz / Xendit / Razorpay / `PaymentGatewayFactory` is **empty**. Capability string is `hosted_link`. There is no Elements, no off-session, no Billing, no Connect fee.

Hub still has the factory of five under `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` (~3,294 lines). That folder is the **judgment library**. It is not a package to reference.

---

## 3. Evidence map

Do not skip a report because this table has a one-liner.

| Report | Slice | Lines | One-line take |
|--------|-------|------:|---------------|
| [01](./01-new-pay-host.md) | Focused host | 1481 | Postgres cashier on 8081. Isolation holds. README/spec stale. B99 unlived. |
| [02](./02-merchant-frontend.md) | Merchant Vite 5178 | 1217 | OIDC + money UI. Writer paste / member lists. Checkout create API is still member-gated. |
| [03](./03-checkout-frontend.md) | Checkout Vite 5179 | 1311 | `/c/{token}` redirects to Stripe. No OIDC. `?status=verifying` is ignored; no poll. |
| [04](./04-old-adapter-seam.md) | Hub adapter seam | 1389 | Five-method port + factory + outbox after parse. Steal decisions; refuse the type graph. |
| [05](./05-stripe-port.md) | Stripe old vs new | 1131 | Right shape, not production BYOK. Platform `whsec_`. Two-TX idempotency. |
| [06](./06-malaysia-rails.md) | CHIP + Billplz | 993 | Zero MY code on 8081. Next rail = **CHIP**, after Stripe dogfood. Billplz = later reminder-only. |
| [07](./07-sea-later-rails.md) | Xendit + Razorpay | 1038 | Keep the 400 wall. Reminder-only later. Copying now recreates the factory of five. |
| [08](./08-webhooks-secrets-fulfillment.md) | Money safety | 1477 | Same-handler win; two-TX bug; platform `whsec_`; One HMAC dialect wrong. |
| [09](./09-porting-architecture.md) | New seam | 1069 | `IHostedRail` = CreateHostedUrl + ParseWebhook. Switch of two names. No factory. |
| [10](./10-honesty-gaps-next.md) | Honesty / next | 587 | Sales script, do-not-say, P0s, tracker drift, next ten. |

Binding plans this evaluation does **not** replace: [011](../011-new-lazuar-pay/README.md) product + tracker (cells not flipped), [012](../012-one-to-pay/README.md) One façade, [013](../013-prods/README.md) Bar B checklists (B99 still open).

---

## 4. Answer on Hub adapters

### 4.1 What Hub actually is

`IPaymentGatewayAdapter` is a **cashier port**: `GenerateCheckoutAsync`, `ParseWebhookAsync` (per-call `webhookSecret`), `IssueRefundAsync`, `GenerateCustomerPortalAsync`, `ChargeOffSessionAsync`. `PaymentGatewayFactory` uppercases the name and resolves `IEnumerable<IPaymentGatewayAdapter>`. After parse, Hub writes `PaymentWebhookLog` and publishes `GatewayPaymentCompletedIntegrationEvent` onto an outbox. Commerce / Billing / Lhdn consume later. Payments README is explicit: not a fulfillment engine, not a ledger. That split **is** the cathedral 011 left.

Honest wrap-rails matrix (`PaymentGatewayCapabilities` — restate the law, do not reference the class):

| Name | Off-session | Hosted link | API refund | E-mandate |
|------|-------------|-------------|------------|-----------|
| Stripe | yes if vaulted PM | yes | yes | **false** |
| CHIP | yes if vaulted token | yes | yes | **false** |
| Billplz | **no** | yes | **no** (mark refunded) | **false** |
| Xendit | **no** | yes (invoice) | yes | **false** |
| Razorpay | **no** (`SetupFutureUsage` discarded) | yes (payment link) | yes | **false** |

Hub still maps Stripe `setup_intent.succeeded` to `PAYMENT_COMPLETED` with amount 0. Steal the PM extract. **Do not steal the event name.** New Pay already ignores `mode=setup` / amount 0 (untested).

### 4.2 What new Pay already stole (Stripe)

| Steal | Live |
|-------|------|
| BYOK `sk_` at rest | `SecretBox` AES-GCM, `gateway_credentials` |
| Hosted Checkout `mode=payment` | `StripeHosted.CreateHostedUrlAsync` |
| Public `/v1/webhooks/{provider}/{orgId}` | Stripe-Signature via Stripe.net |
| Empty body 400 | `PublicPayTests` |
| Unique `(org_id, provider, event_id)` | `psp_webhook_events` PK |
| Same **request** fulfillment | `Fulfillment.FulfillPaidAsync` |
| Official Receipt, not Tax Invoice | title hard-coded |
| No Connect `application_fee_amount` | never set |
| No Stripe Billing SoT | webhook listens `checkout.session.completed` only |
| Isolation | no MediatR / Modules / Hub csproj |

### 4.3 What must not be copied

- `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / five DI registrations “disabled.”
- MediatR `ProcessGatewayWebhookCommand`, outbox, `GatewayPayment*` events, per-module `payments` schema.
- `AesSecretVault` / `DecryptOrPlaintext`.
- `ChipWebhookRegistrar` on boot or on PUT (surprise-POST into Ada’s CHIP account).
- `PublicDnsFallback` / `lazuar-local-dev.com` “just in case.”
- Hub `PAYMENT_COMPLETED` for setup / CHIP `purchase.preauthorized`.
- Stripe Billing portal as Pay’s subscription SoT.
- ProjectReference to `apps/lazuar-api`. Shared adapter NuGet. Go rewrite of this C# host.

### 4.4 How to add the next rail (design in [09](./09-porting-architecture.md))

Keep the live shape. Widen it by **one** name.

1. Small interface when the second class exists: `CreateHostedUrl` + `ParseWebhook` (verify, event id, checkout id, paid amount, ignore-setup). Refunds and off-session are **later verbs**, and off-session only for Stripe/CHIP after a real token exists.
2. Concrete DI (`AddScoped<StripeHosted>()`, later `AddScoped<ChipHosted>()`). A **two-name switch** in `GatewayEndpoints` / `WebhookEndpoints` is allowed. An `IEnumerable` factory that can resolve unused names is how Hub grew five adapters.
3. Per-org secrets: `sk_` **and** `whsec_` (or CHIP Bearer + Brand ID + PEM). Process env is a **dev fallback**, not production SoT.
4. Same `Fulfillment.FulfillPaidAsync`. Rails do not journal.
5. Tests cloned from `WebhookTests`. IsolationTests stay red on cathedral strings.
6. Sequencing: **Stripe harden → CHIP hosted_link → refunds or off-session → Billplz reminder-only → Xendit/Razorpay later.** Do not start all five.

CHIP vs Billplz ([06](./06-malaysia-rails.md)): **CHIP Collect** is the Malaysian dogfood. It can vault. Billplz is reminder + hosted bill forever, needs Collection ID + public HTTPS callback, and carries DNS folklore new Pay does not need until a named Billplz merchant exists. 013 `decisions.md` already locked this.

Xendit / Razorpay ([07](./07-sea-later-rails.md)): keep the 400. 008’s “Xendit UI inoperable” is **stale** in Hub ops; that does not make them day-one 8081 rails. Razorpay `ChargeOffSessionAsync` is a dead pipe. Wallets live on the **processor** page; Pay must never draw GrabPay/TnG tiles.

---

## 5. What is honestly sellable today (new stack only)

Say these sentences and the **code** will back you. Several need a **runbook** (live One + Stripe test + tunnel), not CI.

1. Focused Pay is a separate C# host on **8081**. Hub still exists on 8080 if you boot root compose.
2. Merchants are One humans. Pay does not store passwords or `Modules/One`. One tenant id **is** `org_id`.
3. Staff UI is **`:5178`**. Login is One **`:5175`**. Not ops, not admin.
4. Buyers have **no** One account. Shareable link is `http://localhost:5179/c/{token}`.
5. Checkouts live in Postgres `lazuar_pay` on **5435**. They survive process restart. Tests use EF InMemory.
6. First rail is **Stripe hosted Checkout**, capability `hosted_link`. Money settles on the merchant’s Stripe account. We are not an acquirer, not MoR, not Connect.
7. `owner` / `admin` paste `sk_test_`; `member` cannot PUT keys (UI + writer gate). `member` **can** still `POST /v1/checkouts` (API hole).
8. A verified `checkout.session.completed` with `mode=payment` and amount > 0 writes charge + balanced two-line journal + `RCPT-…` titled Official Receipt. Replay after **success** is `{ duplicate: true }`.
9. Empty PSP body is 400. Bad Stripe signature is 400. Missing process `whsec_` is 503.
10. Receipt is **not** a tax invoice and does not print VALID.

### Do not say

| Lie | Why |
|-----|-----|
| We replaced Hub | Root compose still boots `lazuar-api`. Cutover parked. |
| Pay v1 / Bar B is done | B99 unchecked. Bar C parked. |
| Five adapters / CHIP live on 8081 | One class: `StripeHosted`. |
| We take cards on our page | Redirect to Stripe. |
| Off-session / subscriptions renew | Interval hard-coded `one_off` on checkout create. |
| We file MyInvois | Refuse `NP-XX-001`. |
| SST is computed | Two-line cash/revenue; SST auto-seeded false. |
| Webhook secret is BYOK | Process `Pay:StripeWebhookSecret`. |
| `?status=verifying` means paid | Checkout SPA never reads the query; no poll. |
| VIEWER is a One role | One has owner/admin/member. |
| `/ready` means we can charge | Dummy `ready: true` after member check. |
| We email receipts | `mail_outbox` has no producer. |
| Compose is Pay | `docker-compose.pay.yml` is **DB only**. No Pay Dockerfile. |

Refuse list `NP-XX-001`–`024` stays refuse. Un-refusing any of them is how the museum comes back.

---

## 6. Ranked remaining problems (new Pay)

Priority is **cash blast radius**, not Hub parity.

### P0 — money can be wrong, forged, undercharged, or charged after suspend

| # | Problem | Evidence |
|---|---------|----------|
| 1 | Webhook signing secret is process-wide. Anyone with `Pay:StripeWebhookSecret` can forge `checkout.session.completed` for every org that has a Stripe row. Merchant PUT stores `sk_`, not `whsec_`. | [05](./05-stripe-port.md), [08](./08-webhooks-secrets-fulfillment.md), [10](./10-honesty-gaps-next.md) P0-1 |
| 2 | `psp_webhook_events` `SaveChanges` **then** `FulfillPaidAsync` opens its own TX. Throw after insert → retry `{ duplicate: true }` → buyer paid Stripe, Pay has no `RCPT-`. Path `{orgId}` is not bound to `checkout.OrgId`. | [08](./08-webhooks-secrets-fulfillment.md) §4.5 / §15, [05](./05-stripe-port.md) |
| 3 | SST fail-closed throws on `SstRegistered is null`; checkout create seeds `false`. Unknown coerced to unregistered is undercharge. Journal has no tax line even if `true`. | [01](./01-new-pay-host.md), [08](./08-webhooks-secrets-fulfillment.md) §6.3, [10](./10-honesty-gaps-next.md) P0-3 |
| 4 | Plane A HMAC is body-only uppercase hex vs One’s `v1=` over `{unix}.{body}`; handler reads `org_id` not `tenant_id`. Real `tenant.suspended` never sets `ChargesPaused`. Fulfillment ignores pause even if the flag is set. **No tests.** | [08](./08-webhooks-secrets-fulfillment.md) §9, [10](./10-honesty-gaps-next.md) P0-4 |
| 5 | `POST /v1/checkouts` is `RequireMemberAsync`. UI hides the button. Curl with a member token mints a pay link. | [02](./02-merchant-frontend.md), [10](./10-honesty-gaps-next.md) P0-5 |
| 6 | Setup-not-paid is a branch without a test. Fail lock in 011/03 is unproven. Cheap to forge if P0-1 leaks. | [05](./05-stripe-port.md), [10](./10-honesty-gaps-next.md) P0-6 |

### P1 — honesty and dogfood that still lies in copy

- Host README + `pay-spec` still say “fixture, not a charge.” Spec missing gateway/payments/receipts.
- `SecretBox` hashes git-known `"lazuar-pay-dev-wrap-key"` when `Pay:WrapKey` is missing.
- Checkout SPA: no verifying poll; name/email not required; start not idempotent; success URL is not paid (honest) but the form comes back (not).
- Pay-link ignores catalog `product_id` / interval. Dogfood button never writes a subscription row.
- No member-cannot-PUT-keys test; no One HMAC vectors; InMemory tests ignore transactions — same-TX is unproven on 5435.
- No Pay Dockerfile. Root compose still Hub. CORS still four localhost literals.
- 011 Status cells still `todo` while Bar B code exists; 013 checklists over-tick tests that are not in the tree (G22 setup fixture, O16 pause, F18 unknown SST).

### P2 — after money is boring

CHIP as second rail. SST × seats. Refund-once. Expire open sessions. Receipt email. Buyer magic-link on `:5179`. Subscribers list. Hub cutover. Razorpay/Xendit labelled reminder-only. None of these before P0.

---

## 7. Tracker honesty (do not flip from this file)

011/11 on this SHA still reads: **10 done / 81 todo / 24 refuse**. The ten `done` cells are the 012 C99 whoami/checkout-fixture set. They are still true as *existence*. Notes that say “in-memory fixture” **under-claim**.

**LOOK `todo` but code exists** (partial or hermetic only): NP-CAT-001/003/005, NP-CHK-005/006/007, NP-GW-001/002/004/005/006/008/009, NP-FUL-001, NP-DOC-001–003/005, NP-API-002, NP-MON-001 (two-line only), NP-BUY-001.

**Still genuinely absent:** NP-GW-003 Malaysian rail, NP-FUL-004 renew, NP-MON-003/005 refunds/SST×seats, NP-ONE-014 `lzr_sk_`, invite flow, mail producer, Pay image.

**Do not flip NP-GW-003 because Stripe exists.** XOR. CHIP is absent.

013 papers remain useful as **bar and anti-goals**. They are dangerous as **inventory**. Especially [013/06](../013-prods/06-money-rails.md): “There is no Pay database. There is no key column. There is no PSP client.” That sentence is false on `ee2db8e5`. Plane A vs B vs C split in that paper is still law.

B99 remains the honest close: a lived dogfood sentence, not `WebhookTests` green.

---

## 8. What to do next

Order is [10](./10-honesty-gaps-next.md) §7. Parent list (analysis only, not a checklist flip):

1. **Per-org Stripe `whsec_`** (paste second field or register endpoint on that account). Process env = dev fallback. Closes P0-1.
2. **One DB transaction:** verify → unique insert → fulfill → commit. Bind `checkout.OrgId == path orgId`. Unique violation → 200. Fulfill throw rolls back the event id → Stripe retry is correct. Closes P0-2.
3. **Stop auto-seeding `SstRegistered = false`.** Leave unknown. Merchant writer yes/no. Keep the throw. Closes P0-3.
4. **Speak One’s HMAC; pause fulfill for new attempts; hermetic suspend tests.** Closes P0-4.
5. **`POST /v1/checkouts` is `RequireWriterAsync`.** Member GET stays. Closes P0-5.
6. **Fail-lock tests:** `mode=setup` / amount 0 write zero documents; member cannot PUT gateway; SST null does not consume event id. Closes P0-6 as *proof*.
7. **`Pay:WrapKey` mandatory outside Testing.** Delete the git-known default from any non-dev path.
8. **Rewrite stale sentences** (host README, pay-spec header, Taskfile `pay:test` blurb). Grow spec only for doors that exist.
9. **`:5179` poll after Stripe return; `verifying` state.** Never treat success URL as paid.
10. **Do not start CHIP, a factory, Hub cutover, refunds, or LHDN in the same slice as 1–9.** After those are boring in dogfood, CHIP HTTP extract is action 11: `ChipHosted` + one switch arm + Brand ID + RSA verify + capability `hosted_link`. No `IPaymentGatewayAdapter`.

**Do not** add `payment_intent.succeeded` fulfill until (2) and a paid-PI unique exist. **Do not** add Billing Portal, Connect fees, `mode=subscription`, or a five-name dropdown.

---

## 9. Demo script that does not require lying

One on **8080** (Hub **off**) → Pay **8081** + Postgres **5435** → merchant **5178** OIDC via **5175** → owner pastes `sk_test_` **and** you set process `Pay:StripeWebhookSecret` + a tunnel to `/v1/webhooks/stripe/{orgId}` → create MYR product → copy `http://localhost:5179/c/{token}` → buyer (no login) → Stripe test card → webhook → `RCPT-` on 5178. Replay the webhook; document count stays 1.

Do **not** claim BYOK webhook secrets, SST, CHIP, renewals, refunds, member-cannot-charge (curl will), or “we replaced Hub.”

---

## 10. Closing

Bar B **code** landed on `main`. The architectural win 011 paid Hub to leave is real: the webhook HTTP handler **is** the fulfillment entry, in one process, with no MediatR bus between cash and the journal.

Bar B **is not closed.** The next work is **money safety on the Stripe rail we already have**, then **one** Malaysian hosted rail as HTTP judgment. Copying the five Hub adapter files into `Lazuar.Pay/Gateways/` would compile in Hub and fail IsolationTests — and it would recreate the lie that five names in a factory means five dogfood loops.

Read `01`–`10` before staffing a port. This parent is the judgment, not the evidence.
