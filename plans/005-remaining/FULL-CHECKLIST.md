# 005 Remaining — FULL checklist (all phases)

**Source of truth:** individual files in this directory. This file is a concatenated export.

**Generated:** 2026-08-09


---

# 005 Remaining — detailed implementation checklists

**Status:** Ready to execute  
**Date:** 2026-08-09  
**Style:** Many **small** phase files (not fat catch-alls). One phase ≈ one PR (or smaller).  
**How-to analyses:** parent `../01-…`–`../10-…`  
**Prior scaffold:** `../../004-maintenance/checklists-future/` (F00–F16) — this folder **supersedes for execution detail**; keep F-map for orientation.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One phase / one leak / one ownership move per PR | Land keys + webhooks + BB + SQL in one branch tip |
| Parallel **tracks** after R00 | Parallel **inside** Keys (F03 before F02) |
| Honor product/calendar gates | Remove dual-read before migration |

## Track map

```text
R00 Align
  ├─ Track Keys:     R01 → R02 → R03 → R04 → (wait) → R05
  ├─ Track SQL:      R10 → R11 → R12 → R13 → R14 → R15 → R16 → R17
  ├─ Track TypeSpec: R20 → R21 → R22 → R23 → R24 → R25
  ├─ Track BB:       R30 → R31 → R32 → R33 → R34 → R35
  ├─ Track Webhooks: R40 → R41 → R42 → R43
  ├─ Track Polish:   R50 → R51 → R52 → R53
  └─ Track Extract:  R60 (default SKIP)
R99 Definition of done
```

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| R00 | [`r00-wave-align.md`](./r00-wave-align.md) | Which tracks this wave |
| R99 | [`r99-definition-of-done.md`](./r99-definition-of-done.md) | Close remaining program |

### Track Keys (bullet 1 · analysis 01)

| ID | File | Intent |
|----|------|--------|
| R01 | [`r01-keys-code-inventory.md`](./r01-keys-code-inventory.md) | Refresh dual-read / mint map |
| R02 | [`r02-keys-data-inventory.md`](./r02-keys-data-inventory.md) | Staging/prod row counts |
| R03 | [`r03-keys-migrator-implement.md`](./r03-keys-migrator-implement.md) | Idempotent migrator |
| R04 | [`r04-keys-migrate-staging-prod.md`](./r04-keys-migrate-staging-prod.md) | Run migration |
| R05 | [`r05-keys-one-only-middleware.md`](./r05-keys-one-only-middleware.md) | Remove dual-read |
| R06 | [`r06-keys-table-drop.md`](./r06-keys-table-drop.md) | Drop Lhdn table (≥30d) |

### Track SQL (bullet 4 · analysis 06)

| ID | File | Intent |
|----|------|--------|
| R10 | [`r10-sql-inventory-refresh.md`](./r10-sql-inventory-refresh.md) | Re-grep, ticket table |
| R11 | [`r11-sql-l01-document-published.md`](./r11-sql-l01-document-published.md) | L-01 Communications |
| R12 | [`r12-sql-l02-platform-superadmin.md`](./r12-sql-l02-platform-superadmin.md) | L-02 Payments→one |
| R13 | [`r13-sql-l03-arrears-update.md`](./r13-sql-l03-arrears-update.md) | L-03 Commerce arrears |
| R14 | [`r14-sql-l05-document-lookup-crm.md`](./r14-sql-l05-document-lookup-crm.md) | L-05 CommerceDocumentLookup |
| R15 | [`r15-sql-l04-dead-template-sql.md`](./r15-sql-l04-dead-template-sql.md) | L-04 delete dead SQL |
| R16 | [`r16-sql-l06-metrics-handoff.md`](./r16-sql-l06-metrics-handoff.md) | L-06 → metrics track R35 |
| R17 | [`r17-sql-l07-apikey-handoff.md`](./r17-sql-l07-apikey-handoff.md) | L-07 → keys track R05 |

### Track TypeSpec Wave B (bullet 6 · analysis 08)

| ID | File | Intent |
|----|------|--------|
| R20 | [`r20-tsp-dual-dto-products.md`](./r20-tsp-dual-dto-products.md) | Product create/update DTOs |
| R21 | [`r21-tsp-dual-dto-refund.md`](./r21-tsp-dual-dto-refund.md) | Record refund DTO |
| R22 | [`r22-tsp-broadcast-preview-status.md`](./r22-tsp-broadcast-preview-status.md) | Preview/status honesty |
| R23 | [`r23-tsp-billing-pdf-honesty.md`](./r23-tsp-billing-pdf-honesty.md) | Signed PDF |
| R24 | [`r24-tsp-payments-security-schemes.md`](./r24-tsp-payments-security-schemes.md) | Docs security |
| R25 | [`r25-tsp-path-honesty-ci.md`](./r25-tsp-path-honesty-ci.md) | OpenAPI ⊆ Minimal + allowlist |

### Track BuildingBlocks (bullet 3 · analyses 03–05)

| ID | File | Intent |
|----|------|--------|
| R30 | [`r30-bb-port-hygiene.md`](./r30-bb-port-hygiene.md) | Ports in Application |
| R31 | [`r31-bb-llm-factory-to-ops.md`](./r31-bb-llm-factory-to-ops.md) | Factory/policies/title → Ops |
| R32 | [`r32-bb-agent-tools-to-ops-contracts.md`](./r32-bb-agent-tools-to-ops-contracts.md) | AgentTool + prompt port |
| R33 | [`r33-bb-magic-link-to-commerce.md`](./r33-bb-magic-link-to-commerce.md) | Magic link shapes |
| R34 | [`r34-bb-email-messaging-to-messaging.md`](./r34-bb-email-messaging-to-messaging.md) | Email/IMessagingService |
| R35 | [`r35-bb-metrics-plugins.md`](./r35-bb-metrics-plugins.md) | Contributors + schema reg |

