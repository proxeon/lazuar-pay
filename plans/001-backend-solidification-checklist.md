# Plan 001 — Backend solidification checklist

**Status:** Draft  
**Date:** 2026-08-03  
**Direction:** `docs/001-gaps/00-what-we-need-to-do-next.md`  
**Evidence:** `docs/001-gaps/01`–`20`  

This is a **phase-by-phase implementation checklist** for the Lazuar Hub monorepo. Items are ordered so later phases depend on earlier closed loops. Check boxes as work completes.

**Success bar (all phases):** see “Success criteria” in `00-what-we-need-to-do-next.md`.

**Out of scope for this plan:** escrow, e-sign, community/vault rebuild, multi-country tax beyond LHDN, marketplace, Phase 2/3 ADR 020 wishlist items.

---

## Open decisions (resolve before or at start of relevant phase)

Mark when decided; note outcome in PR/ADR.

- [ ] **D1 — Dunning model:** keep status+day engine with hard fixes **vs** introduce dunning-run + attempt/invoice abstraction
- [ ] **D2 — WhatsApp:** commit Meta Cloud near-term **vs** market email-first until provider ships
- [ ] **D3 — API key ownership:** platform keys in One (scopes include `lhdn.*`) **vs** product-local keys constrained by policy only
- [ ] **D4 — Outbound webhooks:** workspace event bus only **vs** workspace + optional per-product endpoints (never exact-URL-match silent drop)
- [ ] **D5 — Commerce integrator v1 surface:** webhooks + public checkout links only **vs** also key-authenticated M2M admin (products/subs/transactions)

---

# Phase 0 — Foundations and honesty gates

**Goal:** Stop shipping features on broken pipes; make CI and contracts trustworthy enough that Phase A–C work sticks.

**Exit criteria:**

- Every module with `OutboxEventBus` has a publisher job (or explicit “no publish” ADR).
- Failed outbox work is retried or dead-lettered, not silently marked done once.
- `task api:test` (or equivalent) runs all test projects that exist.
- No critical unauthenticated write surfaces remain open without intentional network lock.

## 0.1 Outbox / event pipeline completeness

- [x] **Register Lhdn outbox publisher** (and inbox consumer if keeping inbox tables) in `Modules/Lhdn/Infrastructure/DependencyInjection.cs`
  - Confirm `LhdnDocumentSubmitted|Validated|Cancelled` and `ApiKeyRevoked` leave `lhdn.OutboxMessages`
- [x] **Register CRM outbox publisher** (and inbox if needed) in `Modules/CRM/Infrastructure/DependencyInjection.cs`
  - Confirm `ClientProfileAnonymizedIntegrationEvent` can fan out
- [x] **Architecture/smoke check:** module with OutboxEventBus ⇒ hosted `*OutboxPublisherJob` (document exception list if any)
- [x] **Fix `LhdnDocumentValidatedIntegrationEvent` argument order / status vocabulary**
  - Align poller publish args with event ctor
  - Align Billing + ops UI on `VALID` vs `VALIDATED` (one canonical status)
- [x] **Decide inbox strategy** (docs vs code): implement store-and-ack for state-mutating handlers **or** update `apps/lazuar-api/docs/001-cross-module-communication.md` to official hybrid model

## 0.2 Outbox failure / poison policy

- [x] Extend `OutboxMessage` / `InboxMessage` (or parallel table) with attempt count, next-visible-at, terminal dead-letter state
- [x] Change `OutboxPublisherJob` / `InboxConsumerJob` to **not** set `ProcessedAt` on first failure without retry budget
- [x] Define max attempts + backoff; mark `DEAD` after budget; keep `Error` for support
- [x] Add ops visibility path (SQL runbook minimum; API later): list dead outbox rows by schema
- [x] Document replay procedure for dead letters (manual reset or admin endpoint later)

## 0.3 Critical security gates

