# 015 — Implementation checklists (four adapters, no tax)

**Date:** 24 August 2026  
**Style:** Many **small** phase files. One phase ≈ one commit (or a tightly scoped PR).  
**How-to evidence:** parent [`../00-what-must-be-done.md`](../00-what-must-be-done.md). Do not treat this folder as a substitute for that paper or for [014](../../014-evals/README.md).  
**Freeze:** [`decisions.md`](./decisions.md) (locked in A00).

**This program:** hosted_link wraps for **chip, billplz, xendit, razorpay** on `apps/lazuar-pay` **8081**, plus shared-host hardening Stripe already needs. **Tax is out.** It is not Hub parity, not Hub dark, not off-session, not refunds, not LHDN.

013 Bar B (whoami, Postgres, Stripe hosted, `RCPT-`) is **already in the tree**. This folder **amends** 013 “not five adapters” and “SST fail closed.” IsolationTests, wrap-rails honesty, same-handler fulfillment, and P60 (ops/portal off 8081) still bind.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One intent per phase | CHIP + Billplz + Xendit + Razorpay + schema in one tip |
| Fake PSP HTTP in `task pay:test` | Live CHIP / Billplz / Xendit / Razorpay / Zitadel in CI |
| Steal Hub adapter **HTTP judgment** | Copy `IPaymentGatewayAdapter` / factory / MediatR / `Modules/Payments` |
| One active_provider per org | Buyer-facing four-logo PSP picker |
| Two-line GMV journal | SST math, tax line, Tax Invoice, VALID |
| `CreateHostedUrl` + verify webhook | Off-session, portal, refunds, CHIP registrar, DNS fallback |
| Provider strings **lowercase** | Hub `STRIPE` / `CHIP` in the path |

## Track map

```text
A00 Align & freeze
  │
  ├─ Track T Tax strip          T10 → T18     (serial; first)
  ├─ Track S Schema             S10 → S18     (after A00; S17 after S10–S15)
  │
  ├─ Track H Harden Stripe/money  H10 → H25   (after S17; before any new rail HTTP)
  ├─ Track P Provider door        P10 → P27   (after S17 + H10; dispatch)
  │
  ├─ Track C CHIP               C10 → C32     (after H12 + P17; first of the four)
  ├─ Track B Billplz            B10 → B29     (after C19 pattern)
  ├─ Track X Xendit             X10 → X23     (after P17)
  ├─ Track R Razorpay           R10 → R25     (after P17; HttpClient, no SDK)
  │
  ├─ Track U Merchant UI :5178  U10 → U21     (after P14; field sets can follow each rail)
  ├─ Track K Checkout UI :5179  K10 → K17     (after P19)
  │
  └─ Track Q Spec / isolation / copy  Q10 → Q17
A99 Definition of done

Parked (do not start in this program):
  refunds  off-session  LHDN/SST  factory  CHIP registrar  DNS fallback  e-mandate  Hub cutover
```

**Serial:** A00 → T10–T18 and S10–S17 (T and S may overlap after A00) → H10–H25 → P10–P27 → **C first**, then B, then X and R. U field-sets for a rail may land with that rail. Q grows when the door exists. A99 last.

Do **not** start C/B/X/R HTTP before H12 (one TX) and S10 (per-org webhook secret). Those holes are inherited by every new rail.

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| A00 | [a00-align-freeze.md](./a00-align-freeze.md) | Lock 015. Fill decisions. No product code. |
| A99 | [a99-four-adapters-done.md](./a99-four-adapters-done.md) | Honest close. Not Hub dark. Not tax. |

### Track T — Tax strip

| ID | File | Intent |
|----|------|--------|
| T10 | [t10-remove-sst-throw.md](./t10-remove-sst-throw.md) | Delete Fulfillment SST throw. |
| T11 | [t11-stop-seed-sst-on-create.md](./t11-stop-seed-sst-on-create.md) | Checkout create is not a tax registration. |
| T12 | [t12-stop-seed-sst-on-one-webhook.md](./t12-stop-seed-sst-on-one-webhook.md) | Suspend pauses charges, not SST. |
| T13 | [t13-no-tax-journal-line.md](./t13-no-tax-journal-line.md) | Two-line GMV only. No fee-as-0. |
| T14 | [t14-official-receipt-only.md](./t14-official-receipt-only.md) | Title Official Receipt. No VALID. |
| T15 | [t15-no-sst-merchant-field.md](./t15-no-sst-merchant-field.md) | No SST API/UI. |
| T16 | [t16-fulfill-null-sst-still-pays.md](./t16-fulfill-null-sst-still-pays.md) | Hermetic: null SST still mints `RCPT-`. |
| T17 | [t17-no-lhdn-in-host.md](./t17-no-lhdn-in-host.md) | Grep: no LHDN/UBL in Pay host. |
| T18 | [t18-not-einvoice-copy.md](./t18-not-einvoice-copy.md) | Honesty copy: not an e-invoice. |

