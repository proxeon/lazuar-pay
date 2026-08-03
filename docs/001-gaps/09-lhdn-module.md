<!-- Source subagent: 019fc650-3512-7283-86ea-567ca8ab1f9e -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# LHDN Module Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/`, plus `packages/lhdn-sdk-dotnet`, `packages/lhdn-sdk-ts`, `packages/api-spec/modules/lhdn/`, `docs/lhdn/`, `scripts/lhdn_sandbox/`, related auth middleware, Billing event handlers, ops-page invoicing UI, and Architecture ADRs 009–011 / 020–021.

**Maturity snapshot:** Strong **V1.0 unsigned UBL** pipeline (template → XSD preflight → async submit → poll) with productized **developer API keys**, **outbound webhooks**, **idempotency**, and dual **Kiota SDKs**. Material gaps in **V1.1 signatures**, **contract/endpoint parity**, **billing status vocabulary**, **supplier master data**, **security of secrets**, **worker multi-instance safety**, and **tests**.

---

## Module Inventory

### Modular layout (`Modules/Lhdn/`)

| Layer | Path | Role |
|--------|------|------|
| **Application** | `Application/` | CQRS commands/queries, ports, service interfaces |
| **Domain** | `Domain/` | Aggregates, entities, cancel-window rule |
| **Contracts** | `Contracts/Events/` | Integration events for Billing + API host |
| **Infrastructure** | `Infrastructure/` | EF Core (`schema: lhdn`), endpoints, gateways, strategies, templates, XSD schemas, workers, event handlers |

### Project files

- `Modules.Lhdn.Application.csproj`
- `Modules.Lhdn.Domain.csproj`
- `Modules.Lhdn.Contracts.csproj`
- `Modules.Lhdn.Infrastructure.csproj`
- Module README: `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/README.md`

### Application surface

**Commands**

| Command | Purpose |
|---------|---------|
| `SubmitTaxDocumentCommand` | Credits check → strategy XML → XSD validate → hash → persist `TaxDocument` + `IdempotencyLog` → `DeductTenantCreditCommand` |
| `CancelTaxDocumentCommand` | Domain cancel rule → MyInvois cancel → `LhdnDocumentCancelledIntegrationEvent` |
| `GenerateApiKeyCommand` / `RevokeApiKeyCommand` | Developer API keys (`sk_live_` / `sk_test_`) |
| `RegisterWebhookCommand` / `DeleteWebhookCommand` | Outbound customer webhooks |
| `UpdateLhdnCertificateCommand` | Store P12 + passphrase ciphertext |
| `DispatchExternalWebhookCommand` | Fan-out HMAC-signed JSON to subscriptions |

**Queries**

| Query | Purpose |
|-------|---------|
| `GetLhdnDocumentStatusQuery` | Status DTO + QR link (via recent submissions) |
| `ListWebhooksQuery` | Active webhooks |
| `ListLhdnSubmissionsAgentQuery` | Ops agent tool (`[AgentTool]`) |

**Ports**

- `ILhdnRepository`, `ILhdnGatewayAdapter`, `ILhdnQueryService`

**Application services (interfaces)**

- `IDocumentStrategyFactory`, `IUblDocumentStrategy`, `ITemplateRendererService`, `IUblValidatorService`
- `ICertificateVaultService`, `IWebhookSenderService`, `ITaxpayerValidationService`, `ILhdnLinkService`

### Infrastructure surface

- **DB:** `LhdnDbContext` → PostgreSQL schema `lhdn` (single initial migration `20260627124829_InitialLhdnSchema`)
- **HTTP:** `Endpoints.MapLhdnEndpoints` under `/lhdn` (mapped from host as `/api/v1/lhdn`)
- **Gateway:** `LhdnGatewayAdapter` → MyInvois preprod/prod base URL
- **Workers:** `LhdnSubmissionJob`, `LhdnStatusPollingJob`, `LhdnReferenceDataSeederJob`
- **Inbound integration event handlers:**
  - `InvoiceIssuedIntegrationEventHandler` (Billing)
  - `ConsolidatedInvoiceIssuedIntegrationEventHandler` (Billing)
  - `GatewayRefundCompletedIntegrationEventHandler` (Payments → cancel &lt;72h else credit note)
- **Templates:** 5 Scriban XML files
- **Schemas:** UBL Invoice + CreditNote roots + full `common/` OASIS set (embedded)

### Host wiring

- `Program.cs`: `AddLhdnModule`, `MapLhdnEndpoints`, `ApiKeyAuthenticationMiddleware`, `ApiKeyRevokedIntegrationEvent` subscription
- Auth policy `OrgAdmin` allows `SUPER_ADMIN`, `ADMIN`, **`API_CLIENT`** (API keys)

### External packages / docs / scripts

