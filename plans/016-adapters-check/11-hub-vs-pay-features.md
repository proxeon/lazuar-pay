# 11 — Feature cross-check: old Hub vs new Lazuar Pay

**Date:** 24 August 2026  
**Branch:** `feat/016-harden-adapters`  
**HEAD:** `69454123` — `docs(pay): Q10-Q16 spec start body, preview CORS, README harden`  
**Type:** Product feature matrix. **Not** Hub cutover. **Not** a project reference into `apps/lazuar-api`. **Not** a substitute for the per-rail HTTP papers [04](./04-stripe-crosscheck.md)–[08](./08-razorpay-crosscheck.md).

Parent: [00-evaluation.md](./00-evaluation.md) scored **015** at `c621ceba` (HMAC still wrong, start not idempotent). This paper scores **live Pay after 016 harden** on this SHA. Hub Payments README and `PaymentGatewayCapabilities` are Hub authority. Pay live files under `apps/lazuar-pay` are Pay authority.

---

## 0. Verdict

They are **not the same product**.

Hub (`lazuar-api` on **8080**) is a SaaS billing cathedral: Payments + Commerce + Billing + LHDN + CRM + Communications + Messaging + Ops, with Payments as a **cashier that publishes events** and does not book the ledger.

New Pay (`Lazuar.Pay` on **8081**, merchant **:5178**, checkout **:5179**) is a **focused hosted cashier**: one charge, one Official Receipt, buyers are not One humans. Five processor names look like Hub’s five adapters. The job after verify is different.

**Say:** we extracted Hub’s **hosted pay HTTP** into a real cashier.  
**Do not say:** we replaced Hub.

---

## 1. How the two stacks sit

| | Old Hub | New Pay (this SHA) |
|--|---------|---------------------|
| Process | `apps/lazuar-api` **8080** (collides with One) | `apps/lazuar-pay` **8081** |
| Shape | Modular monolith + MediatR + outbox + per-module DbContexts | One host, one `PayDbContext`, `public` schema |
| Identity | Mix of Hub auth, tenant JWT, portal magic links | Staff = **lazuar-one** OIDC. Buyers = **no account** |
| Staff UI | `lazuar-ops` **:3003**, `lazuar-admin` **:5173** | `lazuar-pay-merchant` **:5178** |
| Buyer UI | `lazuar-portal` **:3004** | `lazuar-pay-checkout` **:5179** |
| Isolation | Hub modules | IsolationTests **ban** MediatR, `Modules.`, factory types, `Razorpay.Api`, Hub csproj |
| CORS on 8081 | n/a | Allows 5178/5179 (+ preview 4178/4179). **Denies** 3003/3004 |

Hub Payments README (live):

> Not an accounting ledger. Not a fulfillment engine. It reports that a financial transaction occurred.

Pay **is** the fulfillment engine: verified Plane B → journal + `RCPT-` in the same HTTP request.

---

## 2. Rails — same five names, different job

Hub: `IPaymentGatewayAdapter` + `PaymentGatewayFactory`, gateway types `STRIPE` / `CHIP` / `BILLPLZ` / `XENDIT` / `RAZORPAY`. Five methods: generate, parse, refund, portal, off-session. Billplz/Xendit/Razorpay **fake** the last three.

Pay: five concrete `IHostedRail` classes (`CreateHostedUrlAsync` + `Provider`). Parse is static next to `POST /v1/webhooks/{provider}/{orgId}`. Capability string is always `"hosted_link"`.

| Feature | Hub | New Pay |
|---------|-----|---------|
| Stripe hosted Checkout | Yes | Yes, `mode=payment` |
| CHIP Collect purchases | Yes | Yes, HttpClient, no NuGet |
| Billplz v3 bills | Yes, JSON create + form HMAC | Yes, same HTTP steal |
| Xendit `/v2/invoices` | Yes | Yes, PAID only |
| Razorpay payment links | Yes, `Razorpay.Api` gravity | Yes, HttpClient; Isolation bans the SDK |
| Dispatch | Factory over `IEnumerable` | Switch of five **known** lowercase names |
| Off-session / vault debit | Stripe + CHIP implement; others return false | **Refused.** No method on `IHostedRail` |
| Customer billing portal | Stripe `GenerateCustomerPortalAsync` | **None** |
| API refunds | Stripe/CHIP/Xendit/Razorpay; Billplz false (Payment Order ≠ refund) | **None** (`parked-refunds`) |
| CHIP webhook registrar | Silent `POST gate.chip-in.asia/.../webhooks` on PUT | **None.** Ada pastes PEM |
| Billplz `PublicDnsFallback` | UDP A-record / `lazuar-local-dev.com` | **Refuse.** Localhost callback **400** |
| Connect `application_fee` | Hub tests ban it; still gravity | Isolation greps `ApplicationFeeAmount` / `application_fee` / `TransferData` |
| E-mandate / FPX auto-debit | `SupportsEmandate` always **false** | Same refuse; CHIP `force_recurring` absent |
| Wallets on **our** page | Capability flags; not a Pay hop-1 tile product | Checkout `locks.test` forbids GrabPay/TnG/FPX/PAN |
| Stripe `mode=setup` as paid | Hub maps some setup to `PAYMENT_COMPLETED` amount 0 | **Ignored** `setup_or_zero` |
| CHIP `purchase.preauthorized` as paid | Hub test locks vault-as-`PAYMENT_COMPLETED` | **Ignored** (inverts Hub) |
| Xendit SETTLED as paid | Hub can book SETTLED | **Ignored** (`settled:{id}`) |
| Billplz unpaid | Hub `PAYMENT_FAILED` verified | **Ignored** `unpaid:{billId}` |
| Razorpay cash event | `payment.captured` | Same; `payment_link.paid` / `order.paid` **not** cash |
| One active rail per tenant | One `GatewayType` | `org_settings.active_provider`; PUT flips it; buyer has **no** picker |