### Track Webhooks (bullet 2 · analysis 02)

| ID | File | Intent |
|----|------|--------|
| R40 | [`r40-webhooks-product-lock.md`](./r40-webhooks-product-lock.md) | Signing/payload/routing |
| R41 | [`r41-webhooks-registry-backfill.md`](./r41-webhooks-registry-backfill.md) | Lhdn → One endpoints |
| R42 | [`r42-webhooks-enqueue-path.md`](./r42-webhooks-enqueue-path.md) | A1 enqueue to One outbox |
| R43 | [`r43-webhooks-retire-fire-and-forget.md`](./r43-webhooks-retire-fire-and-forget.md) | Remove fire-and-forget |

### Track Polish (bullet 6 · analysis 09)

| ID | File | Intent |
|----|------|--------|
| R50 | [`r50-polish-testsupport-batch.md`](./r50-polish-testsupport-batch.md) | TestSupport N tests |
| R51 | [`r51-polish-lhdn-gateway-partials.md`](./r51-polish-lhdn-gateway-partials.md) | LhdnGatewayAdapter |
| R52 | [`r52-polish-llm-stream-partial.md`](./r52-polish-llm-stream-partial.md) | LLM stream split |
| R53 | [`r53-polish-gateway-common-outbox-di.md`](./r53-polish-gateway-common-outbox-di.md) | GatewayCommon + outbox DI pilot |

### Track Extract (bullet 5 · analysis 07)

| ID | File | Intent |
|----|------|--------|
| R60 | [`r60-extract-gate-only.md`](./r60-extract-gate-only.md) | Default SKIP |

## Suggested first wave (default R00)

1. R00  
2. Parallel: **R01–R02** (keys invent), **R10** (SQL invent), **R20–R21** (easy TypeSpec), **R30** (BB ports)  
3. Then: **R03–R04** keys migrate, **R11–R15** SQL fixes, **R31+** BB  
4. Then: **R05** One-only (after migrate)  
5. Webhooks only if R40 locked: R41–R43  
6. R50–R53 polish opportunistic  
7. R06 after 30d; R99 close-out  

## PR hygiene (every phase)

- [ ] Read linked analysis section first  
- [ ] Single intent in PR title  
- [ ] Tests / `task gen` as needed  
- [ ] Architecture tests if boundaries move  
- [ ] Update `../FUTURE-WORK.md` status when a track finishes  
- [ ] No outbox type renames without migration note  


---

# R00 — Wave align

**Goal:** Choose which tracks run this execution wave.  
**Analysis:** `../10-program-sequencing-and-risks.md`  
**Output:** Fill answers below or `plans/005-remaining/wave-decisions.md`

---

## R00.1 Track selection (yes / no / later)

- [ ] Keys R01–R06: ________
- [ ] SQL R10–R17: ________
- [ ] TypeSpec R20–R25: ________
- [ ] BuildingBlocks R30–R35: ________
- [ ] Webhooks R40–R43: ________ (needs product for R40)
- [ ] Polish R50–R53: ________
- [ ] Extract R60: default **no** unless product trigger: ________

## R00.2 Delivery

- [ ] Branch strategy: long-lived `chore/remaining-005` **or** stacked PRs to main: ________
- [ ] One phase ≈ one PR (confirm)
- [ ] Confirm dual-read keys not removed before R04 migrate complete
- [ ] Confirm no second Lhdn webhook stack (decision B rejected)

## R00.3 Calendar / freezes still in force

- [ ] Keys dual-read until 2026-11-30 unless row count 0 (early OK)
- [ ] Revenue recognition stays parked
- [ ] WhatsApp / multi-channel stays frozen
- [ ] No new modules without R60 gate

## R00.4 Exit

- [ ] Ordered start list written (e.g. R01 + R10 + R20)
- [ ] Team unblocked to start first phase


---

# R01 — Keys code inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`  
**Goal:** Confirm current dual-read / mint / revoke map before data work.  
**No production cutover in this phase.**

---

## R01.1 Middleware

- [ ] Open `ApiKeyAuthenticationMiddleware` (or current path under host Middleware)
- [ ] Document: One lookup first SQL (table/columns)
- [ ] Document: Lhdn dual-read second SQL
- [ ] Document: 401 body on miss
- [ ] Document: cache key format + TTL
- [ ] Confirm cutover date comments still present

## R01.2 Mint / list / revoke

- [ ] One `IApiCredentialService` (or equivalent) is only mint path
- [ ] Lhdn `/api-keys` is façade over One (no insert into `DeveloperApiKeys`)
- [ ] Aura provision mints One credentials only
- [ ] Revoke: One event publisher exists
- [ ] Dual subscribe for Lhdn revoke still in composition? (note location)

## R01.3 Dead write paths

- [ ] Grep `DeveloperApiKey` / `AddDeveloperApiKey` / insert into `lhdn.DeveloperApiKeys`
- [ ] List any residual write capability (should be none for app mint)

## R01.4 Tests that encode dual-read

- [ ] List tests that seed Lhdn-only keys
- [ ] List tests for dual revoke handlers
- [ ] Note which must change in R05