| Area | Path |
|------|------|
| TypeSpec | `packages/api-spec/modules/lhdn/{models,routes}.tsp`, `docs-lhdn.tsp` |
| OpenAPI dist | `packages/api-spec/dist/lhdn/openapi.yaml` |
| .NET SDK | `packages/lhdn-sdk-dotnet/` (Kiota, v0.1.0) |
| TS SDK | `packages/lhdn-sdk-ts/` (`@lazuar/lhdn-sdk` v0.1.0) |
| Developers portal | `apps/developers-page/app/lhdn/` |
| Sandbox scripts | `scripts/lhdn_sandbox/` |
| ADRs / notes | `docs/lhdn/*`, `docs/architecture-decision-log/010-…`, `011-sdk-…`, `apps/lazuar-api/docs/lhdn/002-ubl-xsd-validator.md` |
| Sample XML corpus | `docs/xml/{invoice,credit,debit,refund,self-billed-*}/` |

---

## Domain Aggregates

### 1. `TaxDocument` (`Domain/Aggregates/TaxDocument.cs`)

| Field | Notes |
|-------|--------|
| `Id` | UUIDv7 |
| `OrganizationId` | `IMustHaveTenant` |
| `InternalReferenceId` | Client/business ref (codeNumber) |
| `DocumentHash` | SHA-256 hex of normalized UTF-8 XML |
| `RawXmlContent` | Stored XML (LF-normalized) |
| `LhdnUuid` / `SubmissionUid` / `LongId` | MyInvois identifiers |
| `ValidationStatus` | String FSM: `PENDING` → `SUBMITTED` → `VALID` / `INVALID` / `FAILED` / `CANCELLED` |
| `IsTestMode` | Sandbox key submissions |
| `NextPollAt` / `PollAttempts` | Exponential backoff `3^min(n,10)` seconds |

**Behaviors:** `MarkAsSubmitted`, `DelayPendingSubmission`, `ScheduleNextPoll`, `MarkAsValid`, `MarkAsInvalid`, `MarkAsFailed`, `Cancel` (+ `CancelWindowMustBeValidRule` = 72h).

**Gaps**

- Status is free-form `string`, not an enum / value object.
- No unique index on `(OrganizationId, InternalReferenceId)` → duplicate internal IDs possible.
- No document type / version / buyer TIN columns → hard to filter without parsing XML.
- Cancel is optimistic in domain before gateway success is confirmed (handler calls `doc.Cancel()` then gateway; if gateway fails, exception aborts save — OK — but success path publishes event then saves; cancel does not dispatch customer webhooks).

### 2. `LhdnTenantConfig`

| Field | Notes |
|-------|--------|
| Supplier TIN / IdType / IdValue / MSIC | Legal identity for MyInvois |
| `Environment` | `SANDBOX` default |
| `IntermediaryMode` | Adds `onbehalfof` header |
| `MyInvoisClientId` / `MyInvoisClientSecret` | **plaintext** OAuth client credentials |
| `EncryptedPfxBase64` / `PfxPasswordCiphertext` | Certificate vault fields |
| Seeded genesis row | Hardcoded org in `OnModelCreating` |

**Gaps**

- No HTTP API to create/update tenant LHDN profile or MyInvois credentials (only cert PUT; credentials only via SQL/seed/scripts).
- `UpdateApiCredentials` exists on aggregate but is unused by application commands.
- Tenant **legal name, address, contact** are **not** stored; mapper hardcodes `"Lazuar Tenant"` and templates hardcode supplier postal address (`Lot 66 / Bangunan Merdeka` in `StandardInvoice.xml`).
- No validation of TIN format / env consistency with gateway base URL.

### 3. `DeveloperApiKey` — **platform pattern candidate**

| Field | Notes |
|-------|--------|
| `Name`, `Prefix` (`sk_live_` / `sk_test_`), `KeyHash`, `IsActive`, `CreatedAt` | Hash-only storage of secret |

**Gaps**

- No `LastUsedAt`, scopes, expiry, rate-limit tier, or key metadata for audit.
- Test mode inferred from **prefix at auth time**, not a DB flag on the key (works, but list UI cannot show mode without prefix).
- No list-keys command/endpoint in module (TypeSpec declares `GET /api-keys`).

### 4. `WebhookSubscription` — **platform pattern candidate**

| Field | Notes |
|-------|--------|
| `Url`, `Secret`, `IsActive`, `CreatedAt` | Soft-delete via `Deactivate` |

**Gaps**

- `RegisterWebhookRequestDto.events` is **ignored**; all active webhooks get every dispatch.
- No delivery log, retry, backoff, or dead-letter.
- No event filter column despite DTO/API surface advertising `events`.

### 5. Supporting entities

| Entity | Purpose |
|--------|---------|
| `IdempotencyLog` | `(OrganizationId, IdempotencyKey)` unique; stores created doc id |
| `TinValidateCache` | HMAC-hashed id value; 30d valid / 7d invalid TTL |
| `MsicCode`, `CountryCode`, `TaxType` | Reference data for UI/dropdowns |
| Outbox / Inbox | Per-module messaging tables |

