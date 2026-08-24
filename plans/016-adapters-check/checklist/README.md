# 016 — Implementation checklists (harden five wraps)

**Date:** 24 August 2026  
**Style:** Many **small** phase files. One phase ≈ one commit (or a tightly scoped PR).  
**How-to evidence:** parent [`../00-evaluation.md`](../00-evaluation.md). Test names: [`../09-tests-inventory.md`](../09-tests-inventory.md) §9–§10. Ranked bugs: [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md). Do not treat this folder as a substitute for those papers.  
**Freeze:** [`decisions.md`](./decisions.md) (locked in A00).

**This program:** money-hardening of the five `hosted_link` rails already on `apps/lazuar-pay` **8081**, then the hermetic tests 015 ticked without writing, then SPA honesty that blocks dogfood. **It is not a sixth rail. It is not a factory. It is not tax.**

015 landed CHIP / Billplz / Xendit / Razorpay as HTTP extracts. IsolationTests, wrap-rails, same-handler fulfillment, Official Receipt, P60 (ops/portal off 8081) still bind. 015 `[x]` is a **map**, not proof — several exits were code-only.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| Product money first (Beat 1), then tests that lock it (Beat 2), then SPA (Beat 3) | Seventy tests that freeze today’s double-start bug |
| One intent per phase | Start idempotency + One HMAC + Razorpay join in one tip |
| Fake PSP HTTP in `task pay:test` | Live CHIP / Billplz / Xendit / Razorpay / Zitadel in CI |
| Steal One HMAC **judgment** | Copy `Modules.One` / `OutboundWebhookSignature.cs` as a type |
| Real transaction for fulfill-throw | Claim one-TX from EF InMemory |
| Show host `detail` on 400/503 | One conflated buyer sentence for every failure |
| Flip 011/11 only from a lived Exit | Flip `NP-GW-003` from hermetic CHIP |

## Track map

```text
A00 Align & freeze
  │
  ├─ Beat 1  money (serial I first; W/Y/J/D/E/L may overlap after I10)
  │    Track I  Idempotent start          I10 → I18
  │    Track W  One HMAC + pause          W10 → W24
  │    Track Y  Webhook rail bind         Y10 → Y12
  │    Track J  Razorpay join             J10 → J16
  │    Track D  Units + currency          D10 → D20
  │    Track E  Env secrets               E10 → E16
  │    Track L  Checkout origin           L10 → L18
  │
  ├─ Beat 2  prove (after the Beat 1 phase each test locks)
  │    Track G  Prove Beat 1              G10 → G16
  │    Track S  Strengthen existing       S10 → S18
  │    Track F  Fill 015-lied tests     F00 index, then fs/fc/fb/fx/fr/fg/fp/fi
  │
  ├─ Beat 3  SPA honesty (after I15 + L10)
  │    Track M  Merchant :5178            M10 → M22
  │    Track K  Checkout :5179            K10 → K18
  │    Track Q  Spec / CORS / copy        Q10 → Q16
  │
A99 Definition of done

Parked (do not start in this program):
  factory  CHIP registrar  DNS fallback  LHDN/SST  e-mandate
  off-session  refunds  Hub cutover  sixth rail
```

**Serial:** A00 → **I10** (cheapest cash) → remaining Beat 1 (W/Y/J/D/E/L may run in parallel after I10) → **G10–G16** for those product exits → **S10–S18** strengthen before cloning paid tests → **F** fill → **M/K** (need I15 + L) → Q → A99.

Do **not** start Track F as a seventy-method dump before I10 and G14. Do **not** staff parked files because a test is “easy.”

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| A00 | [a00-align-freeze.md](./a00-align-freeze.md) | Lock 016. Fill decisions. No product code. |
| A99 | [a99-harden-done.md](./a99-harden-done.md) | Honest close. Not Hub dark. Not five lived loops. |

### Track I — Idempotent start (P0-A)

