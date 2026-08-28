# 020 — Implementation checklists (kernel door + go-live honesty)

**Date:** 28 August 2026  
**HEAD at freeze:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**Style:** Many **small** phase files. One phase ≈ one commit (or a tightly scoped PR).  
**Idea:** [`../11-what-next.md`](../11-what-next.md)  
**Evidence:** [`../00-evaluation.md`](../00-evaluation.md) and `../01`–`../10`. Do not treat this folder as a substitute.  
**Freeze:** [`decisions.md`](./decisions.md) (locked in K00).

**Named next program is Job A (kernel door):** another app can mint with One `lzr_sk_`, get a pay URL, and learn paid from a signed `payment.completed`.  
**Job B (first-party go-live)** is a **parallel cheap track (H)** plus a **later track (G)**. Do not block A on G. Do not call G “platform.”

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One intent per phase | MemberGate + outbound worker + sample in one tip |
| Hermetic Fake One / captured HttpClient in `task pay:test` | Require live Zitadel for CI |
| One mints `lzr_sk_`; Pay introspects | Pay-local `sk_*` table |
| Host Map* first, then `pay-spec` | Spec a door that is not mapped |
| Hub `examples/` stays museum | Retarget hub-cashier-next at 8081 |
| `/v1` on **8081** | Bind 8080 or steal Hub compose |

## Track map

```text
K00 Align & freeze
  │
  ├─ Track M Machine key (serial) ── M10 → … → M23     JOB A
  ├─ Track U pay_url ─────────────── U10 → … → U15     after M14 (or after K00 if JWT-only field)
  ├─ Track W Plane C outbound ────── W10 → … → W30     schema after K00; worker after W18
  ├─ Track H Host honesty ────────── H10 → … → H16     JOB B cheap; ∥ after K00
  │
  ├─ Track S Spec ────────────────── folded into M22 / U15 / W29  (host first)
  ├─ Track E Sample + README ─────── E10 → … → E16     after M18 + W21 + U14
  ├─ Track D Docs honesty ────────── D10 → D12         ∥ after K00; tighten after E
  │
  └─ Track G Go-live leftover ────── G10 → … → G19     JOB B; do not block A
K99a Kernel definition of done (Job A)
K99b Go-live definition of done (Job B)

Parked (do not start inside A):
  P10 refunds  P11 subscriptions  P12 Mode M worker key
  P13 api_key.revoked cache  P14 pagination  P15 payment.failed
  P16 checkout.expired  P17 merchant webhook UI  P18 OTel
  P19 pay-types-ts  P20 expire-at-processor
```

**Parallel after K00**

| Band | Phases |
|------|--------|
| A | M10–M23 serial |
| A′ | U10–U15 after **M14** (mint with key must return URL) — U10–U13 may land on JWT path earlier |
| A″ | W10–W13 (tables + SSRF) ∥ M; **W14+** needs writer (JWT is enough to register; kernel complete needs M) |
| B cheap | H10–H16 ∥ everything |
| Sample | E after **M18 + U14 + W21** (key can mint, URL on 201, worker 2xx) |
| G | Anytime after H10; not a gate for K99a |

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| K00 | [k00-align-freeze.md](./k00-align-freeze.md) | Lock Job A vs B, anti-goals. No product code. |
| K99a | [k99a-kernel-done.md](./k99a-kernel-done.md) | Stranger learned paid from Pay |
| K99b | [k99b-golive-done.md](./k99b-golive-done.md) | We can boot and charge ourselves without laptop lies |

### Track M — Machine key (Job A)