### Domain rules

- Only explicit rule: `CancelWindowMustBeValidRule` (72 hours).
- No domain rules for credit balances, hash integrity, or status transitions beyond method guards.

---

## Submission Pipeline & Workers

### Happy path (SDK / dashboard)

```
POST /api/v1/lhdn/documents
  + Idempotency-Key
  → SubmitTaxDocumentCommand
      → credit check (live only)
      → strategy.Generate → LF normalize
      → UblValidatorService.Validate
      → SHA-256 hex hash
      → TaxDocument(PENDING) + IdempotencyLog
      → DeductTenantCreditCommand (live only)
  → 200 { status: "accepted_for_processing" }

LhdnSubmissionJob (5s loop, Take 50 PENDING)
  → OAuth token (cached 55m)
  → POST /api/v1.0/documentsubmissions { format:XML, documentHash, codeNumber, document:base64 }
  → MarkAsSubmitted + LhdnDocumentSubmittedIntegrationEvent

LhdnStatusPollingJob (10s loop, Take 50 SUBMITTED)
  → GET /documentsubmissions/{uid}
  → VALID → MarkAsValid + LhdnDocumentValidatedIntegrationEvent + DispatchExternalWebhook
  → INVALID → MarkAsInvalid + outbound webhook
  → else ScheduleNextPoll / Retry-After
```

### Internal event-driven intake

| Source event | Handler | Quality |
|--------------|---------|---------|
| `InvoiceIssuedIntegrationEvent` | Builds stub buyer (`"Resolved via CRM"`, fake TIN) | **Placeholder — not production-ready** |
| `ConsolidatedInvoiceIssuedIntegrationEvent` | Correct B2C general public TIN `EI00000000010` | Better, still no CRM resolve |
| `GatewayRefundCompletedIntegrationEvent` | &lt;72h cancel else CN with stub buyer | Bypass of `SubmitTaxDocumentCommand` (no XSD validate path parity, no idempotency, no credit deduct on that path) |

### Workers

| Job | Interval | Batch | Notes |
|-----|----------|-------|-------|
| `LhdnSubmissionJob` | 5s | 50 | Per-doc save in `finally` |
| `LhdnStatusPollingJob` | 10s | 50 | Publishes validated event with **wrong positional args** (see Gaps) |
| `LhdnReferenceDataSeederJob` | Startup | once | Path `../../../../lhdn_docs/codes` relative to content root — **fragile in Docker** |

**Worker gaps**

- No `FOR UPDATE SKIP LOCKED` / lease / row versioning → **multi-instance double-submit risk**.
- Failures on submit often `MarkAsFailed` with no automatic dead-letter retry beyond rate-limit delay.
- Submission job publishes `LhdnDocumentSubmittedIntegrationEvent` which **double-charges** Billing (see below).
- Status poller does not publish invalidation integration events for Billing ledger on INVALID.
- Cancel path does not enqueue outbound customer webhooks.

### Credit / billing interaction (**critical gap**)

1. **On accept:** `SubmitTaxDocumentCommand` → `DeductTenantCreditCommand` with configurable `CreditAction.LhdnSubmit` cost + idempotency key `lhdn:{key}`.
2. **On MyInvois accept:** `LhdnSubmissionJob` → `LhdnDocumentSubmittedIntegrationEvent` → Billing handler `wallet.Deduct(1, ...)` **again** (hardcoded 1, no idempotency).

This is a **double-deduction** design defect for live keys.

Billing `LhdnDocumentValidatedIntegrationEventHandler` expects status **`VALIDATED`** and uses `QrLink`, but poller publishes:

```csharp
new LhdnDocumentValidatedIntegrationEvent(
    orgId, internalId, uuid, longId!, "VALID");
// ctor: (OrgId, InternalRef, LhdnUuid, Status, QrLink?)
// → Status = longId, QrLink = "VALID"
```

So ledger LHDN status updates and PDF generation (`GenerateAndStoreDocumentCommand` when status == `"VALIDATED"`) are **effectively broken**.

Ops UI cancelability checks `lhdn_validation_status === "VALIDATED"` while LHDN domain uses `"VALID"` — further vocabulary drift.

---

## XML Templating & Validation

### Strategy matrix

| Doc type | Factory key | Template | Strategy |
|----------|-------------|----------|----------|
| `01` B2B | `B2BStandardInvoice` | `StandardInvoice.xml` | `StandardInvoiceStrategy` |
| `01` B2C (empty TIN or `EI00000000010`) | `B2CConsolidatedInvoice` | `ConsolidatedInvoice.xml` | `ConsolidatedInvoiceStrategy` |
| `02`/`03`/`04` | `CreditNote` | `CreditNote.xml` | `CreditNoteStrategy` (dynamic `doc_type_code`) |
| `11` | `SelfBilledInvoice` | `SelfBilledInvoice.xml` | `SelfBilledInvoiceStrategy` |
| `12`/`13`/`14` | `SelfBilledCredit` | `SelfBilledCreditNote.xml` | `SelfBilledCreditNoteStrategy` |