## R01.5 Exit

- [ ] Short inventory note in PR / `plans/005-remaining/r01-notes.md`
- [ ] No behavior change required (docs-only OK if already accurate)


---

# R02 — Keys data inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`, `../../004-maintenance/api-key-cutover-design.md`  
**Goal:** Staging/prod counts; early-cutover decision.

---

## R02.1 Queries (run staging then prod)

- [ ] Count active `lhdn."DeveloperApiKeys"` where `IsActive = true`
- [ ] Count active `one."ApiCredentials"` where `IsActive = true`
- [ ] Count **active_legacy_only**: Lhdn active hash **not** in One
- [ ] Count inactive Lhdn rows (migrate all vs active-only — record choice: ________)
- [ ] Sample scopes not in One allowlist (quarantine list)

## R02.2 Record results

| Env | Active Lhdn | Active One | Active legacy-only | Notes |
|-----|-------------|------------|--------------------|-------|
| Staging | | | | |
| Prod | | | | |

## R02.3 Decision

- [ ] If prod **active_legacy_only = 0**: mark **accelerate** → R03 may be no-op / verify-only; R05 can proceed after staging One-only smoke
- [ ] If prod **active_legacy_only > 0**: R03 migrator required before R05
- [ ] Sign-off for accelerate: ________ (name/date) if used

## R02.4 Exit

- [ ] Numbers committed in plan note or design doc appendix
- [ ] R03 go / no-go clear


---

# R03 — Implement API key migrator

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md` § migration  
**Depends on:** R01, R02  
**Do not:** Remove dual-read (R05)

---

## R03.1 Implementation choice

- [ ] Choose: hosted one-shot job **or** ops SQL/script **or** admin command: ________
- [ ] Idempotent on `KeyHash` (skip if already in One)
- [ ] Copy fields: KeyHash, Prefix, KeyHint, Scopes, OrganizationId, Name, IsActive, CreatedAt
- [ ] CreatedByUserId = null for migrated
- [ ] Preserve Id if design requires; else new Guid (document choice: ________)
- [ ] Scope quarantine log for unknown scopes
- [ ] Dry-run mode if possible (count only)

## R03.2 Safety

- [ ] No plaintext key material logged
- [ ] Transaction per batch or single txn documented
- [ ] Failure leaves dual-read still valid

## R03.3 Tests

- [ ] Unit/module: empty Lhdn → no-op
- [ ] Unit: Lhdn row copies to One
- [ ] Unit: re-run idempotent
- [ ] Unit: collision hash already in One → skip/update policy documented
- [ ] Unit: unknown scope quarantine behavior

## R03.4 Docs

- [ ] Runbook section in design doc or ops README: how to run migrator
- [ ] Rollback: dual-read still on; no table drop

## R03.5 Exit

- [ ] Migrator merged; dual-read still enabled
- [ ] Ready for R04 execute on staging


---

# R04 — Execute key migration (staging then prod)

**Track:** Keys · **Depends on:** R03 (or R02 accelerate with zero rows)  
**Do not:** Ship One-only middleware in this phase unless combined with R05 intentionally after verify

---

## R04.1 Staging

- [ ] Snapshot / note DB backup approach
- [ ] Run dry-run if available; record counts
- [ ] Run migrator
- [ ] Verify `active_legacy_only` → 0 (or accepted remainder: ________)
- [ ] Auth smoke with a real/staging key that was Lhdn-only (should still work via dual-read **or** One after copy)
- [ ] List/revoke UI shows migrated keys
- [ ] Fix quarantine rows

## R04.2 Production

- [ ] Change window scheduled
- [ ] Backup / point-in-time recovery note
- [ ] Run migrator
- [ ] Record before/after counts in PR or ops log
- [ ] Auth smoke sample of integrators / smoke keys
- [ ] Monitor 401 rates 24h (still dual-read)

## R04.3 Exit

- [ ] Prod `active_legacy_only` = 0 (or signed residual list)
- [ ] R05 unblocked


---

# R05 — One-only middleware (remove dual-read)

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md` § F03  
**Depends on:** R04 complete (or accelerate waiver from R02)

---

## R05.1 Preflight

- [ ] Prod/staging migration residual accepted
- [ ] Staging already running candidate build with One-only if possible

## R05.2 Code

- [ ] Remove Lhdn SQL branch from `ApiKeyAuthenticationMiddleware`
- [ ] Remove dual Lhdn revoke subscription from host composition
- [ ] Keep One revoke → cache eviction only
- [ ] Assert no app path inserts `lhdn.DeveloperApiKeys`
- [ ] Lhdn key HTTP: One façade only (or 410/deprecate if product wants)

## R05.3 Tests

- [ ] Update/remove tests that relied on Lhdn-only dual-read
- [ ] One credential auth green
- [ ] Lhdn-only seed (if any test left) expects **401**
- [ ] Architecture / module tests green

## R05.4 Docs

- [ ] One/Lhdn README: dual-read closed + date
- [ ] `api-key-cutover-design.md` / FUTURE-WORK FW-1: One-only live
- [ ] Integrator note if any public dual-read messaging existed

## R05.5 Deploy / monitor

- [ ] Deploy staging → smoke
- [ ] Deploy prod → watch API key 401s
- [ ] Rollback plan: re-enable dual-read commit (document)

## R05.6 Exit

- [ ] One-only in prod
- [ ] Start 30-day clock for R06


---

# R06 — Drop/archive `lhdn.DeveloperApiKeys`