| ID | File | Intent |
|----|------|--------|
| M10 | [m10-bearer-family.md](./m10-bearer-family.md) | Prefix-check `lzr_sk_`; reject `sk_live_` / PAT as Pay Bearer |
| M11 | [m11-fake-one-key-me.md](./m11-fake-one-key-me.md) | Fake One `/me` JSON for a bound key |
| M12 | [m12-membergate-key-branch.md](./m12-membergate-key-branch.md) | Keys skip `authz/check` without `user_id` |
| M13 | [m13-key-bound-tenant.md](./m13-key-bound-tenant.md) | Path org must be the key’s one tenant; active |
| M14 | [m14-key-is-writer.md](./m14-key-is-writer.md) | Bound key may mint (writer of that org) |
| M15 | [m15-jwt-member-still-forbidden.md](./m15-jwt-member-still-forbidden.md) | Human `member` still cannot mint |
| M16 | [m16-no-env-god-key-fallback.md](./m16-no-env-god-key-fallback.md) | Missing Bearer never uses env key |
| M17 | [m17-whoami-forwards-key.md](./m17-whoami-forwards-key.md) | Whoami forwards `lzr_sk_` to One `/me` |
| M18 | [m18-wrong-org-403.md](./m18-wrong-org-403.md) | Key of t1 cannot mint on t2 |
| M19 | [m19-revoked-key-401.md](./m19-revoked-key-401.md) | One 401 on revoked key → Pay 401 |
| M20 | [m20-isolation-no-pay-keys-table.md](./m20-isolation-no-pay-keys-table.md) | No Pay `api_keys` / hasher / pepper |
| M21 | [m21-403-detail-honesty.md](./m21-403-detail-honesty.md) | Do not swallow One scope 403 into “not a member” |
| M22 | [m22-readme-m2m-hatch.md](./m22-readme-m2m-hatch.md) | Document mint-on-One, send `lzr_sk_` |
| M23 | [m23-spa-still-rejects-key.md](./m23-spa-still-rejects-key.md) | Merchant SPA still JWT-only |

### Track U — `pay_url`

| ID | File | Intent |
|----|------|--------|
| U10 | [u10-pay-url-builder.md](./u10-pay-url-builder.md) | Build public pay URL from `CheckoutBaseUrl` + token |
| U11 | [u11-checkout-create-pay-url.md](./u11-checkout-create-pay-url.md) | `POST /v1/checkouts` 201 includes `pay_url` |
| U12 | [u12-payment-link-create-pay-url.md](./u12-payment-link-create-pay-url.md) | `POST /v1/payment-links` 201 includes `pay_url` |
| U13 | [u13-checkout-get-pay-url.md](./u13-checkout-get-pay-url.md) | `GET /v1/checkouts/{id}` includes `pay_url` |
| U14 | [u14-pay-url-tests.md](./u14-pay-url-tests.md) | Hermetic asserts; no localhost lie in Production tests |
| U15 | [u15-pay-spec-pay-url.md](./u15-pay-spec-pay-url.md) | TypeSpec field after host |

### Track W — Plane C (`payment.completed`)