`ViewModelMapper` implements self-billed **entity swap** (tenant → buyer nodes, request party → supplier) and consolidates tax subtotals by tax type; forces classification `004` for true B2C general public.

### Templating stack (ADR 010 aligned)

- **Scriban** singleton with template compile cache.
- Filters: `format_amount` (2 dp), `xml_escape` (`SecurityElement.Escape`).
- `StrictVariables = true`.
- Line endings normalized to `\n` before hash (cryptographic alignment for MyInvois `documentHash`).

### Validation

- **Phase 1 XSD:** `UblValidatorService` singleton, embedded UBL Invoice + CreditNote roots, custom `EmbeddedResourceXmlResolver`, strips xmldsig DTD for compile safety. Documented in `apps/lazuar-api/docs/lhdn/002-ubl-xsd-validator.md`.
- **Phase 2 Schematron:** **Not implemented** (ADR 010 requires it for math/business rules).
- If schema set fails to load (`Count == 0`), validation **no-ops** silently.

### V1.1 signatures

- Templates wrap `<!-- SIGNATURE_PLACEHOLDER -->` when `document_version == "1.1"`.
- **No signing code** uses `ICertificateVaultService.GetDecryptedCertificate`.
- Docs (`docs/lhdn/000-xml-vs-json.md`) record brittle LHDN XML-DSig / “Root element is missing” issues; strategy still open (fix XAdES vs JSON path).

### Template / data quality gaps

1. Supplier address **hardcoded** in Standard (and similar) templates rather than bound to tenant profile.
2. Tenant display name hardcoded `"Lazuar Tenant"`; phone/email placeholders.
3. Buyer address `line2`/`line3` exist on DTO but are unused in templates.
4. No currency other than MYR.
5. No payment means / digital signature / invoice line unit codes beyond defaults.
6. Credit-note original UUID / billing reference fields: partial (view model has `OriginalLhdnUuid`; template usage varies by file).
7. No golden-master unit tests active (`UblStrategyTests.cs` fully commented).
8. Architecture golden master JSON exists (`tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json`) but strategy tests not wired.

---

## Gateway Integration with MyInvois

### `LhdnGatewayAdapter`

| Operation | Endpoint | Client rate limit / min |
|-----------|----------|-------------------------|
| Token | `POST /connect/token` (client_credentials, scope `InvoicingAPI`) | 12 |
| Submit | `POST /api/v1.0/documentsubmissions` | 100 |
| Status | `GET /api/v1.0/documentsubmissions/{uid}` | 300 |
| Details (invalid) | `GET /api/v1.0/documents/{uuid}/details` | (shares poll client) |
| TIN validate | `GET /api/v1.0/taxpayer/validate/{tin}` | 60 |
| Cancel | `PUT /api/v1.0/documents/state/{uuid}/state` | 12 |

**Strengths**

- Intermediary `onbehalfof` header.
- 429 → `Retry-After` / `x-rate-limit-reset`.
- 404 on poll treated as soft-pending (sandbox async lag).
- Rejected document detail parsing for submit + invalid detail aggregation.
- Token cache key per organization (55 minutes).

**Gaps**

- Rate limiters are **in-process** `ConcurrentDictionary` — not cluster-safe.
- Base URL from `Lhdn:BaseUrl` only; no automatic PROD vs SANDBOX switch from `LhdnTenantConfig.Environment`.
- No reject (buyer rejection) API surface.
- No document raw download / QR regeneration endpoints.
- No submission batch of multiple docs in one call (always single-document array).
- Secrets (client secret) logged only on failure body — still risk of secret leakage via exception messages upstream.
- HttpClient is factory default; no named client with Polly policies beyond Kiota-side (API client) — gateway itself has no circuit breaker.
- TIN validate service implemented but **not exposed** on `Endpoints`.

---

## Webhooks (inbound LHDN status + outbound customer)

### Inbound from LHDN

**None.** Status is exclusively **polling** (`LhdnStatusPollingJob`). No MyInvois notification webhook receiver.

### Outbound to customers (developers)

| Piece | Behavior |
|-------|----------|
| Register/list/delete | Implemented (list hardcodes events `invoice.validated`, `invoice.rejected`) |
| Dispatch | On VALID / INVALID from poller |
| Payload | `{ event: "invoice.{status}", data: { internal_id, lhdn_uuid, status, qr_link, error_message, timestamp } }` snake_case |
| Auth | `X-Lazuar-Signature: HMAC-SHA256(secret, body)` hex |
| Failure | Log only; no retry |

**Gaps**

- No delivery attempts table / exponential retry / DLQ.
- No signature versioning / timestamp / idempotency key for consumers.
- Events list not persisted or filtered.
- CANCELLED / FAILED / SUBMITTED not dispatched.
- Does not follow Payments ADR 009 patterns (different domain; outbound is Lazuar-originated).