**Track:** Keys · **Depends on:** R05 live ≥ **30 days** (or signed waiver)  
**Analysis:** `../01-api-key-one-only-cutover.md` § F04

---

## R06.1 Preflight

- [ ] One-only since: ________ (≥30d? yes/waiver)
- [ ] Grep: no read/write of DeveloperApiKeys in app code
- [ ] Outbox: no residual Lhdn revoke events needed

## R06.2 Migration

- [ ] EF migration drop **or** rename to archive
- [ ] Remove dead domain/repo/DI for DeveloperApiKey if unused
- [ ] Clean Lhdn module leftovers

## R06.3 Exit

- [ ] Table gone/archived
- [ ] FW-1 fully closed in FUTURE-WORK.md


---

# R10 — Cross-schema SQL inventory refresh

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md`  
**Goal:** Confirm L-01…L-07 still accurate; open ticket table for R11+  
**No fixes in this phase** (except optional drive-by docs)  
**Live table:** [`cross-schema-leaks-live.md`](./cross-schema-leaks-live.md)  
**Verified:** 2026-08-09

---

## R10.1 Re-grep

- [x] Schema-qualified `FROM`/`JOIN` across modules
- [x] Dapper / `FromSqlRaw` / `NpgsqlCommand` foreign schema
- [x] `PlatformMetricsCollector` multi-schema + product SQL
- [x] Host middleware dual-read (L-07 / keys)

## R10.2 Reconcile with 06 analysis

- [x] L-01 DocumentPublished still present? path: `Modules/Communications/.../DocumentPublishedIntegrationEventHandler.cs` (**present**)
- [x] L-02 PlatformEndpoints GlobalUsers? `Modules/Payments/.../PlatformEndpoints.cs` (**present**)
- [x] L-03 PublicArrears multi-schema? `Modules/Commerce/.../PublicArrearsEndpoints.cs` (**present**)
- [x] L-04 dead GetDefaultTemplateIdsAsync? `CommerceRepository.cs` (**present**, dead callers)
- [x] L-05 CommerceDocumentLookup CRM join? `CommerceDocumentLookup.cs` (**present**)
- [x] L-06 metrics? `PlatformMetricsCollector.cs` (**present**)
- [x] L-07 dual-read keys? **FIXED** by R05 — One-only; no `LhdnLookupSql`
- [x] Any **new** leaks found? list: **none** on product paths (host `SqlApiKeyMigrationStore` is R03 tooling, not a new L-##)

## R10.3 Priority order for this wave

- [x] Ordered fix list (default: R11→R15, R16 handoff metrics, R17 handoff keys): **R11 L-01 → R12 L-02 → R13 L-03 → R14 L-05 → R15 L-04 → R16/R35 L-06 → R17 complete (L-07 fixed)**

## R10.4 Exit

- [x] `plans/005-remaining/cross-schema-leaks-live.md` or updated section in 06 with “verified YYYY-MM-DD”
- [x] R11 unblocked


---

# R11 — Fix L-01 DocumentPublished cross-schema SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-01  
**File (verify):** `Communications/.../DocumentPublishedIntegrationEventHandler.cs`  
**Problem:** JOIN/read `billing` + `one` + `commerce` from Communications

---

## R11.1 Design

- [x] Prefer enrich `DocumentPublishedIntegrationEvent` at publish site with fields Comms needs
- [x] Or add Contracts query ports on owning modules (document choice: **event denorm at publish; not query ports**)
- [x] List fields currently loaded via SQL: **TenantSlug, BusinessName, CustomerName, CustomerEmail**

## R11.2 Implement

- [x] Publisher (Billing/Commerce path) supplies customer/doc fields
- [x] Handler uses event payload only (no foreign-schema SQL)
- [x] Delete Dapper multi-schema query

## R11.3 Tests

- [x] Handler unit/module test with enriched event
- [x] Regression: document published still triggers communications behavior

## R11.4 Exit

- [x] Grep handler: no foreign schema SQL
- [ ] Single-purpose PR merged


---

# R12 — Fix L-02 Payments platform super-admin SQL into `one`

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-02  
**File (verify):** `Payments/.../PlatformEndpoints.cs`  
**Problem:** Dapper/SQL against `one.GlobalUsers` (or similar) from Payments

---

## R12.1 Design

- [x] Define One Contracts query/auth port for super-admin validation (or reuse existing One service)
- [x] Payments only calls Contracts — no `one.` SQL

## R12.2 Implement

- [x] Add/use One port implementation
- [x] Replace PlatformEndpoints SQL
- [x] DI registration

## R12.3 Tests

- [x] Platform endpoint auth paths covered
- [x] No Payments project SQL string referencing `one.`

## R12.4 Exit

- [x] L-02 closed; PR focused only on this leak family


---

# R13 — Fix L-03 Commerce arrears update-payment multi-schema JOIN

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-03  
**File (verify):** `Commerce/.../PublicArrearsEndpoints.cs`  
**Problem:** JOIN `crm` + `one` (and commerce) in one query

---

## R13.1 Design

- [x] Split into commerce-owned SQL + `ICrmQueryService` + `IOneQueryService` (or enrich domain)
- [x] Document data flow for arrears update-payment

## R13.2 Implement

- [x] Replace multi-schema Dapper with port composition
- [x] Preserve HTTP contract/behavior

## R13.3 Tests

- [x] Arrears / update-payment tests green
- [x] Tenant isolation preserved

## R13.4 Exit

- [x] No `crm.`/`one.` in that endpoint SQL


---

# R14 — Fix L-05 CommerceDocumentLookup CRM JOIN

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-05  
**File (verify):** Commerce document lookup service used by Billing  
**Problem:** CRM joined inside Commerce port implementation

---

## R14.1 Design

- [x] Session/document SQL stays commerce-only
- [x] Customer profile fields via `ICrmQueryService`

## R14.2 Implement

- [x] Split query; compose results in lookup service
- [x] Keep `ICommerceDocumentLookup` external contract stable for Billing

## R14.3 Tests

- [x] Billing draft/final document tests still pass
- [x] Lookup unit tests updated

## R14.4 Exit

- [x] No `crm.` SQL inside CommerceDocumentLookup


---

# R15 — Remove L-04 dead cross-schema template SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-04  
**Problem:** `GetDefaultTemplateIdsAsync` (or equivalent) queries `communications.MessageTemplates` but has no callers  
**Notes:** `r15-notes.md`

---

## R15.1 Confirm dead

- [x] Grep all callers — zero production callers
- [x] Confirm safe to delete method + any private helpers only used by it

## R15.2 Delete

- [x] Remove dead method/SQL
- [x] Remove unused usings

## R15.3 Exit

- [x] Build green; no behavior change


---

# R16 — L-06 metrics SQL handoff

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-06, `../05-bb-metrics-plugins.md`  
**Goal:** Do **not** half-fix metrics here — link to R35  
**Notes:** `r16-notes.md` · **Status:** complete (handoff R35)

---

## R16.1 Confirm still present

- [x] `PlatformMetricsCollector` still has hardcoded schemas + `lhdn.TaxDocuments` (or current equivalent)  
  — Confirmed 2026-08-09: `ModuleSchemas` (9) + `QueryLhdnStuckAsync` → `lhdn."TaxDocuments"`

## R16.2 Handoff

- [x] Ensure R35 is on the wave plan if L-06 is P1 this wave  
  — BB track YES; R35 after R16 (`wave-decisions.md`)
- [x] If metrics out of wave: ticket id ________ and stop  
  — N/A (R35 **in** wave)
- [x] Do not leave a partial “move one query” without contributor design  
  — No app code in R16

## R16.3 Exit

- [x] Explicit: fixed in R35 **or** deferred ticket  
  — **Fixed in R35** (`checklists/r35-bb-metrics-plugins.md`)


---

# R17 — L-07 API key dual-read handoff

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-07  
**Goal:** Dual-read was intentional until R05 — not a Contracts-port fix. **R05 done:** confirm dual-read removed; no separate SQL PR.  
**Notes:** `r17-notes.md` · Keys: `r05-notes.md` · **Status:** complete (fixed by R05)

---

## R17.1 Confirm

- [x] Dual-read **removed** — host middleware is **One-only** (`one."ApiCredentials"`; no `LhdnLookupSql` / `lhdn."DeveloperApiKeys"`)  
  — Confirmed 2026-08-09 after R05

## R17.2 Handoff

- [x] Tracked exclusively under Keys R01–R05  
  — Cutover code in R05; deploy gate + table drop remain Keys (R05.5 / R06)
- [x] No “fix” by moving Lhdn SQL into a module without cutover  
  — Obsolete path; One-only middleware already landed

## R17.3 Exit

- [x] Linked to R05; no separate SQL PR required


---

# R20 — TypeSpec dual DTO: products

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Files (verify):** `Commerce/.../ProductEndpoints.cs` local `CreateProductRequest` / `UpdateProductRequest`

---

## R20.1 Align types

- [x] Confirm generated `CreateProductRequestDto` / `UpdateProductRequestDto` exist after gen
- [x] Diff fields local vs generated; fix TypeSpec if gap
- [x] `task gen` if TSP changed *(N/A — shapes already match; no TSP edit)*

## R20.2 Switch endpoints

- [x] Bind Minimal API to generated types
- [x] Map to commands (decimal/double ACL if needed)
- [x] Delete local request records

## R20.3 Tests

- [x] Product completeness / endpoint tests green
- [x] Build Commerce + host

## R20.4 Exit

- [x] No local product create/update DTOs remain


---

# R21 — TypeSpec dual DTO: record refund

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**File (verify):** `Commerce/.../TransactionEndpoints.cs` `RecordRefundRequest`

---

## R21.1 Align + switch

- [x] Use generated `RecordRefundRequestDto` (or fix TSP)
- [x] Delete local record
- [x] `task gen` if needed *(N/A — shapes already match; no TSP edit)*

## R21.2 Tests

- [x] Refund-related tests green

## R21.3 Exit

- [x] Dual DTO gone for refund


---

# R22 — Broadcast preview/status contract honesty

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Impl has preview/status routes not fully in TypeSpec (or reverse)

---

## R22.1 Decision

- [x] **A:** Add routes + models to TypeSpec and use generated types in endpoints  
- [ ] **B:** Remove/internalize routes if not product  
- [x] Choice: **A** (OrgAdmin product surface; see `r22-notes.md`)

## R22.2 Implement A or B

- [x] TSP + gen **or** remove endpoints/docs
- [x] Endpoints use `Lazuar.ApiTypes` if A
- [x] Clients committed if policy requires *(regenerated; commit with PR)*

## R22.3 Tests

- [x] Broadcast tests green *(9/9 Broadcast*)*

## R22.4 Exit

- [x] No honesty gap for preview/status


---

# R23 — Billing signed PDF honesty

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`

