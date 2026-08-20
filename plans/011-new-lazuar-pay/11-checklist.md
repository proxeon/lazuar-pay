# 11 — Feature checklist (new Lazuar Pay)

**Date:** 20 August 2026  
**Schema:** [10-tracker-schema.md](./10-tracker-schema.md)  
**Slice steps:** [12-first-slice-tracker.md](./12-first-slice-tracker.md)  
**Living file.** Flip **Status** when new Pay actually has the job. Seed is `todo` / `refuse` / `n/a` — the old C# tree does not count as `done`.

Columns: **ID** · **Feature** · **Wave** · **Owner** · **Dogfood** · **Status** · **Notes**

---

## Counts (update when flipping)

| Wave | Rows | todo | doing | done | blocked | refuse | n/a |
|------|------|------|-------|------|---------|--------|-----|
| S0 | 22 | 17 | 0 | 5 | 0 | 0 | 0 |
| S1 | 42 | 42 | 0 | 0 | 0 | 0 | 0 |
| V1 | 12 | 12 | 0 | 0 | 0 | 0 | 0 |
| soon | 9 | 9 | 0 | 0 | 0 | 0 | 0 |
| later | 6 | 6 | 0 | 0 | 0 | 0 | 0 |
| refuse | 24 | 0 | 0 | 0 | 0 | 24 | 0 |
| **Total** | **115** | **86** | **0** | **5** | **0** | **24** | **0** |

Dogfood path (`Dogfood = Y`): **43** rows; S0 whoami/authz subset `done` on focused Pay. Remaining dogfood jobs still `todo`. Count includes every S0/S1 job the [01](./01-product.md) dogfood sentence fails without — not only the 12 slice steps.

---

## A. One façade (`NP-ONE`) — wave S0

Pay is Consumer-0. Do not rebuild `Modules/One`. Detail: [02-one-integration.md](./02-one-integration.md).

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-ONE-001 | Register Pay SPA via One `POST /tenants/{id}/apps` (or seed like `lazuar-app`) | S0 | both | Y | todo | Not a Zitadel Console click |
| NP-ONE-002 | OIDC code + PKCE; Pay `client_id`; Zitadel authority | S0 | both | Y | todo | Env like `lazuar-app` |
| NP-ONE-003 | Send **access_token** as `Authorization: Bearer` | S0 | Pay | Y | done | `GET /v1/whoami` forwards Bearer; never `id_token` as Bearer |
| NP-ONE-004 | Register Pay redirects on One app + login `REDIRECT_ALLOWLIST` | S0 | both | Y | todo | Not Console-only |
| NP-ONE-005 | Product login via `:5175`; never ship `:3005` or `:5173` | S0 | both | Y | todo | `:5175` is not Pay’s homepage |
| NP-ONE-006 | `GET /me` for user, tenants, roles, `active_tenant_id` | S0 | both | Y | done | Pay `GET /v1/whoami` calls One `/me` once; not middleware |
| NP-ONE-007 | Path `{tenantId}` + membership is authz SoT | S0 | Pay | Y | done | `GET /v1/orgs/{orgId}/ready`; header is hint only |
| NP-ONE-008 | Roles from `/me` + `authz/check`, not Zitadel project-role claims | S0 | Pay | — | done | Projection copies One `role`; no Zitadel claim parse |
| NP-ONE-009 | Create workspace = `POST /tenants`; One tenant id **is** Pay `org_id` | S0 | both | Y | todo | No second org table |
| NP-ONE-010 | `GET` / `PATCH` tenant profile (name, metadata, logo) | S0 | both | — | todo | Not `POST /platform/tenants` |
| NP-ONE-011 | Copy-link invite + pending list + revoke + resend | S0 | both | Y | todo | One membership is SoT |
| NP-ONE-012 | Accept-invite; keep a **non-email** accept path | S0 | both | Y | todo | Deep-link `lazuar-app` or post same API |
| NP-ONE-013 | Roster; change role; remove member; `GET /me/invites` | S0 | both | — | todo | |
| NP-ONE-014 | Mint / list / revoke `lzr_sk_` with **explicit** scopes | S0 | both | Y | todo | No `*` / empty scopes |
| NP-ONE-015 | `authz/check` `member` / `admin` / `owner` before merchant admin routes | S0 | both | Y | done | Dummy `/v1/orgs/{orgId}/ready` checks `member` on `tenant` |
| NP-ONE-016 | `authz/batch-check` for permission chrome | S0 | both | — | todo | No `authz/write` |
| NP-ONE-017 | HMAC webhooks: `member.*`, `tenant.created` / `suspended` / `reactivated`, `ownership.transferred`, `api_key.revoked` | S0 | both | Y | todo | Pull events if no push; do not tail Zitadel |
| NP-ONE-018 | Stop charges (and staff access) on `tenant.suspended` | S0 | Pay | Y | todo | Money in Pay stays true if webhook is late |
| NP-ONE-019 | Provision Pay catalog/ledger rows on `tenant.created` | S0 | Pay | — | todo | |
| NP-ONE-020 | Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC | S0 | Pay | — | todo | Never Zitadel PAT / FGA admin / masterkey |
| NP-ONE-021 | VIEWER cannot charge, change keys, or refund | S0 | Pay | Y | todo | Enforce in Pay using One role + `authz` |
| NP-ONE-022 | Invited MEMBER can see merchant ops | S0 | Pay | Y | todo | Dogfood second engineer |