**Reusable platform pattern**

```
WebhookSubscription aggregate
  + secret at registration
  + IWebhookSenderService (HMAC header)
  + Dispatch*Command fan-out
  + CRUD under product /api/v1/{module}/webhooks
```

Recommend extracting to BuildingBlocks / Platform once a second product needs it (Billing webhooks, Payments-to-merchant, etc.).

---

## Developer API Keys (as pattern for platform)

### Implemented end-to-end

| Layer | Behavior |
|-------|----------|
| Domain | `DeveloperApiKey` hash-only secret |
| Command | Generate (`sk_live_`/`sk_test_` + 40-char token), Revoke |
| Middleware | `ApiKeyAuthenticationMiddleware` matches `Bearer sk_*`, hashes, queries `lhdn."DeveloperApiKeys"`, 5m cache, sets `TenantId` + `IsTestMode` + role `API_CLIENT` |
| Revocation | `ApiKeyRevokedIntegrationEvent` → cache eviction (`ApiKey_{hash}`) |
| Credits | `IsTestMode` skips credit check/deduct on submit |
| Isolation | `TaxDocument.IsTestMode` for ledger filtering |

### Contract vs code

| TypeSpec | Endpoints.cs |
|----------|--------------|
| `POST /api-keys` | ✅ |
| `GET /api-keys` | ❌ missing |
| `DELETE /api-keys/{id}` | ✅ |
| `POST /taxpayer/validate` | ❌ missing (service exists) |

### Gaps for platform promotion

1. Keys live in **LHDN schema** and middleware hardcodes SQL to `lhdn."DeveloperApiKeys"` — not product-agnostic.
2. No scopes (`lhdn:submit`, `lhdn:read`) — any API key with OrgAdmin-equivalent role can hit all LHDN admin-ish routes under same policy.
3. Generate endpoint returns only `plain_key` — not `id`/`prefix`/`created_at` (harder for UI).
4. No rotation / grace period.
5. Auth middleware mixes infrastructure (Dapper + Lhdn connection factory) into API host.

### Recommended platform extraction

```
Platform.Integrations or BuildingBlocks
  DeveloperApiKey (org-scoped, product_code, prefix, hash, scopes[], is_test)
  ApiKeyAuthenticationMiddleware (product-agnostic table / service)
  ApiKeyRevokedIntegrationEvent + cache eviction
  IdempotencyLog (optional shared)
  WebhookSubscription (product_code, events[])
```

LHDN becomes first consumer; Payments BYOK and future GSTN/Coretax reuse the same credential model.

---

## SDK State

### TypeSpec → OpenAPI → Kiota

- Routes: `/api/v1/lhdn/*` (documents, cancel, webhooks, certificate, api-keys, taxpayer validate).
- Product docs: `docs-lhdn.tsp` + developers-page Scalar at `/lhdn`.

### `@lazuar/lhdn-sdk` (TS) — `0.1.0`

- Factory: `initLhdnClient({ apiKey, baseUrl })`
- Auth: Kiota `ApiKeyAuthenticationProvider` on `Authorization` header — **does not automatically prefix `Bearer `**; depends on whether callers pass `Bearer sk_...` or raw key. Middleware requires `Bearer sk_live_` / `Bearer sk_test_`.
- No automatic `Idempotency-Key` injection (unlike .NET).

### `Lazuar.Lhdn.Sdk` (.NET) — `0.1.0`

- `LhdnClientFactory.Create(apiKey, baseUrl)`
- `IdempotencyHandler` injects random GUID on POST if missing (good for retries; bad if client wants semantic keys).
- Full Kiota tree for api-keys, documents, cancel, webhooks, certificate, taxpayer validate.

### SDK gaps

- Both at **0.1.0**; publishing runbook exists (ADR 011) but maturity is pre-release.
- Generated clients include operations **not implemented** on server (`listApiKeys`, `validateTaxpayerTin`) → runtime 404s.
- No high-level helpers (submit-and-poll, webhook signature verify utilities).
- No webhook verification sample in SDK packages.
- TS package uses preview Kiota deps (`1.0.0-preview.20`) while .NET uses `2.0.0`.

---

## Testing & Sandbox Scripts

### Automated tests

| Suite | Path | Status |
|-------|------|--------|
| Rate / submit unit | `tests/Lazuar.ModuleTests/Lhdn/LhdnRateLimitingTests.cs` | Active — basic happy-path save only (name oversells “rate limiting”) |
| Sandbox E2E | `LhdnSandboxE2ETests.cs` | **`[Ignore]`** — needs env credentials |
| Strategy golden masters | `Strategies/UblStrategyTests.cs` | **Fully commented out** |
| Architecture | includes `lhdn-golden-master.json` | Boundary module list only for Lhdn |
| Integration tests | No LHDN-specific DB tests | — |

