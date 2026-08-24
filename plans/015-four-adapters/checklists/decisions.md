# 015 — Locked decisions

**Filled by:** [a00-align-freeze.md](./a00-align-freeze.md)  
**Evidence:** [`../00-what-must-be-done.md`](../00-what-must-be-done.md), [014](../../014-evals/README.md)  
**Do not change a row without amending A00.**

This **amends** [013/checklists/decisions.md](../../013-prods/checklists/decisions.md) rows **Rails** (“not five adapters”) and **SST** (“fail closed”). Every other 013 lock still binds (8081, IsolationTests, wrap-rails, same-handler, Official Receipt, P60, no MediatR).

| Topic | Lock |
|-------|------|
| Program | Four remaining hosted_link rails on 8081: **chip, billplz, xendit, razorpay**. Stripe already exists and stays. |
| Tax | **Out.** No SST math, no SST throw, no SST merchant field, no LHDN, no Tax Invoice, no VALID. Book `checkout.Amount` as cash + revenue. |
| Active rail | **One** `org_settings.active_provider` per org. Four adapters in **code**. Buyer page has **no** PSP picker. |
| Provider strings | Lowercase in path, PK, JSON: `stripe` \| `chip` \| `billplz` \| `xendit` \| `razorpay`. Not Hub `STRIPE`. |
| Capability this program | All five = `hosted_link`. CHIP vault / off-session is **parked**. Billplz / Xendit / Razorpay never silent-debit. E-mandate false. |
| Verbs | `CreateHostedUrl` + verify webhook + `Fulfillment.FulfillPaidAsync`. No refund, portal, off-session, factory, registrar, DNS fallback. |
| Secrets | Per-org API ciphertext + **webhook ciphertext**. CHIP Brand ID / Billplz Collection ID plaintext. Process `Pay:StripeWebhookSecret` = Stripe **dev fallback** only. |
| Plane B | `POST /v1/webhooks/{provider}/{orgId}`. Empty 400. Bad sig 400. Unique `(org_id, provider, event_id)`. **One DB transaction** with fulfill. |
| Setup ≠ paid | Stripe `mode=setup` / amount 0; CHIP `purchase.preauthorized`; Billplz unpaid; Xendit non-PAID; Razorpay non-`payment.captured`. |
| Event ids | Namespaced (`paid:{id}`). Never bare object id for fail-then-pay. Razorpay prefer `X-Razorpay-Event-Id`. |
| Currency | Fail closed if PSP omits currency. **Do not default MYR.** |
| Email | Required on start for chip / billplz / xendit (and razorpay if API requires customer). No `customer@example.com`. Stripe may stay optional. |
| Seam | No `IPaymentGatewayAdapter`, no `PaymentGatewayFactory`, no MediatR, no Hub `ProjectReference`. Switch of **known** names is allowed. |
| HTTP | CHIP / Billplz / Xendit / Razorpay = `HttpClient`. **No** `Razorpay.Api` unless HTTP is blocked and A00 is amended. Stripe.net stays. |
| Billplz callback | Public **https** origin. Localhost / loopback / `lazuar-local-dev.com` → create **400**. Tunnel for dogfood. |
| Hosts | CHIP `https://gate.chip-in.asia/api/v1/`. Billplz test `https://www.billplz-sandbox.com/api/v3/` live `https://www.billplz.com/api/v3/`. Xendit `https://api.xendit.co`. Razorpay `https://api.razorpay.com/v1/`. |
| Fees / processor tax | Do not book. `unknown ≠ 0`. Razorpay webhook `tax` / `fee` ignored as journal lines. |
| Frontends | Merchant `:5178` staff picker + per-rail fields. Checkout `:5179` no picker, no wallet tiles, verifying poll. |
| Tests | `task pay:test` hermetic. Fake PSP HTTP. No live CHIP/Billplz/Xendit/Razorpay/Zitadel in CI. |
| Tracker | Flip 011/11 only from a phase **Exit**, and only when the job is real on 8081. Do not flip `NP-GW-003` because Stripe exists; flip it when CHIP hosted_link is proven. `NP-LAT-002` may move when Xendit **and** Razorpay hosted_link exist and are labelled reminder-only. |

## Filled in A00 (must not be blank)

| Topic | Value | Notes |
|-------|-------|-------|
| Remaining four | **chip, billplz, xendit, razorpay** | Stripe is rail 0 (already on 8081). |
| Tax | **out** | Strip throw; do not port `SstTaxMath`. |
| One active provider | **yes** | `org_settings.active_provider` |
| Razorpay transport | **HttpClient** | Amend A00 before adding `Razorpay.Api`. |
| CHIP register webhooks | **dashboard paste PEM** | No silent `ChipWebhookRegistrar`. |
| Public Pay callback base | **`Pay:PublicBaseUrl`** | Absolute https used for Billplz `callback_url`. Not Hub `App:ApiBaseUrl`. |