---

## R23.1 Decision

- [x] Final signed PDF is public/admin API surface? yes/no: **no**
- [x] If yes: add to TypeSpec billing routes/models *(N/A)*
- [x] If no: allowlist as internal + document; ensure not advertised in product OpenAPI

## R23.2 Implement

- [x] TSP + gen **or** allowlist entry for R25 *(allowlist: `packages/api-spec/honesty-allowlist.yaml`)*
- [x] Endpoint uses generated types if exposed *(N/A — not product-exposed; 302 redirect)*

## R23.3 Exit

- [x] Decision implemented and documented *(see `r23-notes.md`)*


---

# R24 — Payments OpenAPI security schemes

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Payments docs package may lack auth schemes while routes require auth  
**Notes:** `../r24-notes.md`

---

## R24.1 Fix

- [x] Add `@useAuth` / security to payments docs TSP (mirror LHDN/one pattern)
- [x] Rebuild docs OpenAPI via gen
- [x] Spot-check `dist/payments` or docs output has securitySchemes

## R24.2 Exit

- [x] Authenticated payments routes documented with security


---

# R25 — OpenAPI ↔ Minimal API path honesty CI

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md` § CI gate  
**Depends on:** R20–R24 progress or allowlist ready  
**Notes:** [`r25-notes.md`](./r25-notes.md)

---

## R25.1 Design

- [x] Script/test: OpenAPI paths ⊆ Minimal API maps
- [x] Minimal ⊆ OpenAPI ∪ **allowlist** (unsubscribe, Resend webhook, gateway webhooks, etc.)
- [x] Allowlist file e.g. `packages/api-spec/honesty-allowlist.yaml` with reasons

## R25.2 Implement

- [x] Add tool under `scripts/` or test project
- [x] Wire into `.github/workflows/ci.yml` contracts job after `task gen`
- [x] Document how to update allowlist

## R25.3 Exit

- [x] CI fails on new silent drift
- [x] FW-6 CI item closed in FUTURE-WORK.md


---

# R30 — BuildingBlocks port hygiene

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md`, `../04-bb-email-messaging-move.md`, `009` ownership  
**Goal:** Interfaces in Application; Infrastructure implements