### Shell sandbox (`scripts/lhdn_sandbox/`)

| Script | Coverage |
|--------|----------|
| `00_provision.sh` | Creates org + membership + `TenantConfigs` with **hardcoded MyInvois client id/secret/TIN** |
| `01_test_b2b.sh` | Standard invoice poll until VALID |
| `02_test_credit_note.sh` | CN |
| `03_test_b2c.sh` | Consolidated |
| `04_upload_dummy_cert.sh` | Dummy P12 upload |
| `05_test_b2b_v1_1.sh` | v1.1 (known to fail without signatures) |
| `06_test_cancel.sh` | Cancel |
| `07_test_self_billed.sh` | Type 11 |
| `run_all.sh` | 00→01→02→03→06→07 (**skips** 04/05) |

**Gaps**

- **Secrets committed** in `00_provision.sh` (client secret + NRIC) — rotate and move to env.
- Cookie auth + `X-Tenant-Slug`; does not exercise API-key auth path or idempotency headers.
- No webhook receiver mock test.
- No multi-tenant isolation / credit exhaustion / concurrent idempotency tests.
- Reference data path and seed not covered.

---

## Gaps & Recommendations

### P0 — Correctness / money / compliance risk

| # | Gap | Recommendation |
|---|-----|----------------|
| 1 | **Double credit deduction** (command + submitted event handler) | Pick one owner: prefer charge on **successful MyInvois submit** *or* on accept with hold/settle. Remove the other. Align amount (`LhdnSubmit` cost vs hard-coded `1`). |
| 2 | **`LhdnDocumentValidatedIntegrationEvent` argument order / status** | Fix publish to `(…, uuid, "VALID", qrLink)`. Align Billing + UI on one vocabulary (`VALID` vs `VALIDATED`). |
| 3 | **Supplier legal profile incomplete** | Persist legal name + full address on `LhdnTenantConfig`; bind templates; stop hardcoded Bangunan Merdeka / “Lazuar Tenant”. |
| 4 | **MyInvois client secret + PFX at rest** | Encrypt secrets (same vault as PFX password); encrypt PFX bytes (currently stored as plain base64 despite field name). Prefer KMS/HSM long-term. |
| 5 | **Internal invoice/refund handlers use stub buyers** | Resolve CRM/Billing party data before submit; do not ship stubs. |

### P1 — Product API completeness

| # | Gap | Recommendation |
|---|-----|----------------|
| 6 | TypeSpec ops without endpoints: `listApiKeys`, `validateTaxpayerTin` | Implement or remove from public OpenAPI. |
| 7 | No tenant LHDN config CRUD API | `PUT /workspaces/{id}/lhdn-config` (TIN, BRN, MSIC, env, client credentials). |
| 8 | Get document by internal id scans last 100 | Direct repository query by `(org, internalId)`; return full DTO including `is_test_mode`, `long_id`, `validated_at`. |
| 9 | Webhook events ignored; no delivery reliability | Store `events[]`; filter dispatch; add delivery log + retries. |
| 10 | No list documents API for product UI | Paginated `GET /documents?status=&is_test_mode=`. |

### P2 — Compliance depth

| # | Gap | Recommendation |
|---|-----|----------------|
| 11 | XAdES v1.1 unsigned | Implement C14N + sign pipeline per ADR 010; or officially document JSON-only v1.1 if LHDN XML-DSig remains brittle. |
| 12 | No Schematron | Add Phase-2 business-rule validation (totals, TIN rules, classification). |
| 13 | Certificate vault unused for signing | Wire vault into submit path when version=1.1. |
| 14 | No reject document / recent documents sync | Map remaining MyInvois APIs as needed for full compliance ops. |

### P3 — Platform & ops hardening

| # | Gap | Recommendation |
|---|-----|----------------|
| 15 | Workers not multi-instance safe | Claim rows with `SKIP LOCKED` + processing lease. |
| 16 | In-process rate limiters | Redis / distributed token buckets keyed by client id. |
| 17 | API keys schema-coupled to LHDN | Extract platform IntegrationCredentials module. |
| 18 | Reference data seeder path | Config absolute path + embed JSON or seed from migration. |
| 19 | Sandbox secrets in git | Env-only credentials; scrub history if keys are real. |
| 20 | Tests largely disabled | Restore golden masters; add contract tests for OpenAPI vs Endpoints; webhook HMAC unit tests. |
| 21 | Refund handler bypasses submit pipeline | Always go through `SubmitTaxDocumentCommand` for CN generation. |
| 22 | Unique internal reference | Unique index + clear 409 on conflict. |
| 23 | Status strings / UI drift | Shared constants package or OpenAPI enums for statuses. |
| 24 | Outbound webhook for cancel | Dispatch `invoice.cancelled`. |
| 25 | SDK auth Bearer / idempotency parity | Align TS with .NET; document required header format. |

### Reusable patterns for platform-wide integration APIs