---

## B. Catalog (`NP-CAT`) — wave S1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-CAT-001 | Product: name (and optional description) | S1 | Pay | Y | todo | |
| NP-CAT-002 | Prices: monthly and/or yearly | S1 | Pay | Y | todo | |
| NP-CAT-003 | Currency: start **MYR** | S1 | Pay | Y | todo | |
| NP-CAT-004 | Quantity / seats on the price | S1 | Pay | — | todo | SST: unit then × seats ([NP-MON-003](#f-money-np-mon--waves-s1--v1)) |
| NP-CAT-005 | Merchant ops: list / create / edit products | S1 | Pay | Y | todo | Client of `/v1` |

---

## C. Checkout (`NP-CHK`) — wave S1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-CHK-001 | Checkout session: amount, currency, tenant | S1 | Pay | Y | todo | |
| NP-CHK-002 | Success and cancel URLs | S1 | Pay | — | todo | Fulfillment is the webhook, not `success_url` alone |
| NP-CHK-003 | Idempotency key on create | S1 | Pay | — | todo | |
| NP-CHK-004 | States: open → paid / expired | S1 | Pay | Y | todo | |
| NP-CHK-005 | Hosted buyer pay page (cash register) | S1 | Pay | Y | todo | |
| NP-CHK-006 | Shareable pay link | S1 | Pay | Y | todo | |
| NP-CHK-007 | Buyer pays **without** a One account | S1 | Pay | Y | todo | Fail if checkout requires Zitadel login |

---

## D. Gateways (`NP-GW`) — wave S1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-GW-001 | Encrypted BYOK keys per workspace | S1 | Pay | Y | todo | Stripe **or** CHIP/Billplz for dogfood |
| NP-GW-002 | Stripe card checkout | S1 | Pay | Y | todo | Off-session only if a real PM/token exists |
| NP-GW-003 | One Malaysian rail you will dogfood (CHIP **or** Billplz) | S1 | Pay | Y | todo | Not five adapters on day one |
| NP-GW-004 | Webhook: verify signature | S1 | Pay | Y | todo | |
| NP-GW-005 | Empty webhook body → 400 | S1 | Pay | — | todo | |
| NP-GW-006 | Idempotent on `(tenant, provider, event_id)`; retry no-ops | S1 | Pay | Y | todo | Must not double-journal |
| NP-GW-007 | Honest matrix: Stripe/CHIP auto-charge if vaulted; Billplz-class = reminder + hosted link | S1 | Pay | — | todo | Never silent debit on reminder-only rails |
| NP-GW-008 | Never treat setup / setup-intent as paid | S1 | Pay | — | todo | Fail mode in [03](./03-first-slice.md) |
| NP-GW-009 | Merchant ops: paste / rotate gateway keys | S1 | Pay | Y | todo | VIEWER cannot ([NP-ONE-021](#a-one-façade-np-one--wave-s0)) |

---

## E. Fulfillment (`NP-FUL`) — waves S1 / V1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-FUL-001 | First successful pay creates subscription (or completes one-off) **and** writes the ledger in the **same handler** | S1 | Pay | Y | todo | Do not wait on One to “hear an event” |
| NP-FUL-002 | Buyer access = Pay subscription / session row | S1 | Pay | Y | todo | Do not grant buyer access in One |
| NP-FUL-003 | Merchant ops: payments + subscribers list | S1 | Pay | Y | todo | |
| NP-FUL-004 | Renew: billing job mints checkout or off-session charge | V1 | Pay | — | todo | Wrap-rails: off-session only where vaulted |
| NP-FUL-005 | Decline does not invent PAST_DUE on a healthy seat without a real failed charge | V1 | Pay | — | todo | |

---

## F. Money (`NP-MON`) — waves S1 / V1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-MON-001 | Double-entry journal: cash, revenue, tax, fee | S1 | Pay | Y | todo | Balanced on first pay |
| NP-MON-002 | Gateway fee only when the PSP actually sent it (`unknown` ≠ 0) | S1 | Pay | — | todo | |
| NP-MON-003 | SST: exclusive on the **unit**, then × seats | V1 | Pay | — | todo | Steal judgment from old `SstTaxMath` |
| NP-MON-004 | Fail closed if merchant SST registration is unknown | V1 | Pay | — | todo | Do not undercharge |
| NP-MON-005 | Full refund: call gateway, then reverse the journal **once** | V1 | Pay | — | todo | |
| NP-MON-006 | Disputes: do not double-reverse | V1 | Pay | — | todo | |

---

## G. Documents (`NP-DOC`) — wave S1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-DOC-001 | Official Receipt / payment receipt numbered `RCPT-…` | S1 | Pay | Y | todo | Commercial document, not tax |
| NP-DOC-002 | Number is never a UUID; missing number is `PENDING` | S1 | Pay | Y | todo | |
| NP-DOC-003 | Do not title it Tax Invoice | S1 | Pay | Y | todo | Honesty lock |
| NP-DOC-004 | Do not print MyInvois VALID | S1 | Pay | — | todo | VALID only if a tax **provider** said so (later) |
| NP-DOC-005 | Merchant can open the receipt in ops | S1 | Pay | Y | todo | |

---

## H. Buyer plane (`NP-BUY`) — waves S1 / V1

Not One. Cardholders never become Zitadel users.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-BUY-001 | Payer email / name on the checkout session | S1 | Pay | Y | todo | |
| NP-BUY-002 | Small payer profile inside Pay | S1 | Pay | — | todo | Old CRM/client-profile job, stripped |
| NP-BUY-003 | Magic link to the **payer** mailbox | V1 | Pay | — | todo | Receipts / update-payment |
| NP-BUY-004 | Buyer portal: update payment method | V1 | Pay | — | todo | |
| NP-BUY-005 | Buyer portal: download receipt | V1 | Pay | — | todo | |

---

## I. Mail (`NP-MAIL`) — waves S1 / V1 / soon

Transactional mail lives **in Pay**. Staff invite copy-link stays One.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-MAIL-001 | Receipt email after paid | S1 | Pay | — | todo | Same process; not a Notify service |
| NP-MAIL-002 | Failed-pay email | V1 | Pay | — | todo | |
| NP-MAIL-003 | Buyer magic-link email | V1 | Pay | — | todo | |
| NP-MAIL-004 | PAST_DUE / dunning email sequence | soon | Pay | — | todo | See [NP-SOON-004](#l-soon-np-soon) |

---

## J. Audit (`NP-AUD`) — waves S1 / V1

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-AUD-001 | Audit row on charge, **same DB transaction** as the write | S1 | Pay | — | todo | Not an audit service |
| NP-AUD-002 | Audit row on refund, same transaction | V1 | Pay | — | todo | |
| NP-AUD-003 | Audit row on gateway-key change, same transaction | S1 | Pay | — | todo | |

---

## K. Public door (`NP-API`) — wave S1

Bezos door: [08-bezos-door.md](./08-bezos-door.md).

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-API-001 | `POST /v1/checkouts` | S1 | Pay | Y | todo | Versioned HTTP from day one |
| NP-API-002 | Provider webhook URL (Stripe / CHIP / Billplz) | S1 | Pay | Y | todo | |
| NP-API-003 | `GET` payment status | S1 | Pay | — | todo | |
| NP-API-004 | Merchant ops is a client of `/v1` (One user JWT or `lzr_sk_`) | S1 | Pay | Y | todo | No back-door table reads |
| NP-API-005 | Tenant isolation on every route | S1 | Pay | — | todo | |
| NP-API-006 | Idempotency on money POSTs | S1 | Pay | — | todo | Aligns with [NP-CHK-003](#c-checkout-np-chk--wave-s1) / [NP-GW-006](#d-gateways-np-gw--wave-s1) |

---

## L. Soon (`NP-SOON`)

Still Pay. After S1 is boring.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-SOON-001 | Custom amount / quote | soon | Pay | — | todo | |
| NP-SOON-002 | Proforma PDF (not a tax invoice) | soon | Pay | — | todo | |
| NP-SOON-003 | SST on the quote matches hop-2 checkout | soon | Pay | — | todo | |
| NP-SOON-004 | PAST_DUE + email dunning + cached update-payment link | soon | Pay | — | todo | |
| NP-SOON-005 | One completion does not skip a billing cycle | soon | Pay | — | todo | |
| NP-SOON-006 | Partial refunds that match the gateway | soon | Pay | — | todo | |
| NP-SOON-007 | M2M checkout for a second of *your* apps (same `/v1`) | soon | Pay | — | todo | First extra consumer |
| NP-SOON-008 | Second gateway only after the first two are boring in production | soon | Pay | — | todo | |

---

## M. Later (`NP-LAT`)

Not v1.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-LAT-001 | Tax **provider**: send amount + buyer; receive VALID + QR | later | vendor | — | todo | Pay never owns UBL / consolidation / types 03–14 / XAdES |
| NP-LAT-002 | More rails (Razorpay, Xendit) as reminder-only, labelled as such | later | Pay | — | todo | |
| NP-LAT-003 | Entitlement grant for a **second** Lazuar app via HTTP | later | Pay | — | todo | Not an in-process event catalog talking to yourself |
| NP-LAT-004 | Extract Notify when a second product shares a sending domain | later | Pay | — | todo | Until then `internal/notify` |
| NP-LAT-005 | Audit **feed** API if someone buys a feed | later | Pay | — | todo | Still a table until then |
| NP-LAT-006 | Enterprise SSO / SCIM / HRD via **One** when a named merchant asks | later | One | — | todo | Not a Pay portal on `lazuar-admin` |

---

## N. Refuse (`NP-XX`)

Keep these rows. Deleting them is how the museum comes back.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-XX-001 | Homemade LHDN / XML / UBL / consolidation job | refuse | Pay | — | refuse | Sandbox VALID was never captured in the old tree |
| NP-XX-002 | TIN-at-checkout as a legal e-invoice feature | refuse | Pay | — | refuse | |
| NP-XX-003 | Title receipt Tax Invoice / print VALID without a provider | refuse | Pay | — | refuse | |
| NP-XX-004 | WhatsApp dunning | refuse | Pay | — | refuse | Vitamin |
| NP-XX-005 | Xero | refuse | Pay | — | refuse | |
| NP-XX-006 | Web3, escrow, CMS, 15-app super-app | refuse | Pay | — | refuse | |
| NP-XX-007 | Zitadel, OpenFGA, SCIM, or password store **inside Pay** | refuse | Pay | — | refuse | That is One (or a vendor) |
| NP-XX-008 | Dual JWT vs membership roles | refuse | Pay | — | refuse | `/me` + `authz/check` |
| NP-XX-009 | Per-module schemas / inbox as the way Pay talks to itself | refuse | Pay | — | refuse | Already paid that tax |
| NP-XX-010 | Debit notes, self-billed 11–14, “Credit & Debit Notes” | refuse | Pay | — | refuse | Strategy-only lies in the old tree |
| NP-XX-011 | Homemade FPX e-mandate | refuse | Pay | — | refuse | Wrap-rails only |
| NP-XX-012 | Stripe Billing `subscription.updated` as source of truth | refuse | Pay | — | refuse | |
| NP-XX-013 | Create a Zitadel human per cardholder | refuse | Pay | — | refuse | Buyer plane is Pay |
| NP-XX-014 | Second `organizations` table “just for Pay” plus One members | refuse | Pay | — | refuse | One membership plane |
| NP-XX-015 | Add FGA types `payment` / `document` with no written check call | refuse | both | — | refuse | AUTHZ-05 only with Pay as named consumer |
| NP-XX-016 | Pay calls One `authz/write` | refuse | Pay | — | refuse | |
| NP-XX-017 | Pay holds Zitadel PAT, login PAT, or OpenFGA admin token | refuse | Pay | — | refuse | |
| NP-XX-018 | Ship merchants to `lazuar-admin` (`:5173`) | refuse | Pay | — | refuse | |
| NP-XX-019 | Notify or Audit as a **process** in v1 | refuse | Pay | — | refuse | Same Pay DB transaction |
| NP-XX-020 | Lazuar Media in v1 | refuse | Pay | — | refuse | |
| NP-XX-021 | Block Pay on npm publish of `@lazuar/one-client` | refuse | Pay | — | refuse | Workspace import is enough |
| NP-XX-022 | Hosted One SKU / Okta / SCIM as the next **Pay** ticket | refuse | One | — | refuse | One staging is NOT PASSED; still integrate HTTP |
| NP-XX-023 | Pay calls `POST /platform/tenants` | refuse | Pay | — | refuse | Staff directory |
| NP-XX-024 | Parse Zitadel `urn:zitadel:iam:org:project:roles` | refuse | Pay | — | refuse | |

---

## How to use

1. Build **S0 then S1** in order of [12-first-slice-tracker.md](./12-first-slice-tracker.md).
2. Flip Status in **this file**.
3. Do not start **soon** until S1 dogfood (`Dogfood = Y`) is `done`.
4. Do not “un-refuse” an `NP-XX` row without editing [01-product.md](./01-product.md) and this schema.