- [x] **Authenticate or remove** `POST /api/v1/messaging/notify` (`Modules/Messaging/Infrastructure/Endpoints.cs`)
- [x] **Split auth policies:** `OrgAdmin` = human `SUPER_ADMIN|ADMIN` only; machine principal uses scope/integration policies
- [x] **Deny `API_CLIENT`** on key mint/revoke, certificate upload, payment config, email config, member admin
- [x] **Fail production boot** if JWT secret is missing/default
- [x] **Resend webhook:** fail closed if `Resend:WebhookSecret` empty outside Development
- [x] **One workspace IDOR:** require membership (or system admin) on members/invites list
- [x] **Invite/remove member:** require workspace `ADMIN`, not mere membership
- [x] **OpsConversation EF filter:** combine soft-delete **and** tenant predicate (do not replace tenant filter)

## 0.4 Test harness baseline

- [x] Expand `Taskfile.yml` `api:test` to include `Lazuar.ModuleTests`, `Modules.Billing.Tests`, `Modules.Ops.Tests`
- [x] Fix architecture tests to **fail** if expected module assemblies are not loaded (no silent skip)
- [x] Add smoke test: Lhdn outbox row drains after publish (integration or module test with hosted job or direct drain helper)

## 0.5 Contract hygiene starter

- [x] Fix LHDN TypeSpec route: `@route("/lhdn")` not `/api/v1/lhdn` (eliminate double prefix)
- [x] Run `task gen`; verify Kiota/SDK paths hit `/api/v1/lhdn/...` once
- [x] Document or implement portal routes currently in TypeSpec: magic-link, cancel, billing-link (choose one path—no phantoms)
- [x] Align stale docs: `community_subscription` → `commerce_subscription` in payment webhook docs

### Phase 0 acceptance checklist

- [ ] LHDN submit/validate events reach Billing consumers in a local/dev run
- [ ] API key revoke clears cache on multi-path (at least single-instance)
- [ ] Forced outbox handler failure retries then dead-letters (not silent success)
- [ ] Messaging notify not anonymously callable
- [ ] CI runs full known test suite

---

# Phase A — It actually recovers money and records it

**Goal:** Closed loop: failed renewal → past due / dunning → message and/or auto-charge → pay or cancel/suspend → ledger/metrics correct.

**Exit criteria:**

- Vaulted charge failure enters recovery without manual DB edits.
- Off-session success advances subscription and clears dunning when applicable.
- Auto-charge attempts are multi-attempt capable and logged honestly.
- Default dunning copy does not ship unresolved `{{plan_name}}`.
- Inbound payment webhooks are safer (business-key idempotency, failures published, refund path not zero-amount dead code).

## A.1 Payment failure → domain recovery bridge

**Modules:** Payments, Commerce

- [ ] Publish **`GatewayPaymentFailedIntegrationEvent`** from:
  - [ ] `ProcessGatewayWebhookCommandHandler` when adapters map `PAYMENT_FAILED` (do not silent-return only)
  - [ ] `ExecuteOffSessionChargeIntegrationEventHandler` on failed charge (structured reason if available)
- [ ] Commerce handler (or command) on payment failed for subscription renewals:
  - [ ] `MarkAsPastDue` (if not already)
  - [ ] Assign dunning campaign (same priority/product/payment-method rules as engine)
  - [ ] Optionally fire day-0 step immediately or leave to engine catch-up
- [ ] Fix `BillingEngineJob` no-token path event name: use past-due semantics, not mislabeled `subscription.suspended`
- [ ] Stop treating failed vaulted renewals as “ACTIVE forever with past NextBillingDate”

## A.2 Off-session charge correlation (success path)

**Modules:** Payments gateways, Commerce payment-completed handler

- [ ] Always set off-session / PaymentIntent (or CHIP purchase) metadata:
  - [ ] `type=commerce_subscription`
  - [ ] `subscription_id`
  - [ ] `tenant_id`
  - [ ] `dunning_campaign_id` when in dunning