HTTP steal (create hosted URL + verify) is done on this SHA. The Hub **cashier port** (refund / portal / off-session / factory) is not copied and must not be.

---

## 3. Money after a successful pay

| Feature | Hub | New Pay |
|---------|-----|---------|
| What “paid” means | Payments publishes `GatewayPaymentCompletedIntegrationEvent`; Commerce / Billing / Lhdn consume later | Checkout `status=paid` + two-line journal + Official Receipt **in-process** |
| Receipt | Tax Invoice / VALID path via Lhdn | Title **Official Receipt** `RCPT-{MYT year}-#####`. Never Tax Invoice, never VALID |
| SST / MyInvois | `SstTaxMath`, Lhdn module, UBL | **Out.** Amount charged = amount booked. `sst_registered` column unused |
| Fees / processor tax | Parsed when present; `unknown` vs 0 is a live Hub honesty fight | **Not booked.** Razorpay JSON `tax`/`fee` unread |
| Replay | `payments.PaymentWebhookLogs` `(Provider, EventId)` + outbox dead-letter folklore | Unique `(org_id, provider, event_id)` → `{ duplicate: true }` |
| Org bind on webhook | Tenant GUID in path | Path `{orgId}` must equal `checkout.OrgId`; **and** `{provider}` must equal `checkout.Provider` when set |
| Pause charges | Commerce collection pause across jobs | `ChargesPaused`: new start **403**; in-flight paid webhook **409**, does **not** consume the paid event id |
| Idempotent start | Mixed / Commerce session cache | Second `POST /v1/pay/{token}/start` on **open** + stored URL returns that URL; no second PSP HTTP |
| One HMAC (Plane A) | One signs `t={unix},v1={hex}` over `{unix}.{body}` | Pay verifies that dialect (016 W). Body-only uppercase hex is **401** |
| Wrap / BYOK | Hub vault / `DecryptOrPlaintext` gravity | `SecretBox` AES-GCM. Git wrap key **Testing only**. Production/Development require `Pay:WrapKey` |
| Stripe `whsec_` | Per-tenant in config | Per-org `WebhookCiphertext`. Process `Pay:StripeWebhookSecret` is **Testing only** |

---

## 4. Identity, catalog, subscriptions

| Feature | Hub | New Pay |
|---------|-----|---------|
| Staff login | Hub / ops cookies; VIEWER folklore in copy | One `:5175` OIDC PKCE; JWT `access_token` only; roles owner/admin/member |
| Buyer login | Portal magic link; Zitadel humans in places | **No login, no PAN** |
| Org | Hub tenant + Hub user/member tables | One tenant id **is** `org_id`. Isolation bans `organizations` / `users` / `members` tables |
| Products / prices / seats | Full Commerce catalog, MRR, plan change | Tiny MYR product create/list. Checkout amount is **typed**; catalog is a label unless wired |
| Subscriptions / renewals | Commerce workers; Stripe Billing as extra SoT risk | Checkout `Interval = "one_off"`. `mo`/`yr` fulfill branch is dead on the public mint path |
| Dunning, arrears, quotes | Commerce | **None** |
| CRM / client profiles | CRM module | Payer name/email on the checkout row only |
| Email / WhatsApp | Communications + Messaging | `mail_outbox` table exists; **no producer** |
| M2M / machine checkouts | `/integrations/payments/checkouts` | **None** |
| Custom / mark-paid / offline | Commerce admin | **None** |
| Disputes | Commerce disputes list; Stripe dispute parse in Hub adapter | **None** (ignored Stripe types) |

---

## 5. Frontends