Promote these as **Platform Integration Kit** (first proven by LHDN):

1. **Developer API Keys** — hash storage, live/test prefixes, middleware auth, revocation events, test-mode billing exemption.
2. **Idempotency-Key** — per-tenant unique log storing resource id; concurrent unique-index recovery.
3. **Outbound Webhooks** — subscription aggregate + HMAC signature header + async dispatch command.
4. **Gateway Adapter port** — token cache, per-client rate limits, Retry-After mapping, intermediary headers.
5. **Certificate / secret vault** — AES master key from config (upgrade path to KMS).
6. **Async job pair** — accept → pending worker → status poller with exponential backoff.
7. **TypeSpec product slice** + dual Kiota SDKs + developers portal OpenAPI.
8. **XML templating** (ADR 010) for any government/B2B XML authority (reusable beyond LHDN).
9. **Module Outbox/Inbox** + keyed `IEventBus` per module.
10. **Agent tools** on query side for ops copilots.

---

## File-by-File Notes

### Domain

| File | Notes |
|------|-------|
| `Domain/Aggregates/TaxDocument.cs` | Solid FSM methods; string statuses; cancel rule enforced; test mode flag good |
| `Domain/Aggregates/LhdnTenantConfig.cs` | Credentials + cert fields; missing legal address/name; secrets plaintext |
| `Domain/Aggregates/DeveloperApiKey.cs` | Minimal; good hash-only design; no scopes/expiry |
| `Domain/Aggregates/WebhookSubscription.cs` | Minimal; no events filter; soft deactivate |
| `Domain/Entities/IdempotencyLog.cs` | Good SDK safety net |
| `Domain/Entities/TinValidateCache.cs` | Privacy-aware id hashing |
| `Domain/Entities/{MsicCode,CountryCode,TaxType}.cs` | Simple lookup tables |
| `Domain/Rules/CancelWindowMustBeValidRule.cs` | 72h LHDN policy |

### Application

| File | Notes |
|------|-------|
| `Commands/SubmitTaxDocumentCommand.cs` | Core pipeline; credit deduct on accept; concurrent idempotency handling; credit failure only logged after save |
| `Commands/CancelTaxDocumentCommand.cs` | Domain + gateway; no customer webhook |
| `Commands/GenerateApiKeyCommand.cs` | Prefix-based test mode; uses `ITokenGeneratorService` |
| `Commands/RevokeApiKeyCommand.cs` | Outbox revoke event before save |
| `Commands/WebhookCommands.cs` | Ignores `events`; delete is soft |
| `Commands/UpdateLhdnCertificateCommand.cs` | Vault encrypt password only |
| `Commands/DispatchExternalWebhookCommand.cs` | Builds QR via portal URL; fire-and-forget per subscription |
| `Queries/LhdnQueries.cs` | Status via “recent 100” anti-pattern; hardcoded webhook events in list |
| `Queries/Agent/ListLhdnSubmissionsAgentQuery.cs` | Agent tool metadata present |
| `Ports/*` | Clean; gateway result records include RetryAfter |
| `Services/*` interfaces | Complete for current design |
| `Application/DependencyInjection.cs` | Empty marker for MediatR scan |

### Contracts

| File | Notes |
|------|-------|
| `LhdnDocumentSubmittedIntegrationEvent.cs` | Consumed by Billing deduct — conflicts with submit-time deduct |
| `LhdnDocumentValidatedIntegrationEvent.cs` | `(Uuid, Status, QrLink?)` — poller mis-invokes |
| `LhdnDocumentCancelledIntegrationEvent.cs` | Published; consumers not audited here |
| `ApiKeyRevokedIntegrationEvent.cs` | Host cache eviction — solid pattern |

### Infrastructure — data & API

| File | Notes |
|------|-------|
| `LhdnDbContext.cs` | Schema `lhdn`; unique org config; unique key hash; genesis seed |
| `Migrations/20260627124829_InitialLhdnSchema.cs` | Full initial schema; no later migrations (feature freeze risk) |
| `Repositories/LhdnRepository.cs` | Thin EF; no update helpers; no list keys |
| `Endpoints.cs` | Subset of TypeSpec; OrgAdmin; 402 via message prefix hack; no taxpayer validate / list keys |
| `DependencyInjection.cs` | Full registration; workers + event subscriptions |
| `EventHandlers/InvoiceIssued…` | **Stub data** — gap |
| `EventHandlers/ConsolidatedInvoice…` | Acceptable B2C shape |
| `EventHandlers/GatewayRefund…` | Smart 72h cancel vs CN; bypasses validation/credit/idempotency |

### Infrastructure — gateway, workers, services