- [ ] Commerce `GatewayPaymentCompletedIntegrationEventHandler`:
  - [ ] Accept recovery for off-session path (not only checkout-session completion)
  - [ ] Advance billing period / clear dunning / `RecordRecovery` when recovering from arrears
  - [ ] Fallback parse of legacy `receipt` only if needed during migration
- [ ] Stripe PI success path: improve fee extraction where feasible (or document gross-only limitation)

## A.3 Charge attempt model vs multi-retry

**Modules:** Commerce domain/schema, DunningEngineJob, BillingEngineJob

- [ ] Resolve unique index conflict on `ChargeAttemptLogs (SubscriptionId, TargetBillingDate)`
  - Prefer multi-row attempts: uniqueness includes attempt number **or** drop unique-on-date
- [ ] Persist success/failure, gateway codes, timestamps per attempt
- [ ] Align dunning `attemptCount < 4` (or campaign-configurable max) with schema
- [ ] BillingEngineJob: first attempt of cycle; dunning owns subsequent retries (document ownership)

## A.4 Dunning engine correctness (before big redesign)

**Modules:** Commerce `DunningEngineJob`, domain, Communications hydrate

- [ ] **Catch-up steps:** fire due steps where `DayOffset <= daysOverdue` and not yet logged (ordered), not only equality
- [ ] **Campaign edit safety:** stop regenerating step IDs in a way that re-fires or orphans `ReminderDispatchLog` (immutable steps for in-flight runs **or** snapshot run instance — depends on D1)
- [ ] Replace dead `CurrentDunningStepIndex` with real progress field used by ops UI (last completed offset / step id)
- [ ] Block or reassign delete of campaign while `CurrentDunningCampaignId` references it; prefer archive
- [ ] Idempotent “generate defaults” (no unbounded duplicates)
- [ ] Publish typed `SubscriptionCanceled` / `SubscriptionSuspended` on final actions (so Communications lifecycle + outbound webhooks fire)
- [ ] Pass **plan_name, amount, currency, days_overdue** into dunning fulfillment payload
- [ ] Communications `FulfillmentRequestedIntegrationEventHandler`: substitute `{{plan_name}}` and amount vars; config-driven portal base URL
- [ ] Fix portal/update-payment links (real tokens where product promises magic links)

## A.5 Inbound payment webhook hardening (money safety)

**Modules:** Payments

- [ ] Business-key / payment-level idempotency strategy (Stripe dual events, etc.)
- [ ] Razorpay: never `Guid.NewGuid()` for EventId; fail closed if no stable id
- [ ] CHIP: do not treat `purchase.preauthorized` as paid
- [ ] Catch unique-violation races → HTTP 200 duplicate
- [ ] Platform top-up: transaction-level idempotency (no double credit)
- [ ] Prefer two-phase later if needed: persist raw webhook → async process (can start as P1 within A)
- [ ] Unknown gateway type → 400 not endless 500 retries

## A.6 Refunds and disputes (minimum viable truth)

**Modules:** Payments, Commerce, Billing, Lhdn

- [ ] Wire a real publisher for `GatewayRefundRequested` with amount + currency + gateway tx id
- [ ] Stop hardcoding refund amount `0` / currency `MYR` in refund handler
- [ ] Align Commerce refund transaction matching with gateway transaction id
- [ ] Refund tax/ledger symmetry (Billing) for partial/full refunds as applicable
- [ ] Dispute path: document scope (utility clawback only vs commerce suspension later)

## A.7 Gateway selection correctness

**Modules:** Commerce checkout / public update-payment

- [ ] Custom payment links: do not hardcode BILLPLZ if product/config implies otherwise
- [ ] Update-payment / arrears checkout: use subscription/product gateway, not default BILLPLZ
- [ ] Document Billplz offline-only auto-charge limitation in product UX copy

## A.8 Billing money bugs tied to recovery/compliance

**Modules:** Billing, Lhdn