### Track S — Schema

| ID | File | Intent |
|----|------|--------|
| S10 | [s10-webhook-ciphertext.md](./s10-webhook-ciphertext.md) | Per-org encrypted webhook secret. |
| S11 | [s11-public-merchant-id.md](./s11-public-merchant-id.md) | CHIP Brand ID / Billplz Collection ID. |
| S12 | [s12-environment-test-live.md](./s12-environment-test-live.md) | Billplz test\|live host. |
| S13 | [s13-active-provider.md](./s13-active-provider.md) | One `active_provider` per org. |
| S14 | [s14-checkout-provider.md](./s14-checkout-provider.md) | `checkouts.provider` set on start. |
| S15 | [s15-checkout-provider-session-id.md](./s15-checkout-provider-session-id.md) | Persist purchase/bill/invoice/`cs_` id. |
| S16 | [s16-secretbox-webhook.md](./s16-secretbox-webhook.md) | SecretBox wraps webhook secrets. |
| S17 | [s17-one-migration.md](./s17-one-migration.md) | One EF migration for S10–S15. |
| S18 | [s18-get-never-ciphertext.md](./s18-get-never-ciphertext.md) | GET metadata only. |

### Track H — Harden Stripe / money safety

| ID | File | Intent |
|----|------|--------|
| H10 | [h10-stripe-whsec-from-row.md](./h10-stripe-whsec-from-row.md) | Verify with org `whsec_`, not only process env. |
| H11 | [h11-stripe-whsec-dev-fallback.md](./h11-stripe-whsec-dev-fallback.md) | Process env = dev fallback; Production 503 if row empty. |
| H12 | [h12-one-transaction.md](./h12-one-transaction.md) | Unique insert + fulfill in one commit. |
| H13 | [h13-orgid-bind.md](./h13-orgid-bind.md) | `checkout.OrgId` must equal path `{orgId}`. |
| H14 | [h14-amount-match.md](./h14-amount-match.md) | PSP amount vs checkout amount. |
| H15 | [h15-ignored-events.md](./h15-ignored-events.md) | Setup/ignore must not pay; unique grain honest. |
| H16 | [h16-wrap-key-no-git-default.md](./h16-wrap-key-no-git-default.md) | No git-known wrap key outside Testing. |
| H17 | [h17-writer-create-checkout.md](./h17-writer-create-checkout.md) | `POST /v1/checkouts` is writer. |
| H18 | [h18-member-cannot-put-gateway.md](./h18-member-cannot-put-gateway.md) | Member 403 on PUT keys. |
| H19 | [h19-setup-not-paid-test.md](./h19-setup-not-paid-test.md) | Hermetic Stripe `mode=setup`. |
| H20 | [h20-zero-amount-not-paid-test.md](./h20-zero-amount-not-paid-test.md) | Hermetic amount 0. |
| H21 | [h21-isolation-adapter-types.md](./h21-isolation-adapter-types.md) | Ban Hub adapter type names in Pay src. |
| H22 | [h22-no-connect-fee-grep.md](./h22-no-connect-fee-grep.md) | No `application_fee_amount`. |
| H23 | [h23-audit-on-key-put.md](./h23-audit-on-key-put.md) | Audit row on gateway PUT. |
| H24 | [h24-unique-violation-200.md](./h24-unique-violation-200.md) | Race on unique key → 200 duplicate. |
| H25 | [h25-fulfill-throw-retries.md](./h25-fulfill-throw-retries.md) | Fulfill throw rolls back event id. |

### Track P — Provider door (PUT / GET / start / switch)