---

## R30.1 Inventory

- [ ] Ports defined only under BB.Infrastructure that modules use
- [ ] Concrete BB services injected where interface would suffice

## R30.2 Moves (thin only)

- [ ] Move storage/token/vault interfaces to Application if misplaced
- [ ] Update DI
- [ ] Architecture tests green

## R30.3 Docs

- [ ] Touch `009-building-blocks-ownership.md` if needed

## R30.4 Exit

- [ ] No product logic moved in this PR (LLM/email later)


---

# R31 — LLM factory/policies/title → Ops

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-A  
**Goal:** Remove OpenAI package from BuildingBlocks.Application

---

## R31.1 Move files

- [x] `IChatClientFactory`, policies, title generator, DI (`AddThinLlmFactory`) → Ops
- [x] Fold registration into `AddOpsModule`
- [x] Drop OpenAI from BB Application package refs if unused

## R31.2 Fix consumers

- [x] Ops orchestrator usings/DI
- [x] Remove dead `using BuildingBlocks.Application.Llm` elsewhere (e.g. Commerce)

## R31.3 Tests

- [x] Modules.Ops.Tests green
- [x] Architecture tests green
- [x] Host builds

## R31.4 Docs

- [x] Update 009 ownership map

## R31.5 Exit

- [x] BB has no LLM factory surface


---