- [ ] **Eliminate double LHDN credit deduction** (command-side vs `LhdnDocumentSubmitted` handler)
- [ ] Align LHDN cost to `ICreditCostService` / config (no hardcoded `1`)
- [ ] Utility top-up: single economic story (skip merchant GMV path when `utility_credit_topup`)
- [ ] Financial summary: reduce ABS-driven double-count under reversals (at least for net paths used in ops)

## A.9 Messaging honesty for recovery channel

**Modules:** Messaging, Communications, Program DI

- [ ] Per **D2**: either
  - [ ] Implement real WhatsApp provider behind `IMessagingService`, **or**
  - [ ] Feature-flag WHATSAPP steps off / label ops UI “Email only until WhatsApp connected”
- [ ] Insufficient WhatsApp credits: surface failure to ops/dunning logs (no silent “dispatched”)
- [ ] Wire List-Unsubscribe / `BuildUnsubscribeUrl` for marketing/broadcast emails (compliance)

## A.10 Tests for Phase A

- [ ] Unit: subscription past-due / clear dunning / charge attempt multi-row
- [ ] Unit/integration: payment failed → PAST_DUE + campaign assign
- [ ] Unit: off-session metadata present on adapters (mock)
- [ ] Unit: webhook idempotency fixtures per gateway (signature + duplicate)
- [ ] Integration: recovery payment clears dunning + recovery metrics
- [ ] Unit: LHDN single credit charge path
- [ ] Template variable substitution for dunning payload

### Phase A acceptance checklist

- [ ] Manual test: vaulted renewal fail → subscriber shows PAST_DUE → dunning step sends email → update-payment succeeds → ACTIVE + recovered metric
- [ ] Manual test: AUTO_CHARGE step can attempt more than once per cycle without DB exception
- [ ] Webhook redelivery of same payment does not double-enroll / double-credit
- [ ] Default dunning message body has no raw `{{plan_name}}`

---

# Phase B — Machines can integrate without JWT

**Goal:** Integrator path = console credentials + integration APIs + outbound webhooks; docs teach that path.

**Exit criteria:**

- Workspace admin generates/revokes keys in Ops UI (not JWT paste).
- Keys are scoped; cannot administer the workspace as OrgAdmin.
- Outbound webhooks deliver without exact product-URL match bug.
- Developers hub prioritizes integration products and auth guide.
- LHDN remains first-class; Commerce integrator surface matches **D5**.

## B.1 Authorization model for machine clients

**Modules:** Host `Program.cs`, middleware, Lhdn endpoints

- [ ] Policies: `Integration` / scope-based vs human `OrgAdmin`
- [ ] API key middleware sets `CredentialId`, scopes, `IsTestMode`, tenant
- [ ] Accept `Authorization: Bearer sk_*` and raw `sk_*` (normalize)
- [ ] SDK factories always send correct Authorization format; document once
- [ ] Scope matrix v1 minimum:
  - [ ] `lhdn.documents:write|read`
  - [ ] `lhdn.webhooks:manage` (optional later)
  - [ ] Dashboard-only: keys, certs, payment config
- [ ] Implement **GET `/lhdn/api-keys`** (list metadata, no secret)
- [ ] Rich generate response: id, name, prefix/hint, created_at, plain_key once
- [ ] Store public key hint / last4 for list UI

## B.2 Platform credentials ownership (per D3)

**Modules:** One (preferred) or shared platform store; host middleware

- [ ] Design `ApiCredential` (or equivalent) aggregate: org, name, hash, env, scopes, active, created_by, created_at
- [ ] Migrate or dual-read from `lhdn.DeveloperApiKeys` → platform table
- [ ] Middleware reads platform store (not hard-coded LHDN SQL forever)
- [ ] Revocation event + cache eviction (prefer distributed cache plan for multi-instance)
- [ ] TypeSpec routes for credential CRUD under product-appropriate path (`/one/.../api-keys` or `/platform/credentials` with tenant)
- [ ] Management endpoints: JWT + ADMIN only

## B.3 Ops Developer console — API keys

**Apps:** `ops-page`