| ID | File | Intent |
|----|------|--------|
| W10 | [w10-endpoints-table.md](./w10-endpoints-table.md) | `webhook_endpoints` one active row per org |
| W11 | [w11-deliveries-table.md](./w11-deliveries-table.md) | `webhook_deliveries` outbox |
| W12 | [w12-event-catalog.md](./w12-event-catalog.md) | Closed catalog: `payment.completed` (+ optional `webhook.test`) |
| W13 | [w13-ssrf-validator.md](./w13-ssrf-validator.md) | URL rules; loopback Testing-only |
| W14 | [w14-register-post.md](./w14-register-post.md) | Writer `PUT/POST /v1/orgs/{orgId}/webhooks`; secret once |
| W15 | [w15-get-no-echo-secret.md](./w15-get-no-echo-secret.md) | GET metadata; never echo `whsec_` |
| W16 | [w16-rotate.md](./w16-rotate.md) | Rotate mint new secret once |
| W17 | [w17-sign-one-dialect.md](./w17-sign-one-dialect.md) | Sign `{unix}.{body}`; split One headers |
| W18 | [w18-fulfill-enqueue.md](./w18-fulfill-enqueue.md) | Same SaveChanges as fulfill; no HTTP in TX |
| W19 | [w19-unique-event-id.md](./w19-unique-event-id.md) | Unique `(endpoint, event_id)`; replay no second row |
| W20 | [w20-worker-off-in-testing.md](./w20-worker-off-in-testing.md) | Hosted worker off in Testing; `ProcessBatch` testable |
| W21 | [w21-worker-2xx.md](./w21-worker-2xx.md) | 2xx → succeeded; round-trip verify |
| W22 | [w22-worker-retry-5xx.md](./w22-worker-retry-5xx.md) | 5xx schedules next_attempt |
| W23 | [w23-worker-dead-401.md](./w23-worker-dead-401.md) | 401/403 dead; do not retry forever |
| W24 | [w24-loopback-testing-hatch.md](./w24-loopback-testing-hatch.md) | Testing allows `127.0.0.1` |
| W25 | [w25-production-ssrf.md](./w25-production-ssrf.md) | Production rejects loopback / link-local / metadata |
| W26 | [w26-member-cannot-register.md](./w26-member-cannot-register.md) | Member 403 on register |
| W27 | [w27-no-endpoint-still-paid.md](./w27-no-endpoint-still-paid.md) | No endpoint → 0 deliveries; charge exists |
| W28 | [w28-isolation-hub-outbound.md](./w28-isolation-hub-outbound.md) | Ban Hub outbound type names |
| W29 | [w29-pay-spec-outbound.md](./w29-pay-spec-outbound.md) | Spec register + result after host |
| W30 | [w30-webhook-test-ping.md](./w30-webhook-test-ping.md) | Optional `webhook.test` ping |

### Track H — Host honesty (Job B cheap)

| ID | File | Intent |
|----|------|--------|
| H10 | [h10-ready-bool.md](./h10-ready-bool.md) | `/ready` uses `CanConnectAsync` bool |
| H11 | [h11-ready-down-test.md](./h11-ready-down-test.md) | Down / fake fail → 503 |
| H12 | [h12-production-wrapkey-boot.md](./h12-production-wrapkey-boot.md) | Production empty WrapKey fails boot |
| H13 | [h13-production-cs-boot.md](./h13-production-cs-boot.md) | Production empty Pay CS fails boot |
| H14 | [h14-production-one-url-boot.md](./h14-production-one-url-boot.md) | Production empty/laptop One URL fails boot |
| H15 | [h15-start-limiter-docs.md](./h15-start-limiter-docs.md) | Document default 20; do not raise to 200 |
| H16 | [h16-appsettings-laptop-dev-only.md](./h16-appsettings-laptop-dev-only.md) | Laptop One URL only in Development settings |

### Track E — Sample (after M + U + W)

| ID | File | Intent |
|----|------|--------|
| E10 | [e10-museum-hub-examples.md](./e10-museum-hub-examples.md) | Mark Hub sample museum in every README that presents it |
| E11 | [e11-pay-node-workspace.md](./e11-pay-node-workspace.md) | `examples/pay-node`; exclude default turbo |
| E12 | [e12-pay-node-env.md](./e12-pay-node-env.md) | Env: Pay 8081, One key, webhook secret |
| E13 | [e13-pay-node-mint.md](./e13-pay-node-mint.md) | `POST /v1/checkouts` with `lzr_sk_`; use `pay_url` |
| E14 | [e14-pay-node-verify.md](./e14-pay-node-verify.md) | Verify One-dialect HMAC; raw body |
| E15 | [e15-pay-node-unlock.md](./e15-pay-node-unlock.md) | Unlock a toy row after verified completed |
| E16 | [e16-second-app-readme.md](./e16-second-app-readme.md) | Host README “second app” page |

### Track D — Docs honesty

| ID | File | Intent |
|----|------|--------|
| D10 | [d10-root-readme-pay.md](./d10-root-readme-pay.md) | Root README does not hide focused Pay |
| D11 | [d11-host-readme-honesty.md](./d11-host-readme-honesty.md) | Allowed sentences; no “platform” / “we have API keys” until true |
| D12 | [d12-cors-second-app-origin.md](./d12-cors-second-app-origin.md) | Document `Pay:CorsOrigins` CSV for a browser second app |