| ID | File | Intent |
|----|------|--------|
| P10 | [p10-allowlist-five.md](./p10-allowlist-five.md) | Lowercase allow-list of five names. |
| P11 | [p11-put-provider-fields.md](./p11-put-provider-fields.md) | PUT body grows per-rail fields. |
| P12 | [p12-put-stripe-secret-and-whsec.md](./p12-put-stripe-secret-and-whsec.md) | Stripe PUT requires `sk_` + `whsec_`. |
| P13 | [p13-put-sets-active-provider.md](./p13-put-sets-active-provider.md) | PUT sets `active_provider`. |
| P14 | [p14-get-active-not-always-stripe.md](./p14-get-active-not-always-stripe.md) | GET describes the active rail. |
| P15 | [p15-get-optional-query-provider.md](./p15-get-optional-query-provider.md) | Optional `?provider=` |
| P16 | [p16-capability-hosted-link.md](./p16-capability-hosted-link.md) | JSON `capability: hosted_link`. |
| P17 | [p17-start-dispatch.md](./p17-start-dispatch.md) | Public start calls the active rail. |
| P18 | [p18-start-persist-provider.md](./p18-start-persist-provider.md) | Persist provider + URL + session id. |
| P19 | [p19-start-email-by-rail.md](./p19-start-email-by-rail.md) | Email required for CHIP/Billplz/Xendit. |
| P20 | [p20-placeholder-email-400.md](./p20-placeholder-email-400.md) | `customer@example.com` 400. |
| P21 | [p21-webhook-switch.md](./p21-webhook-switch.md) | Webhook switches on known names. |
| P22 | [p22-unknown-provider-400.md](./p22-unknown-provider-400.md) | Unknown `{provider}` 400. |
| P23 | [p23-empty-body-400-all.md](./p23-empty-body-400-all.md) | Empty body 400 on every rail. |
| P24 | [p24-rail-not-configured-400.md](./p24-rail-not-configured-400.md) | Missing creds 400. |
| P25 | [p25-no-ipaymentgatewayadapter.md](./p25-no-ipaymentgatewayadapter.md) | Do not add Hub’s five-method port. |
| P26 | [p26-no-factory.md](./p26-no-factory.md) | No `PaymentGatewayFactory`. |
| P27 | [p27-hosted-rail-two-methods.md](./p27-hosted-rail-two-methods.md) | Small CreateHostedUrl seam when second class exists. |

### Track C — CHIP Collect

| ID | File | Intent |
|----|------|--------|
| C10 | [c10-chip-class.md](./c10-chip-class.md) | `ChipHosted` next to `StripeHosted`. |
| C11 | [c11-chip-put-fields.md](./c11-chip-put-fields.md) | PUT chip: Bearer + Brand ID + PEM. |
| C12 | [c12-chip-purchases-http.md](./c12-chip-purchases-http.md) | `POST …/purchases/` |
| C13 | [c13-chip-cents.md](./c13-chip-cents.md) | Price in cents, AwayFromZero. |
| C14 | [c14-chip-metadata.md](./c14-chip-metadata.md) | metadata `checkout_id` + `org_id`. |
| C15 | [c15-chip-no-recurring-flags.md](./c15-chip-no-recurring-flags.md) | No `force_recurring` / `skip_capture`. |
| C16 | [c16-chip-redirect-urls.md](./c16-chip-redirect-urls.md) | success/failure/cancel redirects. |
| C17 | [c17-chip-start-mock.md](./c17-chip-start-mock.md) | Hermetic start → `checkout_url`. |
| C18 | [c18-chip-rsa-verify.md](./c18-chip-rsa-verify.md) | `X-Signature` RSA-PEM. |
| C19 | [c19-chip-paid-fulfill.md](./c19-chip-paid-fulfill.md) | `purchase.paid` amount>0 → fulfill. |
| C20 | [c20-chip-event-id.md](./c20-chip-event-id.md) | Event id `paid:{purchaseId}`. |
| C21 | [c21-chip-preauthorized-ignore.md](./c21-chip-preauthorized-ignore.md) | preauthorized is not paid. |
| C22 | [c22-chip-failure-ignore.md](./c22-chip-failure-ignore.md) | `purchase.payment_failure` no fulfill. |
| C23 | [c23-chip-stable-purchase-id.md](./c23-chip-stable-purchase-id.md) | Nested `purchase.id` then root `id`. |
| C24 | [c24-chip-currency-fail-closed.md](./c24-chip-currency-fail-closed.md) | Missing currency: do not default MYR. |
| C25 | [c25-chip-replay.md](./c25-chip-replay.md) | Replay no second `RCPT-`. |
| C26 | [c26-chip-empty-400.md](./c26-chip-empty-400.md) | Empty body 400. |
| C27 | [c27-chip-bad-sig-400.md](./c27-chip-bad-sig-400.md) | Bad RSA 400. |
| C28 | [c28-chip-no-registrar.md](./c28-chip-no-registrar.md) | No silent `POST /webhooks/`. |
| C29 | [c29-chip-no-nuget.md](./c29-chip-no-nuget.md) | HttpClient only. |
| C30 | [c30-chip-email-required.md](./c30-chip-email-required.md) | Start without email 400. |
| C31 | [c31-chip-brand-id-required.md](./c31-chip-brand-id-required.md) | Missing Brand ID 400. |
| C32 | [c32-chip-webhook-tests.md](./c32-chip-webhook-tests.md) | Bundle remaining CHIP hermetic cases. |