| File | Notes |
|------|-------|
| `Gateways/LhdnGatewayAdapter.cs` | Production-grade parsing/rate limits; in-process only |
| `Workers/LhdnSubmissionJob.cs` | Double-charge event; no claim locking |
| `Workers/LhdnStatusPollingJob.cs` | Broken validated event args; good webhook fan-out |
| `Workers/LhdnReferenceDataSeederJob.cs` | Fragile path; MSIC seed uses classification_codes.json as MsicCode (naming mismatch risk) |
| `Services/LhdnReferenceDataSeeder.cs` | Alternate seeder (config path) — appears parallel/unused by DI workers |
| `Services/ScribanTemplateRendererService.cs` | Strong; precompile + escape |
| `Services/UblValidatorService.cs` | Strong; silent skip if schemas missing |
| `Services/DocumentStrategyFactory.cs` | Clean doc-type routing |
| `Services/Strategies/ViewModelMapper.cs` | Entity swap; hardcoded tenant party |
| `Services/Strategies/*Strategy.cs` | Thin wrappers over templates |
| `Services/Strategies/ViewModels/*` | Flat VM for Scriban |
| `Services/CertificateVaultService.cs` | AES-256 password encrypt; **PFX not encrypted**; master key from padded config string |
| `Services/WebhookSenderService.cs` | HMAC OK; no retries |
| `Services/TaxpayerValidationService.cs` | Cache + gateway; endpoint missing |
| `Services/LhdnQueryService.cs` | Dapper recent list only |
| `Services/LhdnLinkService.cs` | Portal URL config |

### Templates & schemas

| Path | Notes |
|------|-------|
| `Templates/StandardInvoice.xml` | Hardcoded supplier address; v1.1 placeholder |
| `Templates/ConsolidatedInvoice.xml` | B2C |
| `Templates/CreditNote.xml` | Shared 02/03/04 via `doc_type_code` |
| `Templates/SelfBilledInvoice.xml` / `SelfBilledCreditNote.xml` | Self-billed layouts |
| `Schemas/**/*.xsd` | Embedded OASIS set — good |

### Host / Billing / UI coupling

| File | Notes |
|------|-------|
| `src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | LHDN-table-coupled; reusable with abstraction |
| `src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | Cache eviction |
| `Modules/Billing/.../LhdnDocumentSubmittedIntegrationEventHandler.cs` | Deducts 1 credit — conflicts with submit command |
| `Modules/Billing/.../LhdnDocumentValidatedIntegrationEventHandler.cs` | Expects `VALIDATED` + QrLink — currently broken |
| `ops-page/.../TaxInvoiceDetailPanel.tsx` | Cancel when `VALIDATED`; status drift vs domain `VALID` |
| `developers-page/app/lhdn/route.ts` | OpenAPI reference for LHDN product |

### Packages & scripts

| Path | Notes |
|------|-------|
| `packages/api-spec/modules/lhdn/models.tsp` | Full DTO surface including fields endpoints don’t fully use (`events`, `is_test_mode` on response) |
| `packages/api-spec/modules/lhdn/routes.tsp` | Ahead of server implementation |
| `packages/lhdn-sdk-dotnet/**` | Kiota + idempotency handler |
| `packages/lhdn-sdk-ts/**` | Factory; weaker header/idempotency ergonomics |
| `scripts/lhdn_sandbox/*` | Good smoke suite; secrets in repo; v1.1 optional |
| `docs/lhdn/*` | Design notes on XML-DSig pain |
| `docs/architecture-decision-log/010-…` | Templating + Schematron + signature placeholder rules |
| `docs/architecture-decision-log/011-…` | SDK publish runbook |
| `docs/architecture-decision-log/020-…` | LHDN as Phase-1 CaaS pillar |
| `Modules/Lhdn/README.md` | Accurate feature matrix; honest about unsigned V1.0 focus |

### Tests

| File | Notes |
|------|-------|
| `LhdnRateLimitingTests.cs` | Minimal unit success path only |
| `LhdnSandboxE2ETests.cs` | Ignored live tests |
| `UblStrategyTests.cs` | Entirely commented golden masters — **major quality gap** |

---

## Executive summary

The LHDN module is a **credible V1.0 compliance gateway**: strategy-based UBL XML, embedded XSD preflight, async MyInvois submission/polling, intermediary mode, TIN cache service, developer keys with test mode, and outbound HMAC webhooks — all good patterns for Lazuar’s broader **platform integration APIs**.

It is **not yet a closed production loop** for CaaS:

1. Billing event contracts and credit charging are inconsistent (double charge + broken validated event).
2. Tenant legal master data and V1.1 signing are incomplete.
3. Public OpenAPI/SDKs advertise endpoints the server does not implement.
4. Cross-module handlers still use stub buyer data.
5. Worker concurrency, secret storage, Schematron, webhook reliability, and automated golden-master tests lag the architecture ADRs.

**Highest leverage next steps:** fix billing/status contracts → complete tenant config + template binding → align TypeSpec/endpoints/SDKs → restore golden-master tests → extract API key + webhook primitives into a platform module for reuse by Payments/Billing and future tax authorities (GSTN, Coretax, InvoiceNow).