- [ ] Nav: Developer → API Keys (alongside Webhooks / Logs)
- [ ] Create test/live key, one-time reveal, copy, revoke
- [ ] List by name/prefix/created/active
- [ ] Deep link to developers docs (`/docs/lhdn` etc.)
- [ ] Fix CreateManualSubscriber and other `as any` only if blocking this workstream (else Phase C contract pass)

## B.4 Outbound customer webhooks — product model (per D4)

**Modules:** One (dispatcher), Commerce emitters, Lhdn (converge later)

### B.4.1 Fix silent drop (P0 product bug)

- [ ] Workspace endpoint receives **workspace events** without requiring product fulfillment URL equality
- [ ] Product free-text URLs either:
  - [ ] Become additional endpoints / optional filters, **or**
  - [ ] Deliver with their own secrets without equality gate
- [ ] Never no-op silently: log + delivery failure row or validate at product save
- [ ] Emit outbound for `custom_payment_link` paid if product claims webhooks

### B.4.2 Delivery quality

- [ ] Multi-endpoint per workspace (schema + API)
- [ ] Event subscription filters (`enabled_events[]`)
- [ ] Signature v1: timestamp + HMAC (Standard Webhooks–compatible if possible)
- [ ] Headers: event type, delivery id, webhook id
- [ ] Enrich payloads: customer email/name (policy), amount/currency, product slug, gateway tx id, status
- [ ] Delivery log: response code, error, attempt timeline; redeliver action (API + UI)
- [ ] Secret: show once on create; GET returns metadata only; rotate endpoint
- [ ] SSRF baseline: HTTPS-only in prod; block obvious private ranges (configurable override)
- [ ] Job claim / SKIP LOCKED for multi-instance safety

### B.4.3 Event catalog v1 (document + emit)

- [ ] `subscription.activated|resumed|suspended|canceled`
- [ ] `subscription.past_due` (or document mapping from current paths)
- [ ] `order.completed`
- [ ] `payment.succeeded` / `payment.failed` (if D5/webhooks-first)
- [ ] LHDN: `invoice.valid|invalid` (+ submitted/cancelled when ready)
- [ ] Align LHDN wire names with TypeSpec/list DTOs (`validated` vs `valid` debt)
- [ ] Persist or remove fictional `events[]` on LHDN register

### B.4.4 Unify delivery stacks (target)

- [ ] Route LHDN outbound through shared durable dispatcher (retire fire-and-forget as sole path)
- [ ] Shared signing + retry policy for all customer webhooks

## B.5 Ops Developer console — webhooks UX

**Apps:** `ops-page`

- [ ] Multi-endpoint UI, event multi-select, secret rotate, test ping
- [ ] Delivery detail + resend
- [ ] Product form: remove misleading webhook textarea **or** wire to real multi-endpoint model
- [ ] LHDN webhooks UI under Developer or Invoicing when LHDN UI un-lobotomized

## B.6 Developers-page as integration hub

**Apps:** `developers-page`, `packages/api-spec`

- [ ] Auth guide page: keys vs JWT; never embed user JWT in ERP
- [ ] Re-curate public products: LHDN primary; Commerce public when ready; demote/gate Ops
- [ ] Remove billing contamination from `docs-one` / `docs-ops` (or label internal)
- [ ] Wire `docs-commerce.tsp` into build + Scalar route + landing card
- [ ] Quickstart: first e-invoice (curl + TS/.NET); verify `X-Lazuar-Signature`
- [ ] Surface LHDN SDK install links; fix Bearer/idempotency docs
- [ ] Event catalog page for outbound webhooks
- [ ] Align production server URLs with deploy (`hub.lazuar.com` / real API host)
- [ ] Set real OpenAPI `info.version` (not `0.0.0`) for product docs

## B.7 Commerce integrator surface (per D5)

**Modules:** Commerce, TypeSpec, optional new integration route group

### If D5 = webhooks + public checkout first

- [ ] Document public commerce routes as integrator-facing (buy links, portal token rules)
- [ ] Ensure webhook catalog covers unlock/revoke without M2M admin