### Track G — Go-live leftover (Job B; not a gate for K99a)

| ID | File | Intent |
|----|------|--------|
| G10 | [g10-persist-chip.md](./g10-persist-chip.md) | CHIP: persist session before/idempotent HTTP |
| G11 | [g11-persist-billplz.md](./g11-persist-billplz.md) | Billplz same |
| G12 | [g12-persist-xendit.md](./g12-persist-xendit.md) | Xendit same |
| G13 | [g13-persist-razorpay.md](./g13-persist-razorpay.md) | Razorpay same |
| G14 | [g14-pay-db-volume.md](./g14-pay-db-volume.md) | Named volume on pay-db |
| G15 | [g15-compose-laptop-honesty.md](./g15-compose-laptop-honesty.md) | `--profile apps` is laptop; not prod |
| G16 | [g16-one-suspend-fixture.md](./g16-one-suspend-fixture.md) | Captured/sanitized One `tenant.suspended` |
| G17 | [g17-one-webhook-register-runbook.md](./g17-one-webhook-register-runbook.md) | Ops: register Pay URL on One + PUT `whsec_` |
| G18 | [g18-vitest-ci.md](./g18-vitest-ci.md) | Merchant + checkout vitest in CI |
| G19 | [g19-pay-ghcr-bake.md](./g19-pay-ghcr-bake.md) | Bake group `pay` is not Hub GHCR job |

### Parked

| ID | File | Intent |
|----|------|--------|
| P10 | [p10-refunds.md](./p10-refunds.md) | Refund API — after Bar B boring |
| P11 | [p11-subscriptions.md](./p11-subscriptions.md) | Subscriptions — refuse this program |
| P12 | [p12-mode-m-worker-key.md](./p12-mode-m-worker-key.md) | Pay-process `ONE_API_KEY` for jobs |
| P13 | [p13-api-key-revoked-cache.md](./p13-api-key-revoked-cache.md) | Cache `/me` only with revoke HMAC |
| P14 | [p14-pagination.md](./p14-pagination.md) | List cursors |
| P15 | [p15-payment-failed.md](./p15-payment-failed.md) | Do not emit failed from `{ignored}` |
| P16 | [p16-checkout-expired.md](./p16-checkout-expired.md) | Occupancy TTL event |
| P17 | [p17-merchant-webhook-ui.md](./p17-merchant-webhook-ui.md) | Staff chrome for Plane C |
| P18 | [p18-otel.md](./p18-otel.md) | Metrics after a URL exists |
| P19 | [p19-pay-types-ts.md](./p19-pay-types-ts.md) | Optional types from pay-spec |
| P20 | [p20-expire-at-processor.md](./p20-expire-at-processor.md) | Late PSP pay after TTL |

## Phase file template

Every phase file in this folder **must** have, in this order:

1. Title, Track, Depends, Analysis (020 paper + freeze), Goal
2. **Why** (or **Why parked** + **Unpark when**) — what is false or missing on `6d730d15`
3. **Related files** — live paths the implementer opens (host, tests, spec, SPA, compose). Not Hub unless museum contrast
4. **Current (`6d730d15`)** — what the file does today, so the PR is a diff not a guess
5. Numbered checkbox sections (do / must not / tests)
6. **Exit** — what is now true, which phase unblocks

Do not leave “same as G10” as the whole file. Copy the rail path and the exact persist-after-HTTP site.

## Isolation (every product PR)

Stay red: `MediatR`, `IEnumerable<IHostedRail>`, `Modules.One`, Hub `@repo/api-types-ts`, `GatewayPaymentCompletedIntegrationEvent`, org/user/member tables, `apps/lazuar-api` project reference, Zitadel PAT. W28 adds outbound Hub type names.