### Track B — Billplz

| ID | File | Intent |
|----|------|--------|
| B10 | [b10-billplz-class.md](./b10-billplz-class.md) | `BillplzHosted` class. |
| B11 | [b11-billplz-put-fields.md](./b11-billplz-put-fields.md) | Secret + Collection ID + X-Signature + env. |
| B12 | [b12-billplz-hosts.md](./b12-billplz-hosts.md) | Sandbox vs www from `environment`. |
| B13 | [b13-billplz-bills-http.md](./b13-billplz-bills-http.md) | `POST …/bills` Basic auth. |
| B14 | [b14-billplz-callback-url.md](./b14-billplz-callback-url.md) | `/v1/webhooks/billplz/{orgId}?checkout_id=` |
| B15 | [b15-billplz-localhost-400.md](./b15-billplz-localhost-400.md) | Loopback callback 400. |
| B16 | [b16-billplz-checkout-id-query.md](./b16-billplz-checkout-id-query.md) | Join via query `checkout_id`. |
| B17 | [b17-billplz-reference-1.md](./b17-billplz-reference-1.md) | `reference_1` = checkout id. |
| B18 | [b18-billplz-form-hmac.md](./b18-billplz-form-hmac.md) | Form `x_signature` HMAC. |
| B19 | [b19-billplz-hmac-extra-fields.md](./b19-billplz-hmac-extra-fields.md) | With-extra then without-extra. |
| B20 | [b20-billplz-paid-fulfill.md](./b20-billplz-paid-fulfill.md) | `paid=true` / `state=paid`. |
| B21 | [b21-billplz-unpaid-ignore.md](./b21-billplz-unpaid-ignore.md) | Unpaid callback no fulfill. |
| B22 | [b22-billplz-event-id.md](./b22-billplz-event-id.md) | `paid:{billId}`. |
| B23 | [b23-billplz-no-dns-fallback.md](./b23-billplz-no-dns-fallback.md) | Do not port `PublicDnsFallback`. |
| B24 | [b24-billplz-no-refund-api.md](./b24-billplz-no-refund-api.md) | No Payment Order as refund. |
| B25 | [b25-billplz-no-offsession.md](./b25-billplz-no-offsession.md) | Never silent debit. |
| B26 | [b26-billplz-email-required.md](./b26-billplz-email-required.md) | Email required. |
| B27 | [b27-billplz-collection-required.md](./b27-billplz-collection-required.md) | Collection ID required. |
| B28 | [b28-billplz-tests.md](./b28-billplz-tests.md) | Hermetic form + HMAC cases. |
| B29 | [b29-billplz-tunnel-runbook.md](./b29-billplz-tunnel-runbook.md) | Public HTTPS tunnel for dogfood. |

### Track X — Xendit