### If D5 = include M2M

- [ ] Key-authenticated group for: list/create products (subset), create checkout session URL, get subscription status, cancel/pause dunning, list transactions
- [ ] TypeSpec product docs for that group only
- [ ] Map endpoints → Commands with tenant from credential (never body org spoof)

## B.8 LHDN integration completeness (keep gold standard)

- [ ] Implement or remove TypeSpec ops: taxpayer validate, list keys (keys list shared with B.1)
- [ ] Tenant LHDN config CRUD API (TIN, BRN, MSIC, env, client credentials) — not only cert PUT
- [ ] Get document by internal id without “last 100” scan
- [ ] Supplier legal name/address on tenant config bound into templates (stop hardcoded sample address)
- [ ] Encrypt MyInvois secrets / PFX at rest (or explicit risk acceptance + follow-up)

## B.9 Tests for Phase B

- [ ] API key auth: valid/invalid/revoked; scope denies admin routes
- [ ] Webhook delivery: enqueue on lifecycle event without product URL equality
- [ ] Signature verify unit test for receivers (sample in docs or test fixture)
- [ ] Generate key once-show secret not returned on list

### Phase B acceptance checklist

- [ ] New integrator can create test key in Ops, submit LHDN sandbox doc, see status webhook delivered
- [ ] Commerce subscription activate delivers workspace webhook without product URL matching
- [ ] Stolen/mis-scoped key cannot mint more keys or change payment config
- [ ] Developers hub explains auth + one happy path without reading ADRs

---

# Phase C — We can operate and trust it

**Goal:** Operational reliability, financial truth for sold paths, isolation, contract/UI honesty, multi-instance safety.

**Exit criteria:**

- Financial paths used in production do not double-count or lie on happy/refund paths.
- Tenant HTTP paths fail closed without tenant context where required.
- Phantom frontend/backend routes eliminated for critical ops/portal flows.
- Domain workers safe under single worker deployment strategy (or SKIP LOCKED claims).
- Core money/auth tests gate CI.

## C.1 Financial truth (Billing)

- [ ] B2C consolidation selection: fix filter so normal `B2C_RECEIPT` sales can consolidate (or dedicated consolidation state field)
- [ ] Separate receipt number vs LHDN consolidation status if dual-use field is the root cause
- [ ] Refunds reverse tax liability proportionally where applicable
- [ ] Deferred revenue: create schedules when booking deferred **or** delete dead job/entity to reduce lie surface
- [ ] Implement or remove TypeSpec `net-profit` and summary date filters
- [ ] Chargeback: reverse ledger where required, not only wallet units for utility
- [ ] Account type constants/enums (reduce magic strings)
- [ ] Stop or formalize cross-schema Dapper joins (Billing ↔ commerce/crm)

## C.2 Tenant isolation hardening

- [ ] Fail-closed global filter strategy for HTTP request scopes (no “empty tenant = all rows”)
- [ ] Expand mandatory tenant middleware beyond `/admin/` to `/lhdn`, `/ops`, and other OrgAdmin modules
- [ ] Webhook resource ownership: session/subscription `OrganizationId` must match path tenant
- [ ] Public commerce: bind checkout status / magic token mint to safer proof (slug + sig / email OTP)
- [ ] Draft PDF public route: require HMAC like final documents
- [ ] Presigned storage: reject empty TenantId
- [ ] Architecture tests: no second `HasQueryFilter` without tenant clause; allowlist anonymous routes

## C.3 Commerce product completeness (ops/portal truth)

- [ ] Implement or remove ops UI actions: cancel, ban, record-payment, refund, export
- [ ] Portal cancel / magic-link / billing-link vs TypeSpec (implement or remove from portal UI)
- [ ] Coupon: confirm reservation on paid completion; release on session expiry worker
- [ ] Checkout session expiry worker
- [ ] Offline mark-paid / custom checkout: define entitlement outcome (Order/Sub/tx log consistency)
- [ ] Enforce CheckoutConfiguration flags at initiate (or remove from UI)
- [ ] Stats stubs: fill or hide zeroed KPIs
- [ ] Admin portal-link: add to TypeSpec; optional ops button