| ID | File | Intent |
|----|------|--------|
| I10 | [i10-return-existing-hosted-url.md](./i10-return-existing-hosted-url.md) | Second start returns stored `PspRedirectUrl`. |
| I11 | [i11-skip-create-when-started.md](./i11-skip-create-when-started.md) | Do not call `CreateHostedUrlAsync` again. |
| I12 | [i12-do-not-overwrite-session-id.md](./i12-do-not-overwrite-session-id.md) | Keep first `ProviderSessionId`. |
| I13 | [i13-paid-expired-still-409.md](./i13-paid-expired-still-409.md) | Idempotency is for `open` only. |
| I14 | [i14-paused-still-403.md](./i14-paused-still-403.md) | Resume must not bypass pause. |
| I15 | [i15-public-get-started.md](./i15-public-get-started.md) | Public GET says `started` + `redirect_url`. |
| I16 | [i16-stripe-idempotency-key.md](./i16-stripe-idempotency-key.md) | Session create key `lazuar-checkout:{id}`. |
| I17 | [i17-remove-dead-stripe-default.md](./i17-remove-dead-stripe-default.md) | Drop Start `_ => stripe`. |
| I18 | [i18-savechanges-after-psp.md](./i18-savechanges-after-psp.md) | Document persist-after-PSP; do not invert HTTP. |

### Track W — One HMAC + pause-on-fulfill (P0-B)

| ID | File | Intent |
|----|------|--------|
| W10 | [w10-steal-one-signer-judgment.md](./w10-steal-one-signer-judgment.md) | Read One signer. Do not copy the type. |
| W11 | [w11-parse-t-v1-header.md](./w11-parse-t-v1-header.md) | Header `t={unix},v1={hex}`. |
| W12 | [w12-signed-payload-unix-body.md](./w12-signed-payload-unix-body.md) | HMAC over `{unix}.{body}`. |
| W13 | [w13-lowercase-hex-fixed-time.md](./w13-lowercase-hex-fixed-time.md) | Lowercase hex, fixed-time. |
| W14 | [w14-skew-window.md](./w14-skew-window.md) | Reject stale `t` (~300s). |
| W15 | [w15-invalid-header-401.md](./w15-invalid-header-401.md) | Missing/garbage header 401. |
| W16 | [w16-old-dialect-401.md](./w16-old-dialect-401.md) | Body-only uppercase hex 401. |
| W17 | [w17-tenant-id-or-org-id.md](./w17-tenant-id-or-org-id.md) | Read `tenant_id` and `org_id`. |
| W18 | [w18-suspended-sets-paused.md](./w18-suspended-sets-paused.md) | `tenant.suspended` → `ChargesPaused`. |
| W19 | [w19-reactivated-clears-paused.md](./w19-reactivated-clears-paused.md) | Keep `tenant.reactivated`. |
| W20 | [w20-missing-one-secret-503.md](./w20-missing-one-secret-503.md) | Empty `Pay:OneWebhookSecret` 503. |
| W21 | [w21-fulfill-reads-paused.md](./w21-fulfill-reads-paused.md) | Fulfill does not book when paused. |
| W22 | [w22-paused-does-not-consume-paid-id.md](./w22-paused-does-not-consume-paid-id.md) | No paid unique insert while paused. |
| W23 | [w23-hmac-vector-test.md](./w23-hmac-vector-test.md) | Hermetic Standard Webhooks vector. |
| W24 | [w24-paused-webhook-no-receipt-test.md](./w24-paused-webhook-no-receipt-test.md) | Valid paid webhook, paused org, zero `RCPT-`. |

### Track Y — Webhook rail bind

| ID | File | Intent |
|----|------|--------|
| Y10 | [y10-path-matches-checkout-provider.md](./y10-path-matches-checkout-provider.md) | Path `{provider}` must match `checkout.Provider` when set. |
| Y11 | [y11-never-started-checkout.md](./y11-never-started-checkout.md) | Null provider on checkout → 400 (not a second rail). |
| Y12 | [y12-cross-rail-webhook-test.md](./y12-cross-rail-webhook-test.md) | CHIP checkout + Stripe path 400. |

### Track J — Razorpay join (P0-C)

| ID | File | Intent |
|----|------|--------|
| J10 | [j10-keep-notes-checkout-id.md](./j10-keep-notes-checkout-id.md) | Still stamp and read notes. |
| J11 | [j11-fallback-plink-session.md](./j11-fallback-plink-session.md) | Join via stored `ProviderSessionId`. |
| J12 | [j12-captured-without-notes.md](./j12-captured-without-notes.md) | No notes + no plink match → 400, no silent pay. |
| J13 | [j13-ignore-payment-link-paid.md](./j13-ignore-payment-link-paid.md) | `payment_link.paid` is not cash. |
| J14 | [j14-ignore-order-paid.md](./j14-ignore-order-paid.md) | `order.paid` is not cash. |
| J15 | [j15-other-event-id-not-bare-type.md](./j15-other-event-id-not-bare-type.md) | Ignored events namespaced. |
| J16 | [j16-captured-without-notes-test.md](./j16-captured-without-notes-test.md) | Hermetic: notes omitted. |