| ID | File | Intent |
|----|------|--------|
| X10 | [x10-xendit-class.md](./x10-xendit-class.md) | `XenditHosted` class. |
| X11 | [x11-xendit-put-fields.md](./x11-xendit-put-fields.md) | Secret + callback token. |
| X12 | [x12-xendit-invoices-http.md](./x12-xendit-invoices-http.md) | `POST /v2/invoices`. |
| X13 | [x13-xendit-no-setup-future.md](./x13-xendit-no-setup-future.md) | Discard vault flags. |
| X14 | [x14-xendit-callback-token.md](./x14-xendit-callback-token.md) | `x-callback-token` fixed-time. |
| X15 | [x15-xendit-paid-fulfill.md](./x15-xendit-paid-fulfill.md) | Status PAID only. |
| X16 | [x16-xendit-settled-ignore.md](./x16-xendit-settled-ignore.md) | SETTLED does not second-journal. |
| X17 | [x17-xendit-pending-ignore.md](./x17-xendit-pending-ignore.md) | PENDING/EXPIRED/FAILED ignore. |
| X18 | [x18-xendit-event-id.md](./x18-xendit-event-id.md) | `paid:{invoiceId}`. |
| X19 | [x19-xendit-currency-fail-closed.md](./x19-xendit-currency-fail-closed.md) | No default MYR. |
| X20 | [x20-xendit-no-wallets-on-pay.md](./x20-xendit-no-wallets-on-pay.md) | No GrabPay tiles on `:5179`. |
| X21 | [x21-xendit-no-xenplatform.md](./x21-xendit-no-xenplatform.md) | No xenPlatform / Connect-like split. |
| X22 | [x22-xendit-email-required.md](./x22-xendit-email-required.md) | Email required. |
| X23 | [x23-xendit-tests.md](./x23-xendit-tests.md) | Hermetic invoice + token cases. |

### Track R — Razorpay

| ID | File | Intent |
|----|------|--------|
| R10 | [r10-razorpay-class.md](./r10-razorpay-class.md) | `RazorpayHosted` class. |
| R11 | [r11-razorpay-put-fields.md](./r11-razorpay-put-fields.md) | `key_id:key_secret` + webhook secret. |
| R12 | [r12-razorpay-key-split.md](./r12-razorpay-key-split.md) | Split on `:` for Basic auth. |
| R13 | [r13-razorpay-payment-links-http.md](./r13-razorpay-payment-links-http.md) | `POST /v1/payment_links`. |
| R14 | [r14-razorpay-no-official-sdk.md](./r14-razorpay-no-official-sdk.md) | No `Razorpay.Api` package. |
| R15 | [r15-razorpay-no-setup-future.md](./r15-razorpay-no-setup-future.md) | Discard SetupFutureUsage. |
| R16 | [r16-razorpay-hmac.md](./r16-razorpay-hmac.md) | `X-Razorpay-Signature` HMAC-SHA256 raw body. |
| R17 | [r17-razorpay-captured-fulfill.md](./r17-razorpay-captured-fulfill.md) | `payment.captured` only. |
| R18 | [r18-razorpay-failed-ignore.md](./r18-razorpay-failed-ignore.md) | `payment.failed` no fulfill. |
| R19 | [r19-razorpay-event-id.md](./r19-razorpay-event-id.md) | Header Event-Id or `captured:{pay_}`. |
| R20 | [r20-razorpay-currency-fail-closed.md](./r20-razorpay-currency-fail-closed.md) | No default MYR. |
| R21 | [r21-razorpay-no-book-tax.md](./r21-razorpay-no-book-tax.md) | Ignore JSON `tax` / `fee`. |
| R22 | [r22-razorpay-no-emandate.md](./r22-razorpay-no-emandate.md) | No registration / e-mandate links. |
| R23 | [r23-razorpay-no-offsession.md](./r23-razorpay-no-offsession.md) | Dead pipe stays dead. |
| R24 | [r24-razorpay-email.md](./r24-razorpay-email.md) | Customer on payment link as Hub did. |
| R25 | [r25-razorpay-tests.md](./r25-razorpay-tests.md) | Hermetic HMAC + captured cases. |

### Track U — Merchant UI `:5178`

| ID | File | Intent |
|----|------|--------|
| U10 | [u10-provider-select.md](./u10-provider-select.md) | Staff provider select. |
| U11 | [u11-stripe-fields.md](./u11-stripe-fields.md) | `sk_` + `whsec_`. |
| U12 | [u12-chip-fields.md](./u12-chip-fields.md) | Bearer + Brand ID + PEM. |
| U13 | [u13-billplz-fields.md](./u13-billplz-fields.md) | Secret + Collection + X-Signature + env. |
| U14 | [u14-xendit-fields.md](./u14-xendit-fields.md) | Secret + callback token. |
| U15 | [u15-razorpay-fields.md](./u15-razorpay-fields.md) | key_id, key_secret, webhook secret. |
| U16 | [u16-writer-only-paste.md](./u16-writer-only-paste.md) | Hide paste unless owner/admin. |
| U17 | [u17-member-sees-last4.md](./u17-member-sees-last4.md) | Member sees metadata only. |
| U18 | [u18-no-five-logo-wall.md](./u18-no-five-logo-wall.md) | No FPX/GrabPay logo wall. |
| U19 | [u19-wrap-copy.md](./u19-wrap-copy.md) | Honest hosted_link / reminder copy. |
| U20 | [u20-no-vite-secrets.md](./u20-no-vite-secrets.md) | No `sk_` / PEM in `VITE_*`. |
| U21 | [u21-active-provider-shown.md](./u21-active-provider-shown.md) | Show which rail is active. |