# R32 — AgentTool + IAgentPromptProvider → Ops.Contracts

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md` PR-B  
**Depends on:** R31 recommended first  
**Arch rule:** Cross-module types via Contracts only

---

## R32.1 Move

- [x] `AgentToolAttribute`, `IAgentPromptProvider` → `Modules.Ops.Contracts`
- [x] Retarget all `[AgentTool]` sites (One/Billing/Payments/Lhdn/Ops — count: **10**)
- [x] Billing prompt provider implements Ops.Contracts interface

## R32.2 Cleanup BB

- [x] Remove agent types from BuildingBlocks.Application
- [x] Package refs updated (Ops.Contracts dependency-light; module Application/Infra ProjectRefs added)

## R32.3 Tests

- [x] Tool discovery still works (Ops tests — `ToolRegistryTests` + orchestrator suite)
- [x] Architecture: Contracts-only references

## R32.4 Exit

- [x] No AgentTool in BB


---

# R33 — Magic-link token service → Commerce

**Track:** BB · **Analysis:** `../04-bb-email-messaging-move.md`  
**Consumers:** Commerce portal validate; Communications dunning mint

---

## R33.1 Move

- [x] `IMagicLinkTokenService` + HMAC impl → Commerce (Contracts port if Communications needs it)
- [x] Preserve wire format + secret source (parity freeze)
- [x] Communications uses Contracts not BB

## R33.2 Tests

- [x] Portal magic link + dunning mint tests green

## R33.3 Exit

- [x] No magic-link product shapes in BB


---

# R34 — Email + IMessagingService → Messaging

**Track:** BB · **Analysis:** `../04-bb-email-messaging-move.md`  
**Respect:** 00.4 no WhatsApp product work

---

## R34.1 Move

- [x] `IEmailService`, Resend, ConsoleEmail, ResendOptions → Messaging
- [x] `EmailTemplateBuilder` brand HTML → Messaging
- [x] `IMessagingService` + Console → Messaging
- [x] Host/Messaging DI still resolves for DispatchMessage path
- [x] Communications BYOK stays in Communications (not moved into BB)

## R34.2 Parity

- [x] Org tag `org` behavior unchanged
- [x] BYOK rules unchanged

## R34.3 Tests

- [x] Messaging dispatch / notify tests
- [x] Host build

## R34.4 Docs

- [x] Update 009

## R34.5 Exit

- [x] BB has no Resend/brand email stack


---

# R35 — Metrics plugins + schema registration

**Track:** BB · **Analysis:** `../05-bb-metrics-plugins.md`  
**Also closes:** SQL L-06 handoff from R16  
**Notes:** `r35-notes.md` · **Status:** complete

---

## R35.1 Schema registration (M1)

- [x] `IOutboxSchemaRegistration` / `AddOutboxSchemaMetrics("one")` etc.
- [x] Remove hardcoded 9-schema constant array
- [x] Each module registers its schema in DI

## R35.2 Contributor + LHDN stuck (M2)

- [x] `IPlatformMetricsContributor` + contribution bag
- [x] Move `lhdn.TaxDocuments` stuck SQL to Lhdn contributor
- [x] Aggregator uses `IEnumerable<>` contributors
- [x] Preserve `/health/metrics` field names (`lhdn_stuck_count` etc.)

## R35.3 Hardening (M3 optional)

- [x] Fail-soft per contributor
- [ ] Dunning counter ownership cleanup if still in BB (M4) — **deferred**

## R35.4 Docs / tests

- [x] Metrics endpoint smoke (registration + boundary tests)
- [x] 009 + FUTURE-WORK updated

## R35.5 Exit

- [x] No product table SQL in BB collector
- [x] No hardcoded schema inventory


---

# R40 — LHDN webhooks product lock

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Goal:** Written decisions before any enqueue code  
**No dispatcher implementation in this phase**  
**Artifact:** [`webhook-convergence-decisions.md`](./webhook-convergence-decisions.md) · **Status:** complete  
**Checklist:** [`checklists/r40-webhooks-product-lock.md`](./checklists/r40-webhooks-product-lock.md)

---

## R40.1 Inventory refresh

- [x] LHDN events: `invoice.valid`, `invoice.invalid`; others (submitted/cancelled): **out of MVP**
- [x] One signing: Standard Webhooks `t=,v1=` (live `OutboundWebhookSignature`)
- [x] LHDN signing: body-only HMAC hex (live `WebhookSenderService`)
- [x] Prod/staging Lhdn webhook subscription row counts: **pending ops** (blocked like keys R04 — do not invent)

## R40.2 Locks (write answers)

- [x] Signing end-state: **One `t=,v1=` only**; dual-verify window **if prod LHDN subs exist** (hard cut if prod count 0)
- [x] Payload: **platform envelope wrapping LHDN `data`** (P-B); stable `data.*` field names
- [x] Routing: **migrate to `TenantWebhookEndpoints`**; migrated LHDN URLs get `EnabledEvents = [invoice.valid, invoice.invalid]`; **empty = all** (unchanged `AcceptsEvent`)
- [x] Breaking notice required? **Yes** (signing and/or top-level payload shape)

## R40.3 Design choice

- [x] Confirm **A1** (Lhdn publishes `OutboundWebhookRequestedIntegrationEvent`) — **chosen**; A2/A3 not chosen
- [x] Explicitly reject B (second stack)

## R40.4 Artifact

- [x] Written `plans/005-remaining/webhook-convergence-decisions.md` with answers  
  _(commit when wave commits; this phase is docs-only, no app code)_

## R40.5 Exit

- [x] R41 unblocked


---

# R41 — Registry backfill Lhdn → One webhook endpoints

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Depends on:** R40  
**Goal:** One `TenantWebhookEndpoint` rows cover LHDN customer URLs

---

## R41.1 Migrator

- [x] Map `lhdn.WebhookSubscriptions` (active) → One endpoints (`Jobs/WebhookSubscriptionMigration/`)
- [x] Set `EnabledEvents` to `invoice.valid` / `invoice.invalid` (per R40)
- [x] Idempotent on Org+Url; preserve secrets/signing material per R40 dual-verify design
- [x] Staging then prod runbook → `r41-webhooks-registry-backfill-runbook.md`
- [x] Dual-write of register API skipped (optional)

## R41.2 Validation

- [ ] Row counts match expectations (ops execute)
- [ ] No silent zero endpoints for orgs that had Lhdn subs (ops execute)
- [x] Unit tests with fake store

## R41.3 Exit

- [x] Migrator implemented; fire-and-forget still may run until R42/R43 cutover plan says stop
- [ ] Staging/prod execute when counts warrant


---

# R42 — Enqueue LHDN lifecycle into One dispatcher (A1)

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Depends on:** R40, R41 recommended  
**Do not:** Dual-fire fire-and-forget + One in production without explicit dual-delivery decision

---

## R42.1 Implement enqueue

- [ ] On LHDN validated/invalid (etc.), publish `OutboundWebhookRequestedIntegrationEvent` (or chosen A1 shape)
- [ ] Payload per R40 (envelope vs raw)
- [ ] Org/endpoint resolution matches One dispatcher expectations
- [ ] Correlation ids

## R42.2 Optional dual-sign

- [ ] If R40 requires dual-verify: implement dual headers / dual body rules
- [ ] Golden signature tests

## R42.3 Tests

- [ ] Event → outbox row(s)
- [ ] Fan-out filters by EnabledEvents
- [ ] Dispatcher still delivers One platform events unchanged

## R42.4 Exit

- [ ] Staging: LHDN event produces durable outbox delivery


---

# R43 — Retire LHDN fire-and-forget sender

**Track:** Webhooks · **Depends on:** R42 verified in staging (and prod cutover plan)  
**Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`