### Track D — Units + currency fail-closed (P0-D)

| ID | File | Intent |
|----|------|--------|
| D10 | [d10-chip-total-is-cents.md](./d10-chip-total-is-cents.md) | Parser comment + fixture 1000 = RM10. |
| D11 | [d11-xendit-amount-is-major.md](./d11-xendit-amount-is-major.md) | `paid_amount` major then `ToMinor`. |
| D12 | [d12-billplz-paid-amount-is-sen.md](./d12-billplz-paid-amount-is-sen.md) | Form `paid_amount` is minor. |
| D13 | [d13-stripe-amount-total-cents.md](./d13-stripe-amount-total-cents.md) | `AmountTotal` cents. |
| D14 | [d14-razorpay-amount-is-minor.md](./d14-razorpay-amount-is-minor.md) | Entity `amount` already minor. |
| D15 | [d15-billplz-no-default-myr.md](./d15-billplz-no-default-myr.md) | Stop hardcoding `Currency = "MYR"`. |
| D16 | [d16-stripe-missing-currency-refuse.md](./d16-stripe-missing-currency-refuse.md) | Null currency does not skip the check. |
| D17 | [d17-mismatch-no-event-row.md](./d17-mismatch-no-event-row.md) | 400 amount/currency → no unique insert. |
| D18 | [d18-xendit-hash-first-token.md](./d18-xendit-hash-first-token.md) | Compare hashes, not raw length. |
| D19 | [d19-do-not-invent-myr-on-webhook.md](./d19-do-not-invent-myr-on-webhook.md) | Checkout default MYR ≠ webhook default. |
| D20 | [d20-json-unit-fixtures.md](./d20-json-unit-fixtures.md) | Checked-in JSON per rail (FakePsp). |

### Track E — Env secrets (P0-E / wrap)

| ID | File | Intent |
|----|------|--------|
| E10 | [e10-stripe-fallback-testing-only.md](./e10-stripe-fallback-testing-only.md) | Process `whsec_` only in Testing. |
| E11 | [e11-development-empty-ciphertext-503.md](./e11-development-empty-ciphertext-503.md) | Development does not use process env. |
| E12 | [e12-production-empty-ciphertext-503.md](./e12-production-empty-ciphertext-503.md) | Production 503 even if process env set. |
| E13 | [e13-wrap-key-testing-only-git.md](./e13-wrap-key-testing-only-git.md) | Git wrap string only in Testing. |
| E14 | [e14-production-wrap-key-required.md](./e14-production-wrap-key-required.md) | Missing `Pay:WrapKey` throws. |
| E15 | [e15-testing-wrap-still-works.md](./e15-testing-wrap-still-works.md) | Tests stay green without a committed key. |
| E16 | [e16-missing-secret-503-after-e10.md](./e16-missing-secret-503-after-e10.md) | Existing 503 test still true in Testing. |

### Track L — Checkout origin (P1-5)

| ID | File | Intent |
|----|------|--------|
| L10 | [l10-checkout-base-url-config.md](./l10-checkout-base-url-config.md) | `Pay:CheckoutBaseUrl`. |
| L11 | [l11-hosted-default-helper.md](./l11-hosted-default-helper.md) | One helper for success/cancel defaults. |
| L12 | [l12-stripe-chip-xendit-razorpay-defaults.md](./l12-stripe-chip-xendit-razorpay-defaults.md) | Four rails use the helper. |
| L13 | [l13-billplz-redirect-not-callback.md](./l13-billplz-redirect-not-callback.md) | Redirect = checkout origin; callback = PublicBaseUrl. |
| L14 | [l14-merchant-vite-checkout-origin.md](./l14-merchant-vite-checkout-origin.md) | `VITE_CHECKOUT_ORIGIN` for minted links. |
| L15 | [l15-optional-success-url-on-mint.md](./l15-optional-success-url-on-mint.md) | SPA may send `success_url`. |
| L16 | [l16-env-example-checkout-base.md](./l16-env-example-checkout-base.md) | Host + merchant `.env.example`. |
| L17 | [l17-readme-checkout-origin.md](./l17-readme-checkout-origin.md) | README names both bases. |
| L18 | [l18-tests-override-checkout-base.md](./l18-tests-override-checkout-base.md) | Factory sets a test origin. |