### Track K — Checkout UI `:5179`

| ID | File | Intent |
|----|------|--------|
| K10 | [k10-no-provider-picker.md](./k10-no-provider-picker.md) | Buyer does not pick a PSP. |
| K11 | [k11-email-required-by-rail.md](./k11-email-required-by-rail.md) | Require email when the rail needs it. |
| K12 | [k12-no-wallet-tiles.md](./k12-no-wallet-tiles.md) | No GrabPay/TnG/FPX tiles. |
| K13 | [k13-verifying-poll.md](./k13-verifying-poll.md) | Poll public GET after return. |
| K14 | [k14-success-url-not-paid.md](./k14-success-url-not-paid.md) | `?status=verifying` is not paid. |
| K15 | [k15-no-oidc.md](./k15-no-oidc.md) | Still no Zitadel on 5179. |
| K16 | [k16-503-rail.md](./k16-503-rail.md) | Rail not configured honesty. |
| K17 | [k17-no-pan.md](./k17-no-pan.md) | No card number fields. |

### Track Q — Spec / isolation / copy

| ID | File | Intent |
|----|------|--------|
| Q10 | [q10-pay-spec-gateway.md](./q10-pay-spec-gateway.md) | `pay-spec` PUT/GET gateway. |
| Q11 | [q11-pay-spec-webhooks.md](./q11-pay-spec-webhooks.md) | Spec lists five provider names honestly. |
| Q12 | [q12-pay-spec-not-fixture.md](./q12-pay-spec-not-fixture.md) | Stop saying checkout is a fixture. |
| Q13 | [q13-host-readme.md](./q13-host-readme.md) | Host README matches Postgres + rails. |
| Q14 | [q14-taskfile-blurb.md](./q14-taskfile-blurb.md) | `pay:test` description is not “health + isolation” only. |
| Q15 | [q15-hermetic-ci.md](./q15-hermetic-ci.md) | CI still hermetic. |
| Q16 | [q16-no-hub-gen.md](./q16-no-hub-gen.md) | Do not add pay-spec to Hub `task gen`. |
| Q17 | [q17-cors-still-denies-ops.md](./q17-cors-still-denies-ops.md) | CORS still denies 3003/3004. |

### Parked

| ID | File | Intent |
|----|------|--------|
| — | [parked-refunds.md](./parked-refunds.md) | Full refund + journal reverse once. |
| — | [parked-offsession.md](./parked-offsession.md) | Stripe/CHIP vault auto-debit. |
| — | [parked-lhdn-sst.md](./parked-lhdn-sst.md) | SST math and MyInvois. |
| — | [parked-factory.md](./parked-factory.md) | `IPaymentGatewayAdapter` factory of five. |
| — | [parked-chip-registrar.md](./parked-chip-registrar.md) | Silent CHIP webhook register. |
| — | [parked-dns-fallback.md](./parked-dns-fallback.md) | `PublicDnsFallback`. |
| — | [parked-emandate.md](./parked-emandate.md) | Homemade FPX e-mandate. |
| — | [parked-hub-cutover.md](./parked-hub-cutover.md) | Kill Hub compose. |

## How to execute

1. Complete **A00** and fill [`decisions.md`](./decisions.md).
2. **T10→T18** (tax out) and **S10→S17** (schema). S17 after S10–S15.
3. **H10→H25** before any CHIP HTTP. Stripe dogfood must still mint `RCPT-`.
4. **P10→P27** so PUT/GET/start/webhook can name five providers without a factory.
5. **C10→C32** first remaining rail. Then **B**, then **X** and **R**.
6. **U** field-sets with the matching rail. **K13** verifying poll as soon as start redirects (can be with H).
7. **Q** when the door exists. **A99** only when C/B/X/R hermetic paid+replay+not-paid exist and tax is still out.
8. Flip [011/11](../../011-new-lazuar-pay/11-checklist.md) only for IDs listed in a phase **Exit**, and only when a human can do the job on 8081.
9. Do not start parked files in the same PR as 015 phases.