## C.4 Communications / CRM maturity

- [ ] CRM outbox consumers for anonymize (Commerce cancel/suppress as required)
- [ ] Consent default false; explicit opt-in
- [ ] Wire or delete orphan templates (welcome, abandoned cart) vs fulfillment events
- [ ] Message delivery log aggregate (channel, status, provider id) — minimum for support
- [ ] Encrypt Resend API keys; mask on GET
- [ ] Fix template “reset” to re-seed defaults, not blank

## C.5 Background workers multi-instance

- [ ] Document deploy rule: single API replica **or** worker process replica=1 until claims exist
- [ ] Add SKIP LOCKED / lease claims for: BillingEngine, DunningEngine, BroadcastFanout, OutboundWebhook, Lhdn submission/poll
- [ ] Per-subscription transaction batches in dunning/billing (avoid all-tenant single SaveChanges failure)
- [ ] B2cConsolidation: catch-up if instance was down on the 28th
- [ ] Configurable intervals via options (optional)

## C.6 Observability

- [ ] Metrics: outbox lag, dead letters, webhook failed, LHDN stuck PENDING/SUBMITTED, dunning cancels
- [ ] Correlation id middleware + log enrichment
- [ ] Health readiness: DB; optional outbox lag threshold
- [ ] Structured success logs on payment webhook process (event id, provider, tx id)

## C.7 Secrets and BYOK

- [ ] Encrypt payment gateway secrets at rest (or KMS plan)
- [ ] Encrypt LHDN client secrets / PFX
- [ ] Remove committed secrets from appsettings; Key Vault / user secrets only
- [ ] Payment config: soft-disable gateway without delete (IsActive or equivalent)

## C.8 Contract & frontend honesty pass

- [ ] `task gen` + CI `git diff --exit-code` on generated clients
- [ ] Enumerate Minimal API vs OpenAPI paths; allowlist intentional internal routes
- [ ] Ops: remove `as any` phantom routes; fix CreateManualSubscriber DTO
- [ ] Fix LHDN cancel path double-prefix in ops invoicing UI
- [ ] OpenAPI for ops stream / system-message or stop untyped fetch
- [ ] Route or delete unrouted modules (billing profile, invoicing, chat)
- [ ] Login default path fix (`/commerce/dashboard`)
- [ ] README / ADR watermark: 014/020 vs 021/023 truth; update Billing README golden rule

## C.9 Tests for Phase C

- [ ] Postgres concurrent credit deduct + idempotency
- [ ] Ledger balance matrix payment/refund/top-up
- [ ] B2C consolidation eligibility
- [ ] Tenant isolation: cross-tenant IDOR negative tests
- [ ] Coupon reserve/confirm/expire
- [ ] Expand architecture boundary tests (BuildingBlocks/SharedKernel rules as needed)

### Phase C acceptance checklist

- [ ] Support can answer “was this payment fulfilled?” from logs/tables
- [ ] Ops UI actions either work end-to-end or are gone
- [ ] Horizontal scale plan documented; no silent double dunning cancel under two replicas without claims
- [ ] Financial summary on refund scenario is believable for ops dashboard

---

# Phase D — Differentiation

**Goal:** Product differentiators on top of a trusted core—not before it.

**Exit criteria:** Marketing claims match production channels and flexibility; compliance UI can reappear when ready.

## D.1 WhatsApp productization (if D2 committed)

- [ ] Meta Cloud API client (token, phone_number_id, WABA)
- [ ] Template messages vs session messages; utility category for dunning
- [ ] Interactive CTA buttons to update-payment / FPX checkout
- [ ] Tenant or platform credential model for WhatsApp
- [ ] Delivery/status webhooks; phone E.164 normalization; phone suppressions
- [ ] Credits + failure UX in ops

## D.2 Dunning flexibility (after closed loop)