### Track G — Prove Beat 1

| ID | File | Intent |
|----|------|--------|
| G10 | [g10-inmemory-is-not-tx-proof.md](./g10-inmemory-is-not-tx-proof.md) | Comment the H25 skip rule. |
| G11 | [g11-fulfill-throw-seam.md](./g11-fulfill-throw-seam.md) | Tiny `IFulfillPaid` or real TX store. |
| G12 | [g12-fulfill-throw-5xx-no-event.md](./g12-fulfill-throw-5xx-no-event.md) | First POST 5xx, no unique row. |
| G13 | [g13-fulfill-retry-pays.md](./g13-fulfill-retry-pays.md) | Second POST one `RCPT-`. |
| G14 | [g14-start-twice-one-psp-http.md](./g14-start-twice-one-psp-http.md) | FakePsp send count 1. |
| G15 | [g15-amount-mismatch-no-event.md](./g15-amount-mismatch-no-event.md) | Stripe 999 vs 10.00, event absent. |
| G16 | [g16-placeholder-email-four-rails.md](./g16-placeholder-email-four-rails.md) | Index; methods live in fc/fb/fx/fr. |

### Track S — Strengthen existing methods first

| ID | File | Intent |
|----|------|--------|
| S10 | [s10-zero-amount-ignored-asserts.md](./s10-zero-amount-ignored-asserts.md) | `ignored` + checkout `open`. |
| S11 | [s11-setup-body-contains-setup.md](./s11-setup-body-contains-setup.md) | Optional `setup` token. |
| S12 | [s12-paid-official-receipt-title.md](./s12-paid-official-receipt-title.md) | Title, paid, SST null. |
| S13 | [s13-chip-start-redirect-and-ids.md](./s13-chip-start-redirect-and-ids.md) | `redirect_url`, Provider, session id. |
| S14 | [s14-billplz-paid-rcpt-replay.md](./s14-billplz-paid-rcpt-replay.md) | `RCPT-` + duplicate; no localhost here. |
| S15 | [s15-xendit-paid-rcpt-settled-ignored.md](./s15-xendit-paid-rcpt-settled-ignored.md) | Strengthen SETTLED method. |
| S16 | [s16-razorpay-captured-rcpt-balance.md](./s16-razorpay-captured-rcpt-balance.md) | `RCPT-` + D==C. |
| S17 | [s17-isolation-extra-tokens.md](./s17-isolation-extra-tokens.md) | Registrar, DNS, Connect, LHDN. |
| S18 | [s18-get-webhook-configured.md](./s18-get-webhook-configured.md) | GET boolean + audit org. |

### Track F — Fill tests 015 ticked without methods

Stripe (`fs`), CHIP (`fc`), Billplz (`fb`), Xendit (`fx`), Razorpay (`fr`), gateway (`fg`), public (`fp`).

Index and locked method names: [f00-fill-index.md](./f00-fill-index.md). **Do not invent aliases.** Strengthen S10–S18 **before** cloning paid tests. Pointer files (`fs11`, `fs15`, `fs16`, `fs18`, `fr23`, `fi10`) must not duplicate G/E/D/J/S methods.

### Track M — Merchant `:5178`

| ID | File | Intent |
|----|------|--------|
| M10 | [m10-chip-pem-textarea.md](./m10-chip-pem-textarea.md) | PEM is `<textarea>`. |
| M11 | [m11-hydrate-environment.md](./m11-hydrate-environment.md) | GET `environment` → select. |
| M12 | [m12-hydrate-webhook-configured.md](./m12-hydrate-webhook-configured.md) | Show configured, never the secret. |
| M13 | [m13-hydrate-public-merchant-id.md](./m13-hydrate-public-merchant-id.md) | Brand / Collection from GET. |
| M14 | [m14-put-error-detail.md](./m14-put-error-detail.md) | `keys` errors show host `detail`. |
| M15 | [m15-product-error-detail.md](./m15-product-error-detail.md) | Same for product POST. |
| M16 | [m16-checkout-error-detail.md](./m16-checkout-error-detail.md) | Same for checkout POST. |
| M17 | [m17-billplz-webhook-hint.md](./m17-billplz-webhook-hint.md) | Copy is not the Billplz callback. |
| M18 | [m18-pay-link-uses-checkout-origin.md](./m18-pay-link-uses-checkout-origin.md) | Minted URL uses L14. |
| M19 | [m19-product-id-or-honesty.md](./m19-product-id-or-honesty.md) | Attach `product_id` or stop saying SKU. |
| M20 | [m20-locks-pem-textarea.md](./m20-locks-pem-textarea.md) | Vitest grep textarea for chip. |
| M21 | [m21-locks-hydrate-environment.md](./m21-locks-hydrate-environment.md) | Grep `setEnvironment` from GET. |
| M22 | [m22-no-new-vite-secrets.md](./m22-no-new-vite-secrets.md) | Still no `sk_` / PEM in `VITE_*`. |