---

## R43.1 Stop dual delivery

- [ ] Remove calls to `WebhookSenderService` / `DispatchExternalWebhookCommand` for migrated events
- [ ] Ensure only One path remains

## R43.2 Cleanup

- [ ] Delete or gut unused service
- [ ] Metrics: re-tag or remove pure Lhdn failure counter if obsolete
- [ ] Optional later: drop Lhdn webhook subscription table after façade period

## R43.3 Docs

- [ ] Lhdn README freeze section removed; One path documented
- [ ] Integrator changelog if signing/payload broke
- [ ] FUTURE-WORK FW-2 done

## R43.4 Exit

- [ ] No customer LHDN webhook uses fire-and-forget


---

# R50 — TestSupport migration batch

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Goal:** Migrate N ModuleTests off copy-paste fixtures

---

## R50.1 Select batch

- [ ] Target N = 4–6 (recommended first batch)
- [ ] Prefer: One webhook tests, Billing event-handler fixtures, one Commerce fixture
- [ ] Skip: WebApplicationFactory auth suites, mediator-heavy LHDN/provision for later

## R50.2 Migrate

- [ ] Use `Lazuar.TestSupport` FakeExecutionContext + InMemory helpers
- [ ] Delete local NoopMediator duplicates where possible
- [ ] All migrated tests green

## R50.3 Exit

- [ ] Batch merged; list remaining high-copy suites for next batch


---

# R51 — LhdnGatewayAdapter partials

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Goal:** Split ~384 LOC adapter without behavior change

---

## R51.1 Split

- [ ] Partials: Token, Submit, Status, TIN, Cancel (+ shared rate limit if needed)
- [ ] Keep public type name + port interface stable

## R51.2 Tests

- [ ] LHDN gateway / module tests green

## R51.3 Exit

- [ ] Navigable files; zero behavior change


---

# R52 — LlmOrchestratorService stream partial

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Note:** Preserve BinaryData / streaming fix history

---

## R52.1 Split

- [ ] Move stream loop to `.Stream.cs` partial (or equivalent)
- [ ] Keep non-stream path clear
- [ ] No streaming behavior regression

## R52.2 Tests

- [ ] Ops LLM tests green

## R52.3 Exit

- [ ] Main file thinner; stream isolated


---

# R53 — GatewayCommon + outbox DI pilot

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`

---

## R53.1 GatewayCommon

- [ ] Extract shared ExtractName / minor unit helpers used by payment adapters
- [ ] **No** mega abstract gateway base class
- [ ] Adapters call helpers only

## R53.2 Outbox/inbox DI pilot

- [ ] Design `AddModuleOutboxInbox<TDbContext>` (Option A: keep thin job subclasses)
- [ ] Optional `ApplyOutboxInbox` EF helper
- [ ] Pilot on **CRM** (or agreed small module)
- [ ] Zero EF migrations required
- [ ] Arch tests + Lhdn registration tests still green if touched later

## R53.3 Optional ProblemDetails

- [ ] Expand stable codes on LHDN documents or One provision when editing those endpoints

## R53.4 Exit

- [ ] At least GatewayCommon **or** outbox pilot landed


---

# R60 — Module extract / merge gate (default SKIP)

**Track:** Extract · **Analysis:** `../07-module-extract-and-merge.md`  
**Default outcome:** N/A — do not implement

---

## R60.0 Gate (all required to proceed)

- [ ] Product trigger written (credits / webhooks product / multi-channel funded)
- [ ] `decisions.md` reopened and updated
- [ ] Design note (schema, events, dual-write)
- [ ] Product sign-off

If any unchecked → mark **SKIP** and stop.

---

## R60.1 If Credits extract triggered

- [ ] Follow analysis § Credits full steps (module, consumers, cutover)

## R60.2 If Webhooks extract triggered

- [ ] Follow analysis § Webhooks extract (after FW-1/FW-2 preferred)

## R60.3 If Messaging→Communications merge triggered

- [ ] Follow analysis § merge steps

## R60.4 Exit

- [ ] SKIP documented **or** extract complete with Contracts-only boundaries


---

# R99 — Definition of done (remaining-work program)

**Goal:** Close the 005 remaining program honestly  
**Analysis:** `../10-program-sequencing-and-risks.md`

---

## R99.1 Per selected track

### Keys (if selected)

- [ ] R05 One-only in prod (or waived)
- [ ] R06 done or dated

### SQL (if selected)

- [ ] R11–R15 P0/P1 fixed
- [ ] R16/R17 handoffs resolved via R35/R05

### TypeSpec (if selected)

- [ ] R20–R24 targets for wave done
- [x] R25 optional CI on or ticketed

### BB (if selected)

- [ ] R30–R35 planned moves done or ticketed with owners

### Webhooks (if selected)

- [ ] R43 complete **or** product deferred with new date

### Polish

- [ ] Opportunistic items done or explicitly skipped

### Extract

- [ ] R60 SKIP or complete

## R99.2 Docs

- [ ] `FUTURE-WORK.md` statuses updated
- [ ] No dual-path lies in One/Lhdn READMEs for closed tracks
- [ ] Residuals are normal tickets, not open “mega remaining program”

## R99.3 Stop

- [ ] Declare wave closed