- [ ] Campaign run snapshot / versioning (if not done in A under D1)
- [ ] Freeform day offsets + optional time-of-day + merchant timezone
- [ ] Multi-action same day without FirstOrDefault loss
- [ ] Decline-code-aware retry rules (static rules first)
- [ ] Ops: assign campaign, force retry, skip step, per-sub activity timeline
- [ ] Analytics: funnel by step (sent → paid → churned)
- [ ] Optional restore template library link vs pure inline copy

## D.3 Compliance CaaS re-surface (ADR 023 reverse)

- [ ] Un-hide B2B TIN / quotes / tax invoice UX when backend metrics trusted
- [ ] B2C consolidation job validated in sandbox month-end dry run
- [ ] V1.1 signing pipeline or documented JSON-only path for signed docs
- [ ] Schematron / business-rule validation phase 2
- [ ] Multi-country tax: only after LHDN loop is production-trusted

## D.4 Commerce / platform polish

- [ ] Invoice abstraction if multi-item open balances required
- [ ] PWYW / PricingModel enforcement or remove from product
- [ ] Welcome/fulfillment messaging on order/subscription activate
- [ ] Xero (ADR 021 keep) as separate plan once ledger trusted
- [ ] Additional gateways only after core four stable (fee fidelity, refunds, webhooks)

## D.5 Developer platform maturity

- [ ] Authenticated “Try it” with test keys (optional)
- [ ] API request logs / usage vs credit wallet
- [ ] CI publish SDKs on tag; changelog
- [ ] OAuth2 client_credentials when third-party apps need delegated access
- [ ] Restricted keys, IP allowlists, key expiry, last_used async updates
- [ ] Separate Internal OpenAPI vs Public OpenAPI generation permanently

### Phase D acceptance checklist

- [ ] README claims (WhatsApp dunning, webhooks, API-first) match demoable production paths
- [ ] Compliance path can be demoed end-to-end for MY B2C/B2B without “backend only” caveats
- [ ] Integrator onboarding measured in minutes, not “read 20 ADRs”

---

# Cross-phase workstreams (track in parallel)

These appear in multiple phases; owners can track once.

| Workstream | Primary phases | Owner hint |
|------------|----------------|------------|
| Payments webhooks + off-session | A | Payments + Commerce |
| Dunning engine | A, D | Commerce |
| Outbox reliability | 0, C | BuildingBlocks |
| API credentials | 0, B | One + Host + Lhdn |
| Outbound webhooks | B, C | One + Commerce |
| Billing credits/ledger | A, C | Billing |
| TypeSpec / gen / docs | 0, B, C | api-spec + developers-page |
| Ops / portal UI | B, C | ops-page + portal-page |
| Tests / CI | 0, A, B, C | all |
| WhatsApp | A (honesty), D (product) | Messaging + Communications |

---

# Suggested milestone tags

Use in PRs/commits for traceability:

- `phase-0/...`
- `phase-a/...`
- `phase-b/...`
- `phase-c/...`
- `phase-d/...`

Example: `phase-a/commerce: past-due on off-session failure`

---

# Definition of done per checklist item

An item is complete only when:

1. Code merged (or explicit “won’t do” with ADR note)
2. TypeSpec/clients updated if edge changed
3. Test or manual verification recorded
4. Docs/README/marketing claim updated if the item changes a public promise

---

# Related documents

| Doc | Role |
|-----|------|
| `docs/001-gaps/00-what-we-need-to-do-next.md` | Narrative intent |
| `docs/001-gaps/README.md` | Index of deep analyses |
| `docs/architecture-decision-log/019`–`023` | Product pivots |
| `apps/lazuar-api/docs/001-cross-module-communication.md` | Event/outbox rules (update in Phase 0) |
| `apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md` | Webhook cutover playbook |

---

*This checklist implements the suggestions in `docs/001-gaps/00-what-we-need-to-do-next.md`. Update checkboxes in-place as work lands; split into per-phase tickets only after Phase 0–A sequencing is agreed.*