### Track K — Checkout `:5179`

| ID | File | Intent |
|----|------|--------|
| K10 | [k10-placeholder-email-blocked.md](./k10-placeholder-email-blocked.md) | `customer@example.com` not usable. |
| K11 | [k11-400-shows-detail.md](./k11-400-shows-detail.md) | Stop conflating callback vs email. |
| K12 | [k12-503-shows-detail.md](./k12-503-shows-detail.md) | Stop mapping every 503 to rail. |
| K13 | [k13-verifying-timeout-escape.md](./k13-verifying-timeout-escape.md) | After 15 ticks: not-paid-yet + refresh GET. |
| K14 | [k14-started-continue-not-new-pay.md](./k14-started-continue-not-new-pay.md) | Open + started → Continue, not a second mint. |
| K15 | [k15-locks-verifying-query.md](./k15-locks-verifying-query.md) | Grep `status === 'verifying'`. |
| K16 | [k16-locks-poll.md](./k16-locks-poll.md) | Grep interval GET `/v1/pay/`. |
| K17 | [k17-locks-placeholder.md](./k17-locks-placeholder.md) | Grep placeholder refuse. |
| K18 | [k18-locks-not-paid-on-400.md](./k18-locks-not-paid-on-400.md) | 400 does not set status paid. |

### Track Q — Spec / CORS / copy

| ID | File | Intent |
|----|------|--------|
| Q10 | [q10-pay-spec-start-body.md](./q10-pay-spec-start-body.md) | TypeSpec start accepts `{name,email}`. |
| Q11 | [q11-cors-preview-origins.md](./q11-cors-preview-origins.md) | Allow 4178/4179 or document no. |
| Q12 | [q12-cors-still-denies-ops.md](./q12-cors-still-denies-ops.md) | 3003/3004 still denied. |
| Q13 | [q13-host-readme-harden.md](./q13-host-readme-harden.md) | README: idempotent start, two bases, Testing-only `whsec_`. |
| Q14 | [q14-no-011-flip-from-tests.md](./q14-no-011-flip-from-tests.md) | Tracker only from lived Exit. |
| Q15 | [q15-task-pay-test-still-hermetic.md](./q15-task-pay-test-still-hermetic.md) | No live PSP in CI. |
| Q16 | [q16-no-hub-projectreference.md](./q16-no-hub-projectreference.md) | Isolation still red on cathedral. |

### Parked

| File | Stay refused |
|------|----------------|
| [parked-factory.md](./parked-factory.md) | `IPaymentGatewayAdapter` / factory |
| [parked-chip-registrar.md](./parked-chip-registrar.md) | Silent CHIP webhook CRUD |
| [parked-dns-fallback.md](./parked-dns-fallback.md) | `PublicDnsFallback` |
| [parked-lhdn-sst.md](./parked-lhdn-sst.md) | SST / MyInvois / Tax Invoice |
| [parked-emandate.md](./parked-emandate.md) | FPX e-mandate / Agreements v5 |
| [parked-offsession.md](./parked-offsession.md) | Vault auto-debit |
| [parked-refunds.md](./parked-refunds.md) | `IssueRefund` |
| [parked-hub-cutover.md](./parked-hub-cutover.md) | Dark Hub / retarget ops |
| [parked-sixth-rail.md](./parked-sixth-rail.md) | A sixth PSP name |

## How to tick

Tick a box only when the **live file** does the thing. Do not tick because 015 already `[x]`’d a cousin. A99.1 (lived dogfood) stays open until a human loop on **one** rail.