| Surface | Hub | New Pay |
|---------|-----|---------|
| Paste PSP keys | Ops payment-config | Merchant PUT; AES-GCM; GET never echoes secret; last4 + `webhook_configured` |
| Rail picker | Ops / tenant config | Staff `<select>` of five names. Buyer **no** picker |
| Pay link | Portal hop-2 | `{VITE_CHECKOUT_ORIGIN}/c/{token}` (default `:5179`) |
| Success URL | Mixed | Hosted default `?status=verifying` is **not** paid. SPA polls; timeout has Refresh |
| Double Pay | Processor can mint two sessions | Host returns stored URL; SPA **Continue to processor** when `started` |
| CHIP PEM | Ops | `<textarea>` |
| Billplz environment | Config / Hub host pick | Staff select; hydrates from GET; callback ≠ redirect (PublicBaseUrl vs CheckoutBaseUrl) |
| Error copy | Hub problem JSON | Merchant/checkout show host `detail` |

---

## 6. Live Pay doors (this SHA)

```
GET  /health  /v1/health
GET  /ready
GET  /v1/whoami                          Bearer → One /me
GET  /v1/orgs/{id}/ready                 dummy ready:true after member
POST /v1/checkouts                       writer
GET  /v1/checkouts/{id}                  member
POST /v1/orgs/{id}/products              writer; MYR
GET  /v1/orgs/{id}/products              member
PUT  /v1/orgs/{id}/gateway               writer; five names
GET  /v1/orgs/{id}/gateway               member; optional ?provider=
GET  /v1/pay/{token}                     public; email_required, started, redirect_url
POST /v1/pay/{token}/start               public; idempotent if already started
POST /v1/webhooks/{provider}/{orgId}     five parsers → Fulfillment
POST /v1/one/webhooks                    Standard Webhooks HMAC → ChargesPaused
GET  /v1/orgs/{id}/payments              member
GET  /v1/orgs/{id}/receipts[/id]         member; Official Receipt
```

Hub Payments doors (not a 1:1 map): `/webhooks/payments/{gatewayType}/{tenantId}`, `/integrations/payments/checkouts`, platform `/payment-config`. Commerce/Billing/Lhdn add the rest of Hub’s product.

---

## 7. What Hub has that Pay must not grow by accident

Parked in [checklist/](./checklist/README.md):

- Factory / `IPaymentGatewayAdapter`
- CHIP registrar
- `PublicDnsFallback`
- LHDN / SST / Tax Invoice
- E-mandate
- Off-session / vault
- Refunds
- Hub cutover (dark `lazuar-api`, retarget ops/portal)
- Sixth PSP name

Also not Pay v1: dunning, MRR, quotes, portal subscriptions, Stripe Billing as SoT, M2M integration checkouts, WhatsApp.

---

## 8. Where Pay is stricter than Hub (keep)

1. Setup / preauthorized / SETTLED / unpaid Billplz / `payment.failed` are **not** cash.
2. No SST auto-seed; no tax journal line.
3. IsolationTests fail the cathedral strings (including registrar/DNS/Connect/LHDN tokens after 016 S17).
4. Start is idempotent; pause does not book and does not eat the paid event id.
5. One HMAC matches One’s signer (`t=,v1=` over `{unix}.{body}`).
6. Buyers never get a PSP dropdown or wallet tiles on `:5179`.
7. Receipt cannot print VALID / Tax Invoice from fulfill.

---

## 9. Where Pay is thinner (honest gaps, not bugs to “fix” with Hub)

1. Lived PSP dogfood (A99.1 / B99) is still a **human** loop. Hermetic 98 tests ≠ Ada paid on a dashboard.
2. Catalog is a label; checkout does not follow product interval.
3. No refunds, disputes, or customer portal.
4. No Pay Dockerfile; `docker-compose.pay.yml` is DB only.
5. `/v1/orgs/{id}/ready` is dummy `ready: true` after member check.
6. InMemory tests still do not prove Postgres transactions; fulfill-throw is a **seam** (`IFulfillPaid` probe), not Npgsql 5435.

---

## 10. How to talk about it

| Say | Do not say |
|-----|------------|
| Focused Pay is a separate cashier on 8081 | We replaced Hub |
| Five hosted_link wraps, one active rail per org | Factory of five / five logos on the buyer page |
| Official Receipt, not an e-invoice | We file MyInvois / compute SST |
| Staff paste BYOK keys; buyers have no One account | We take cards on our page / off-session / e-mandate |
| Success URL is verifying, webhook writes `RCPT-` | Bar B / Pay v1 / Hub dark is done |

Per-rail HTTP steal vs refuse remains in [04](./04-stripe-crosscheck.md)–[08](./08-razorpay-crosscheck.md). This file is the **product** matrix, not those walks.
