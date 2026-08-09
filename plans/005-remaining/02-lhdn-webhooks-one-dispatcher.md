# FW-2 — LHDN customer webhooks → One dispatcher (HOW analysis)

**Status:** Analysis only — **no app code changes** in this document.  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Track:** Product-scheduled future work (`plans/004-maintenance/FUTURE-WORK.md` §FW-2)  
**Locked platform decision:** `plans/004-maintenance/decisions.md` §00.2  
**Prior inventory:** `plans/004-maintenance/phase-04-analysis.md` (C freeze landed; A not coded)  
**Future checklists:** `plans/004-maintenance/checklists-future/phase-f05-webhooks-product-decisions.md`, `phase-f06-webhooks-one-dispatcher.md`

---

## 0. Executive summary

Lazuar has **two outbound customer-webhook paths**:

| Path | Module | Delivery | Status |
|------|--------|----------|--------|
| **Platform durable** | One | `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` + Standard Webhooks–style signing | Production platform model |
| **LHDN fire-and-forget** | Lhdn | `WebhookSenderService` one-shot HTTP, body-only HMAC | **C freeze** until product unlocks A |

Decision **00.2** locks end-state **A** (route LHDN through One) and **rejects B** (second Lhdn durable stack). Phase 04 already shipped freeze docs + failure metrics; it deliberately did **not** implement A.

**FW-2 is blocked on product answers** (signing, payload envelope, registry/routing, event catalog) before any enqueue PR. Once those lock, implementation is a **narrow convergence**: LHDN publishes into the existing One fan-out event (or One listens to LHDN lifecycle events), outbox rows appear, dispatcher delivers with platform signing/headers/retries, fire-and-forget is retired.

**Recommended default engineering posture (pending product):**

1. **Signing end-state:** One `t=,v1=` only.  
2. **Migration:** dual-verify window for integrators (document both; receivers accept either until date).  
3. **Payload:** One platform envelope wrapping LHDN `data` (or keep LHDN wire shape as `data` content with stable field names).  
4. **Registry:** migrate `lhdn.WebhookSubscriptions` → `one.TenantWebhookEndpoints` (dual-write cutover), filter by `EnabledEvents` including `invoice.valid` / `invoice.invalid`.  
5. **Do not** build Lhdn outbox.

If product cannot share One signing without unacceptable integrator break, **re-open 00.2 formally** and write an ADR for B — do not silently fork a second durable stack in Lhdn.

---

## 1. Source-of-truth map (read these first)

| Artifact | Absolute path | Role |
|----------|---------------|------|
| Decision 00.2 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md` | A end-state, C interim, reject B |
| FW-2 backlog | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/FUTURE-WORK.md` | Product blockers + done criteria |
| Phase 04 inventory | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/phase-04-analysis.md` | Dual-path file map, signing table |
| Phase 04 done | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/phase-04-done.md` | What freeze shipped |
| F05 gate | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f05-webhooks-product-decisions.md` | Product locks before code |
| F06 implement | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f06-webhooks-one-dispatcher.md` | Code checklist after F05 |
| Lhdn freeze README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/README.md` §5 | Operational freeze rules |
| One platform README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/README.md` §7 | Platform model + LHDN exception pointer |
| Integrator docs (One only) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/webhooks.md` | `t=,v1=` verify recipe; **no LHDN path** |

---

## 2. Current dual paths (as-built)

### 2.1 Comparison matrix

| Aspect | One (platform) | Lhdn (special-case, frozen) |
|--------|----------------|-----------------------------|
| **Role** | Only platform-grade durable delivery | Module-local fire-and-forget for e-invoice terminal status |
| **Registry table** | `one.TenantWebhookEndpoints` | `lhdn.WebhookSubscriptions` |
| **Registry model** | Multi-endpoint per org; URL, `SecretKey`, `IsActive`, `EnabledEvents` | Multi-URL per org; URL, `Secret`, `IsActive` only — **no event filter column** |
| **Secret ownership** | Platform generates `whsec_…` once on create (companion API / provision) | Caller supplies secret at register (`RegisterWebhookRequestDto.secret`) |
| **Delivery table** | `one.WebhookDeliveryOutboxes` | **None** |
| **Enqueue** | `OutboundWebhookEventHandlers` on `OutboundWebhookRequestedIntegrationEvent` | `DispatchExternalWebhookCommand` (in-process MediatR from poller) |
| **Dispatcher** | Hosted `OutboundWebhookDispatcherJob` | In-process `WebhookSenderService` inside command handler |
| **HTTP client** | Named `"DeveloperWebhooks"` (15s timeout) | Default `IHttpClientFactory` client (no named policy) |
| **Signing material** | `{unixTimestamp}.{rawBody}` | `rawBody` only |
| **Signature header** | `X-Lazuar-Signature: t={unix},v1={hmac_hex}` | `X-Lazuar-Signature: {hmac_hex}` (raw hex, no `t`/`v1`) |
| **Extra headers** | `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id` | **none** |
| **Retries / terminal** | Up to 5 attempts; exponential backoff `2^AttemptCount` minutes; then `FAILED` | One attempt; errors swallowed after log + metric |
| **Claim concurrency** | `FOR UPDATE SKIP LOCKED` + `ClaimLease` on `NextAttemptAt` (InMemory fallback) | N/A (sync POST in poll loop) |
| **Metrics** | `LazuarMetrics.RecordWebhookFailed("outbound")` | `RecordWebhookFailed("lhdn")` |
| **Delivery history API** | Workspace webhook logs (`GET .../webhooks/logs`) | None |
| **UI** | Ops Developer → Outbound Webhooks + Delivery Logs | **No UI** (API/SDK only) |
| **Public docs** | `apps/lazuar-docs/docs/integrations/webhooks.md` | Undocumented in hub docs (SDK routes exist) |
| **API surface** | `/api/v1/one/workspaces/{id}/webhooks` (+ logs) | `/api/v1/lhdn/webhooks` POST/GET/DELETE |
| **Event filter** | `AcceptsEvent`: empty `EnabledEvents` = all | Implicit all LHDN invoice events for every active URL |
| **Freeze discipline** | Platform path free to improve | Do **not** add outbox/retry/second signing stack without reopening 00.2 |

### 2.2 One path — runtime flow

```
Commerce / Payments publishers
  └─ OutboundWebhookRequestedIntegrationEvent
       OrganizationId, TargetUrl (null = fan-out), EventType, Payload (JsonElement)
         │
         ▼
One.OutboundWebhookEventHandlers
  • Load active TenantWebhookEndpoints for OrganizationId (IgnoreQueryFilters)
  • Filter AcceptsEvent(EventType)
  • Wrap envelope:
      { id, event_type, created_at, data: <payload> }  // snake_case JSON
  • Insert WebhookDeliveryOutbox row per matching endpoint
         │
         ▼
one.WebhookDeliveryOutboxes  (PENDING, NextAttemptAt)
         │
         ▼
OutboundWebhookDispatcherJob (poll OutboundWebhookInterval)
  • Claim PENDING with SKIP LOCKED + ClaimLease
  • Load endpoint secret
  • Sign: OutboundWebhookSignature.ComputeHeaderValue(secret, payload, unixTs)
  • POST application/json + platform headers
  • RecordSuccess / RecordFailure (retry or FAILED at 5)
```

**Key files (absolute):**

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs` | Registry + `AcceptsEvent` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs` | Outbox row, lease, 5-attempt machine |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookUrlValidator.cs` | HTTPS / loopback URL rules |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs` | Integration event (lives in **Commerce.Contracts**) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs` | Fan-out → outbox |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | Claim + HTTP delivery |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` | Sign + `TryVerify` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/DependencyInjection.cs` | `"DeveloperWebhooks"` client, hosted job, event subscribe |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs` | Workspace CRUD + logs |

**Publishers into One today (LHDN is not among them):**

| Event type | Source |
|------------|--------|
| `subscription.activated` / `.suspended` / `.canceled` / `.resumed` | Commerce lifecycle handlers |
| `subscription.past_due` | Commerce `BillingEngineJob` |
| `order.completed` | Commerce order completed |
| `payment_link.paid` | Commerce custom payment link |
| `payment.completed` / `payment.failed` | Payments `IntegrationCheckoutGatewayEventsHandler` |

Payments already takes a project reference on `Modules.Commerce.Contracts` specifically so it can publish `OutboundWebhookRequestedIntegrationEvent` (comment in `Modules.Payments.Infrastructure.csproj`). That is the **template** for how Lhdn can join the same bus without inventing a second outbox.

### 2.3 Lhdn path — runtime flow

```
LhdnStatusPollingJob
  MyInvois status VALID or INVALID
    ├─ VALID: MarkAsValid + LhdnDocumentValidatedIntegrationEvent (internal, Billing listens)
    │         + DispatchExternalWebhookCommand(...)
    └─ INVALID: MarkAsInvalid
              + DispatchExternalWebhookCommand(...)   // no LhdnDocumentInvalidated event today
         │
         ▼
DispatchExternalWebhookCommandHandler
  • GetActiveWebhooksAsync(OrganizationId)
  • Build qr_link via ILhdnLinkService.GetPortalUrl()
  • Wire payload:
      {
        "event": "invoice.valid" | "invoice.invalid",
        "data": {
          "internal_id", "lhdn_uuid", "status",
          "qr_link", "error_message", "timestamp"
        }
      }
  • For each WebhookSubscription: await WebhookSenderService.SendWebhookAsync
         │
         ▼
WebhookSenderService
  • HMAC-SHA256(secret, body bytes) → lowercase hex
  • Header X-Lazuar-Signature = hex only
  • POST; on failure: log + RecordWebhookFailed("lhdn"); never rethrow for retry
```

**Key files (absolute):**

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs` | Registry aggregate |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Services/IWebhookSenderService.cs` | Port |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` | Fire-and-forget HMAC POST |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Payload build + loop send |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs` | Register / soft-delete |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` | List (hardcodes events list) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | VALID/INVALID → dispatch |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminWebhookEndpoints.cs` | `/lhdn/webhooks` admin API |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Registers `IWebhookSenderService` |

**Internal integration events (not customer webhooks):**

| Event | When | Customer webhook? |
|-------|------|-------------------|
| `LhdnDocumentValidatedIntegrationEvent` | VALID poll | **No** — Billing uses it; customer path is separate command |
| `LhdnDocumentSubmittedIntegrationEvent` | Submission job | No customer webhook |
| `LhdnDocumentCancelledIntegrationEvent` | Cancel path | No customer webhook |

Important: **INVALID does not publish** an integration event today — only `DispatchExternalWebhookCommand`. Any design that “just subscribe One to LHDN integration events” must either add an invalid event or keep an explicit outbound publish on both branches.

### 2.4 Registry / API surface mismatch (integrator-visible debt)

| Issue | Detail |
|-------|--------|
| **Dual registries** | LHDN URLs in `lhdn.WebhookSubscriptions`; commerce/payments URLs in `one.TenantWebhookEndpoints`. Same customer may register twice with different secrets. |
| **Events field is theater** | TypeSpec `RegisterWebhookRequestDto.events: string[]` is accepted but **not stored** on domain. List handler hardcodes `["invoice.valid","invoice.invalid"]`. |
| **Secret model differs** | LHDN: caller-chosen secret. One: platform-minted `whsec_` returned once. Migration must map secrets (preserve caller secret on import, or force remint). |
| **URL validation** | One uses `WebhookUrlValidator` (https / loopback). LHDN register path does not share that validator today. |
| **Docs** | Hub webhooks doc describes **One only**. LHDN SDK exposes routes but signing recipe for LHDN body-hex is not in `lazuar-docs`. |
| **Blocking poller** | Fire-and-forget still runs **synchronously inside** `LhdnStatusPollingJob` via MediatR; slow customer endpoints stall status processing for that doc batch. |

### 2.5 Payload shapes today

**One envelope** (built in `OutboundWebhookEventHandlers`):

```json
{
  "id": "<uuid v7>",
  "event_type": "subscription.activated",
  "created_at": "<ISO>",
  "data": { /* publisher JsonElement, snake_case fields */ }
}
```

**LHDN wire body** (built in `DispatchExternalWebhookCommandHandler`):

```json
{
  "event": "invoice.valid",
  "data": {
    "internal_id": "...",
    "lhdn_uuid": "...",
    "status": "VALID",
    "qr_link": "https://…/{uuid}/share/{longId}",
    "error_message": null,
    "timestamp": "<ISO>"
  }
}
```

Notes:

- LHDN uses top-level key **`event`**, One uses **`event_type`**.  
- LHDN has no platform delivery `id` in body (delivery id is only a One header today).  
- Field names inside `data` are the compliance-relevant contract integrators actually care about (`internal_id`, `lhdn_uuid`, `qr_link`, etc.).

### 2.6 What Phase 04 already did (do not redo)

- Inventory + signing comparison documented.  
- Lhdn README §5 freeze rules; One README §7 platform model.  
- `WebhookSenderService`: structured failure logs + `RecordWebhookFailed("lhdn")`.  
- **Not done:** A convergence code, registry migration, signing change, delete fire-and-forget.

---

## 3. Signing differences (integrator-breaking surface)

### 3.1 Algorithms side-by-side

| | One | Lhdn |
|--|-----|------|
| **Algorithm** | HMAC-SHA256 | HMAC-SHA256 |
| **Key** | UTF-8 secret bytes | UTF-8 secret bytes |
| **Message** | `"{unix}.{body}"` | `body` only |
| **Encoding** | lowercase hex | lowercase hex |
| **Header value** | `t=<unix>,v1=<hex>` | `<hex>` only |
| **Replay resistance** | Timestamp + receiver skew (`TryVerify` default 300s) | **None** |
| **Helper** | `OutboundWebhookSignature.ComputeHeaderValue` / `TryVerify` | Inline in `WebhookSenderService` |

### 3.2 Same header name, different semantics

Both paths use **`X-Lazuar-Signature`**. A receiver that only implements One parse (`t=…,v1=…`) will **fail** on current LHDN deliveries. A receiver that only does body-hex equality will **fail** on One deliveries. There is **no version negotiation header** today.

### 3.3 Dual-verify (integrator migration pattern)

“Dual-verify” is an **integrator-side** (and docs/SDK) strategy during cutover, optionally assisted by a short **platform dual-sign window**.

**Receiver dual-verify (recommended for customers during window):**

```text
function verify(secret, rawBody, signatureHeader):
  if header looks like t=…,v1=…:
    return StandardWebhooksVerify(secret, rawBody, header)  // One scheme
  else:
    return ConstantTimeEq(hex(hmac_sha256(secret, rawBody)), header)  // legacy LHDN
```

**Platform dual-sign options (if product requires zero-break day 1):**

| Option | Platform behavior | Pros | Cons |
|--------|-------------------|------|------|
| **D0 — One-only (hard cut)** | Only `t=,v1=` after switch | Simple; matches platform docs | Breaks body-hex-only receivers |
| **D1 — Dual headers (preferred dual-sign)** | `X-Lazuar-Signature` = One format; add `X-Lazuar-Signature-Legacy` = body hex | One header stays canonical; legacy clients read second header | Docs + temporary code in dispatcher |
| **D2 — Dual values in one header** | e.g. `t=…,v1=…,v0=<bodyhex>` | Single header | Nonstandard; parse complexity |
| **D3 — Two POSTs** | Deliver twice with different schemes | Worst: doubles load, double side effects | **Reject** |

**Engineering recommendation:** Prefer **D0** if prod LHDN subscription count is zero or all known integrators can ship dual-verify quickly; otherwise **D1** for a dated window (e.g. 30–60 days), then remove legacy header. Do **not** permanently keep dual-sign.

**Where dual-sign would live if chosen:** only in `OutboundWebhookDispatcherJob` (or a thin helper next to `OutboundWebhookSignature`) — **not** a second Lhdn sender. That keeps 00.2’s “one durable path” intact while honoring migration.

### 3.4 Secret format notes

- One secrets are typically `whsec_…` prefixes (documentation + provision). HMAC uses the full string as key bytes (prefix included unless product later adopts Stripe-style base64 decode — **current code does not strip `whsec_`**).  
- LHDN secrets are free-form caller strings.  
- After registry merge, **preserve existing LHDN secrets** when importing rows so dual-verify works without remint; new One endpoints still mint `whsec_`.

---

## 4. Product decision blockers (F05 gate — write answers before code)

These four questions are **explicit FW-2 / F05 blockers**. Implementation PRs must not guess.

### 4.1 Signing

| Choice | Meaning |
|--------|---------|
| **S-A** Keep LHDN body-only forever | Conflicts with 00.2 “One signing is platform” — effectively re-opens B or permanent dual scheme |
| **S-B** Move to One `t=,v1=` only (hard cut) | Clean; requires notice + integrator verify update |
| **S-C** Move to One with **dual-verify / dual-sign window** | Migration-safe; needs end date |

**Lock required:** S-B or S-C (S-C recommended if any production LHDN webhook rows exist).  
**If product insists S-A durable:** re-open `decisions.md` §00.2 + ADR for permanent dual scheme or B.

### 4.2 Payload envelope

| Choice | Wire body | Breaking? |
|--------|-----------|-----------|
| **P-A Keep LHDN envelope** | `{ "event", "data" }` only; no One wrapper | Receivers unchanged; inconsistent with platform envelope |
| **P-B One envelope, LHDN data inside** | `{ "id", "event_type":"invoice.valid", "created_at", "data": { internal_id, … } }` | **Yes** for parsers of top-level `event` |
| **P-C Versioned** | e.g. `api_version` / dual body during window | More work; cleanest long-term |

**Recommendation:** **P-B** as end-state (platform consistency), with:

- Stable `data.*` field names matching today’s LHDN `data` object.  
- `event_type` = `invoice.valid` | `invoice.invalid`.  
- Integrator notice that top-level key renames `event` → platform envelope.  
- Optional short dual-payload only if a major ERP cannot change quickly (prefer not).

### 4.3 Routing / registry

| Choice | Who receives LHDN events |
|--------|---------------------------|
| **R-A All workspace One endpoints** | Every active `TenantWebhookEndpoint` with empty filter **or** that includes invoice events |
| **R-B Event-filtered only** | Only endpoints whose `EnabledEvents` contains `invoice.valid` / `invoice.invalid` (empty filter still = all under current `AcceptsEvent` semantics — product must clarify whether empty means “all commerce” vs “all platform including LHDN”) |
| **R-C Keep Lhdn table as config source** | Continue reading `lhdn.WebhookSubscriptions` but enqueue One outbox rows (hybrid registry) |
| **R-D Migrate registry to One** | Import LHDN rows → One endpoints; Lhdn API becomes façade or deprecates |

**Recommendation:** **R-D + R-B clarified**:

1. Migrate URLs/secrets into One endpoints with `EnabledEvents = ["invoice.valid","invoice.invalid"]` (not empty — avoids accidental fan-out of all commerce events to LHDN-only URLs and vice versa if product wants separation).  
2. Document: empty `EnabledEvents` means **all platform events including invoice.*** (current code already treats empty as all). Product may instead want empty = “commerce default set” — that would be a **behavior change** to `AcceptsEvent` and needs explicit lock.

**Critical product nuance:** If an integrator has one Zapier URL for payments and a separate ERP URL for LHDN, empty-filter endpoints will receive **both** after convergence. That may be desired (one endpoint, filter later) or not (strict separation). Lock this before cutover scripts.

### 4.4 Event catalog

| Event | Today customer webhook? | Recommendation |
|-------|-------------------------|----------------|
| `invoice.valid` | Yes | Keep name (already aligned list DTO ↔ wire) |
| `invoice.invalid` | Yes | Keep name |
| `invoice.submitted` | No | Optional later; out of FW-2 MVP unless product expands |
| `invoice.cancelled` | No | Optional later |

**Also fix honesty:** TypeSpec `events[]` on register must either be stored (map to One `EnabledEvents`) or removed from contract.

### 4.5 Written lock artifact

Before F06 code:

- Commit answers to something like  
  `plans/004-maintenance/webhook-convergence-decisions.md`  
  (as F05.4 already requires) **or** extend `decisions.md` with §00.2-A appendix.  
- Include: dual-sign end date, breaking-change channel (changelog, SDK notes, email if needed), prod row-count check procedure.

### 4.6 Prod inventory queries (ops gate)

Run before choosing hard cut vs dual-sign:

```sql
-- LHDN active customer webhooks
SELECT COUNT(*) FROM lhdn."WebhookSubscriptions" WHERE "IsActive" = true;

-- Distinct orgs still on LHDN registry
SELECT COUNT(DISTINCT "OrganizationId") FROM lhdn."WebhookSubscriptions" WHERE "IsActive" = true;

-- One endpoints already present (for dual-registration risk)
SELECT COUNT(*) FROM one."TenantWebhookEndpoints" WHERE "IsActive" = true;
```

If LHDN active count is **0**, prefer hard cut (S-B + P-B + R-D) with docs-only dual-verify recipe for future-proof receivers.

---

## 5. Implementation design options (end-state A)

All options below assume **reject B** (no Lhdn outbox table, no second dispatcher job).

### 5.1 Option overview

| ID | Name | Enqueue mechanism | Registry | Signing | Complexity |
|----|------|-------------------|----------|---------|------------|
| **A1** | Lhdn publishes `OutboundWebhookRequestedIntegrationEvent` | Poller / command → event bus → existing One handler | One endpoints (after migrate) | One (+ optional dual-sign) | Low–medium |
| **A2** | One subscribes to LHDN lifecycle integration events | One handler on `LhdnDocumentValidated` (+ new invalid event) | One | One | Medium (event gaps) |
| **A3** | Hybrid: Lhdn registry still source of truth; command writes One outbox directly | Skip event bus; inject outbox writer | Lhdn table | One | Medium–high; couples modules |
| **A4** | Dual-write registry + dual delivery during cutover | Both paths briefly | Both | Both | Highest operational risk |

**Recommended:** **A1** (mirrors Payments pattern), with **registry migration R-D**, signing **S-C dual-verify docs + optional D1 dual-sign**, payload **P-B**.

**A2** is attractive if we want Lhdn poller free of “customer webhook” concerns, but requires:

- `LhdnDocumentInvalidatedIntegrationEvent` (or generic status event).  
- One.Infrastructure reference to Lhdn.Contracts (mirror of how One already references Commerce.Contracts for the outbound event).  
- Careful not to double-fire if poller still sends commands during cutover.

**A3** violates modular boundaries more harshly (Lhdn writing `one.WebhookDeliveryOutboxes` or calling One internals) unless mediated by a Contracts port — still worse than publishing the existing public integration event.

**A4** only for multi-week cutover with active integrators; default avoid.

### 5.2 Recommended design detail (A1 + dual-verify)

#### 5.2.1 Enqueue

Replace the body of `DispatchExternalWebhookCommandHandler` (or replace the command entirely) with:

1. Build **data** object (same fields as today):  
   `internal_id`, `lhdn_uuid`, `status`, `qr_link`, `error_message` (timestamp can move to envelope `created_at`).  
2. `JsonSerializer.SerializeToElement(data, snake_case)`.  
3. Publish:

```csharp
new OutboundWebhookRequestedIntegrationEvent(
    OrganizationId: request.OrganizationId,
    TargetUrl: null, // fan-out; do not reintroduce URL equality gate
    EventType: $"invoice.{request.Status.ToLowerInvariant()}", // invoice.valid | invoice.invalid
    Payload: dataElement)
```

4. **Do not** call `IWebhookSenderService`.

**Module reference:** Add `Modules.Commerce.Contracts` to Lhdn.Infrastructure (and possibly Application if publish lives there), same comment style as Payments.

**Event bus keying:** Follow how Payments/Commerce publish (which bus / outbox). Lhdn already publishes `LhdnDocumentValidatedIntegrationEvent` via its module event bus — the outbound request must use a bus that **One’s subscription** receives (host `IEventBus` / cross-module subscription model already used for Commerce → One). Verify composition in host `Program` / module `Use*Subscriptions` before coding; do not invent a new bus topology.

#### 5.2.2 Fan-out / filter (no One handler change required if R-D done)

Existing `OutboundWebhookEventHandlers` already:

- Fans out to all active endpoints for org.  
- Filters `AcceptsEvent(eventType)`.  
- Wraps platform envelope.  
- Creates outbox rows.

So if migrated endpoints have `EnabledEvents` containing `invoice.valid`/`invoice.invalid` (or empty = all), **no handler rewrite is strictly required**.

Optional hardening (product-dependent):

- Catalog allowlist for known event types (docs + optional validation on `EnabledEvents` update).  
- Metrics tag for event family (`invoice` vs `payment`).

#### 5.2.3 Delivery

Unchanged core: `OutboundWebhookDispatcherJob` + `OutboundWebhookSignature`.

If product chooses dual-sign **D1**:

- After computing One header, also set  
  `X-Lazuar-Signature-Legacy: {hex(hmac(secret, body))}`.  
- Gate with feature flag or `BackgroundWorkerOptions` / config  
  `OutboundWebhooks:EmitLegacyBodyHmacHeader` until end date.  
- Metric/log when legacy header emitted for audit.

#### 5.2.4 Registry migration

**Phase M1 — dual-write (optional if row count high):**

- `RegisterWebhookCommand` → also create/update One `TenantWebhookEndpoint` with same URL/secret and invoice event filters.  
- List Lhdn API can façade over One.  
- Delete deactivates both.

**Phase M2 — backfill:**

- One-shot migration job or SQL script: for each active Lhdn subscription without matching One URL, insert One endpoint.  
- Id mapping table optional (support tickets: old Lhdn id → new One id).

**Phase M3 — dual-read stop:**

- Lhdn register/list/delete become façades over One (similar to API key façades under 00.1) **or** deprecate with 410 + docs pointing to One companion API.  
- Drop or archive `lhdn.WebhookSubscriptions` ≥ 30 days after zero reads (product/ops).

**Secret preservation:** On backfill, copy Lhdn `Secret` into One `SecretKey` as-is (no remint) so dual-verify works without customer action beyond code update.

**URL validation:** Apply `WebhookUrlValidator` on new registers; for backfill, quarantine invalid URLs (log + skip + ops list) rather than fail entire migration.

#### 5.2.5 Retire fire-and-forget

After cutover:

1. Remove calls to `IWebhookSenderService` from dispatch path.  
2. Delete or obsolete `WebhookSenderService`, port, DI registration.  
3. Retire metric tag `lhdn` for outbound (or keep only if any residual path). Failures appear as `outbound` via dispatcher.  
4. Remove freeze section from Lhdn README; update One README event list to include `invoice.*`.  
5. Mark FW-2 done in `FUTURE-WORK.md`.

#### 5.2.6 Payload mapping table (implement as code + tests)

| LHDN input | Outbound event type | One envelope `data` fields |
|------------|---------------------|----------------------------|
| Poll VALID | `invoice.valid` | `internal_id`, `lhdn_uuid`, `status=VALID`, `qr_link`, `error_message=null` |
| Poll INVALID | `invoice.invalid` | `internal_id`, `lhdn_uuid`, `status=INVALID`, `qr_link` (may be null), `error_message` |

**Not in FW-2 MVP unless product expands:** submitted, cancelled, credit note, self-billed variants as separate event types.

#### 5.2.7 Correlation / ids

| Id | Source | Propagate how |
|----|--------|---------------|
| OrganizationId | Document / command | Event + outbox tenant |
| Internal reference | Document | `data.internal_id` |
| LHDN UUID | MyInvois result | `data.lhdn_uuid` |
| Integration event Id | New outbound event | Can seed envelope `id` only if handler changed; today handler generates **new** uuid per fan-out — acceptable |
| Delivery Id | Outbox row Id | Header `X-Lazuar-Delivery-Id` |
| Webhook Id | Endpoint Id | Header `X-Lazuar-Webhook-Id` |

Optional improvement (not required for A): pass publisher event id into outbox for end-to-end correlation — separate small PR.

#### 5.2.8 Failure / retry semantics (inherit One)

| Behavior | Value |
|----------|-------|
| Max attempts | 5 |
| Backoff | `2^AttemptCount` minutes |
| Lease | `ClaimLease` + SKIP LOCKED |
| Success | HTTP 2xx |
| Terminal | Status `FAILED`, metric `outbound` |
| Customer latency | **Decoupled** from Lhdn status poller (major win vs today) |

### 5.3 Option A2 sketch (if product prefers “events only”)

1. Add `LhdnDocumentInvalidatedIntegrationEvent` (org, internal id, uuid?, error).  
2. One handler (new class or extend outbound handlers) maps validated/invalid → same payload table → either:  
   - publish internal `OutboundWebhookRequested` (double hop), or  
   - write outbox rows directly (duplicate fan-out logic — avoid).  
3. Remove `DispatchExternalWebhookCommand` customer path.

Prefer A1 to avoid duplicating fan-out logic and to keep One as pure “outbound requested” consumer.

### 5.4 What not to build

- Lhdn `WebhookDeliveryOutboxes` table.  
- Second hosted dispatcher.  
- Body-hex as long-term platform default.  
- Silent URL equality gate between product fulfillment URLs and endpoints (already removed for Commerce; do not reintroduce for LHDN).  
- `Modules/Webhooks` extract (00.2 / 00.6 — stay in One unless Phase 16 product trigger).

---

## 6. File-level change plan (when coding — not this PR)

### 6.1 Product lock doc (F05) — docs only

| File | Change |
|------|--------|
| `plans/004-maintenance/webhook-convergence-decisions.md` (**new**) | Lock S/P/R/event answers, dual-sign end date, cutover date |
| `plans/004-maintenance/FUTURE-WORK.md` | Link lock doc; leave FW-2 open until F06 exit |
| `plans/004-maintenance/checklists-future/phase-f05-*.md` | Check boxes when locked |

### 6.2 Enqueue path (FW-2.2 / F06.2)

| File | Change |
|------|--------|
| `Modules/Lhdn/Infrastructure/Modules.Lhdn.Infrastructure.csproj` | ProjectReference `Modules.Commerce.Contracts` (+ comment like Payments) |
| `Modules/Lhdn/Application/Modules.Lhdn.Application.csproj` | Reference if publish moves to Application layer |
| `Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Replace sender loop with `OutboundWebhookRequestedIntegrationEvent` publish; build data payload only |
| `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | Possibly thin: keep command send, or publish event inline; ensure **both** VALID and INVALID paths enqueue |
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Ensure event bus used for outbound is the shared bus One listens on |

**No change required (likely):**

| File | Why |
|------|-----|
| `OutboundWebhookEventHandlers.cs` | Already fan-out + envelope + `AcceptsEvent` |
| `WebhookDeliveryOutbox.cs` | Already durable |
| `OutboundWebhookDispatcherJob.cs` | Unless dual-sign D1 |

### 6.3 Dual-sign (only if S-C + D1)

| File | Change |
|------|--------|
| `Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` | Add `ComputeLegacyBodyHmacHex(secret, body)` helper |
| `Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | Optionally add `X-Lazuar-Signature-Legacy` under config flag |
| Host config / `BackgroundWorkerOptions` or `appsettings` | Flag + optional end-date log warning |
| `apps/lazuar-docs/docs/integrations/webhooks.md` | Dual-verify recipe + deprecation date |

### 6.4 Registry migration (R-D)

| File | Change |
|------|--------|
| `Modules/Lhdn/Application/Commands/WebhookCommands.cs` | Dual-write or façade to One create/deactivate |
| `Modules/One/Application/Commands/*Webhook*` (existing create/update) | Possibly allow caller-supplied secret for migration import path (security-review: admin/migration only) |
| New migration script or one-shot admin command | Backfill Lhdn → One |
| `Modules/Lhdn/Infrastructure/Endpoints/AdminWebhookEndpoints.cs` | Façade or deprecation headers |
| `Modules/Lhdn/Application/Queries/LhdnQueries.cs` | List from One if façade |
| EF: optional later drop `lhdn.WebhookSubscriptions` | **After** dual-read period, not day 1 |

**Caller-supplied secret tension:** One create currently mints secret. Migration import needs either:

- Internal `CreateWebhookEndpointCommand` overload with explicit secret (migration/façade only), or  
- Direct domain construction in a migration seeder inside One.

Do **not** expose arbitrary secret override on public companion API without product OK (secret stuffing / weak secrets).

### 6.5 Retire fire-and-forget (FW-2.3 / F06.3)

| File | Change |
|------|--------|
| `Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` | Delete or `[Obsolete]` empty |
| `Modules/Lhdn/Application/Services/IWebhookSenderService.cs` | Delete |
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Remove registration |
| Grep cleanup | No remaining `IWebhookSenderService` / body-only sign for customer path |
| Metrics | Confirm failures use `outbound`; remove dead `lhdn` paths or keep counter for unrelated future use |

### 6.6 Docs honesty (FW-2.4 / F06.4)

| File | Change |
|------|--------|
| `Modules/Lhdn/README.md` §5 | Remove freeze; document One path + migration |
| `Modules/One/README.md` §7 | Add `invoice.valid` / `invoice.invalid` to event list; remove “LHDN exception” once true |
| `apps/lazuar-docs/docs/integrations/webhooks.md` | Invoice events table; dual-verify if needed; deprecate `/lhdn/webhooks` if façade |
| TypeSpec `packages/api-spec/modules/lhdn/models.tsp` | Align `events` semantics or document façade |
| LHDN SDKs | Regenerated clients if API changes; README signing notes |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | Mark dual-path residual closed when done (optional hygiene) |
| Changelog | Breaking note if signing/payload change |

### 6.7 Explicit non-touch (this epic)

- Frontend Ops UI for LHDN webhooks (optional later residual; One UI already exists).  
- BuildingBlocks webhook primitive extract.  
- New modules.  
- Changing Commerce/Payments publishers.  
- API key dual-read (FW-1 — independent).

---

## 7. Tests plan

### 7.1 Existing coverage to keep green

| Test file | Focus |
|-----------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookTests.cs` | Signature format, TryVerify, fan-out, AcceptsEvent (includes assert empty filter accepts `invoice.valid`) |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookClaimTests.cs` | Claim lease / SKIP LOCKED InMemory |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionLifecycleWebhookTests.cs` | Commerce → outbound event |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/IntegrationCheckoutOutboundWebhookTests.cs` | payment.completed/failed |

### 7.2 New / extended tests for FW-2

#### Unit — enqueue (Lhdn)

| Test | Assert |
|------|--------|
| `Dispatch_Valid_Publishes_OutboundWebhookRequested_With_InvoiceValid` | EventType `invoice.valid`; payload has `internal_id`, `lhdn_uuid`, `status`, `qr_link`; **no** call to `IWebhookSenderService` |
| `Dispatch_Invalid_Publishes_InvoiceInvalid` | EventType `invoice.invalid`; `error_message` present |
| `Dispatch_Does_Not_Http_Post_Directly` | Mock event bus captured; sender mock `DidNotReceive` |
| `QrLink_Built_When_Uuid_And_LongId_Present` | Matches portal URL pattern |
| `QrLink_Null_When_Missing_Ids` | null qr_link |

#### Unit — fan-out (One, reuse patterns)

| Test | Assert |
|------|--------|
| `FanOut_InvoiceValid_To_Endpoints_With_EnabledEvents` | Endpoint with `["invoice.valid"]` gets outbox; endpoint with only `payment.completed` does not |
| `FanOut_InvoiceValid_EmptyEnabledEvents_Receives_If_Product_Says_All` | Documents R-B lock |
| `Envelope_Contains_EventType_And_Data_Fields` | Snake_case; data nested |

#### Unit — signing migration

| Test | Assert |
|------|--------|
| Existing One signature tests remain | Unchanged |
| `Legacy_Body_Hmac_Helper_Matches_Old_WebhookSenderService` | Golden hex for fixed body/secret (parity fixture) |
| `Dispatcher_Emits_Legacy_Header_When_Flag_On` | Only if D1 implemented |
| `Dispatcher_Omits_Legacy_Header_When_Flag_Off` | Default after window |

#### Unit / integration — registry

| Test | Assert |
|------|--------|
| `Backfill_Copies_Url_And_Secret_And_Invoice_Events` | One endpoint fields |
| `Register_Lhdn_Facade_Creates_One_Endpoint` | If façade chosen |
| `Delete_Deactivates_One_Endpoint` | Soft deactivate |

#### Regression — poller decoupling

| Test | Assert |
|------|--------|
| Status poller VALID path marks document valid **even if** outbound publish is stubbed | Domain status not coupled to customer HTTP |
| (Optional) No HTTP from Lhdn module on VALID | Architecture test / mock |

#### Explicitly not required for FW-2 MVP exit

- Full MyInvois sandbox e2e (manual residual).  
- Load test of dual-sign.  
- Frontend Ops LHDN webhooks page.

### 7.3 Manual staging checklist

1. Register One endpoint with `enabled_events: ["invoice.valid","invoice.invalid"]` (or migrate).  
2. Submit sandbox document → VALID.  
3. Observe outbox row PENDING → SUCCESS; delivery logs API shows `invoice.valid`.  
4. Verify signature with One recipe; if dual-sign, also verify legacy header.  
5. Force 500 receiver → confirm retries then FAILED; metric `outbound`.  
6. Confirm Lhdn fire-and-forget no longer fires (tcpdump / mock receiver count = 1 path).  
7. INVALID path with error_message.  
8. Endpoint filtered to `payment.completed` only → no invoice delivery.

---

## 8. Risks and mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Integrator signature break** | High | Dual-verify docs; optional dual-sign D1 with end date; changelog; SDK notes |
| **Payload top-level shape break** (`event` vs envelope) | High | Product lock P-*; preserve `data.*`; sample payloads in docs |
| **Double delivery during cutover** (fire-and-forget + One) | High | Feature flag: disable sender in same deploy as enable publish; never leave both on in prod |
| **Silent zero delivery** after migrate (no One endpoints) | High | Backfill before cutover; log “no active endpoints”; preflight SQL counts |
| **Empty EnabledEvents fan-out pollution** | Medium | Migrate LHDN-only URLs with explicit invoice event list; document empty = all |
| **Secret remint forces customer outage** | Medium | Preserve secrets on backfill |
| **Module boundary / wrong event bus** | Medium | Follow Payments reference + host subscription verification tests |
| **Lhdn still blocks on HTTP if dual path left on** | Medium | Delete sender path; measure poller latency |
| **Caller-supplied weak secrets on façade** | Medium | Prefer One mint for new; preserve only for migrated rows |
| **INVALID path forgotten** (no integration event) | Medium | A1 keeps command for both; A2 must add invalid event |
| **SSRF / http URLs from legacy LHDN rows** | Medium | Validate on new write; quarantine bad backfill URLs |
| **Ops confusion dual registries mid-cutover** | Medium | Short dual-write window; status page in README |
| **Metric continuity** | Low | Dashboard update `lhdn` → `outbound` |
| **Reopening 00.2 under pressure** | Process | If dual scheme becomes permanent, ADR + decisions edit — no silent B |

### 8.1 Freeze rules still apply until A ships

Until the cutover PR merges to production:

1. Do not build Lhdn outbox.  
2. Do not “improve” fire-and-forget into durable.  
3. Do not invent a third signing scheme.  
4. Allowed: docs, metrics, this analysis, F05 decision doc, tests for future path behind flag if needed.

---

## 9. PR sequence (recommended)

Order is **strict** for production safety. Each PR should be independently revertible.

### PR-0 — Product lock (docs only)

- **Title:** `docs(webhooks): lock LHDN→One convergence decisions (F05 / FW-2 gate)`  
- **Contents:** `webhook-convergence-decisions.md` with S/P/R/event answers; update F05 checklist; link from FUTURE-WORK.  
- **Exit:** Engineering unblocked; no runtime change.

### PR-1 — Enqueue plumbing behind safety (no dual delivery)

- **Title:** `feat(lhdn): publish invoice lifecycle to OutboundWebhookRequested (no dual send)`  
- **Contents:**  
  - ProjectReference Commerce.Contracts.  
  - Change dispatch handler to publish event **and remove** `WebhookSenderService` call in the **same** PR (avoid double POST).  
  - Unit tests for publish payload.  
- **Risk:** Orgs with only Lhdn registry and **no** One endpoints get zero deliveries until PR-2.  
- **Mitigation:** Prefer ship PR-2 (backfill) **before** or **with** PR-1 in environments with active Lhdn rows; or feature-flag publish with temporary dual path only in staging.

**Safer split if prod rows > 0:**

#### PR-1a — Backfill + dual-write registry

- Import active Lhdn subscriptions → One endpoints (`EnabledEvents` invoice.*).  
- Dual-write on register/delete.  
- Verify counts.

#### PR-1b — Switch delivery path

- Publish outbound event; remove fire-and-forget.  
- Tests green.

### PR-2 — Dual-sign / docs (if S-C)

- **Title:** `feat(webhooks): optional legacy body HMAC header + dual-verify docs`  
- Flag default **on** if customers need it, else off.  
- Docs + golden tests.

### PR-3 — Façade or deprecation of `/lhdn/webhooks`

- **Title:** `refactor(lhdn): webhook admin API façades One endpoints`  
- Align TypeSpec `events` with stored filters.  
- SDK regen if needed.

### PR-4 — Cleanup

- **Title:** `chore(lhdn): remove WebhookSenderService and freeze docs`  
- Delete dead code; README updates; FUTURE-WORK FW-2 marked done; F06 checklist complete.  
- Metric dashboard note.

### PR-5 — (Optional, ≥30 days later) Drop `lhdn.WebhookSubscriptions`

- Only after zero reads and façades gone or pure One.  
- EF migration archive/drop.

### Suggested commit subjects (mirror FUTURE-WORK)

```
docs(webhooks): lock LHDN→One convergence decisions (FW-2 F05)
feat(lhdn): backfill webhook subscriptions into One endpoints
feat(webhooks): deliver LHDN lifecycle events via One dispatcher (FW-2)
feat(webhooks): dual-sign legacy header for LHDN migration window
docs(webhooks): invoice events + dual-verify integrator guide
chore(lhdn): remove fire-and-forget WebhookSenderService
```

### Dependency on other tracks

| Track | Interaction |
|-------|-------------|
| **FW-1 API keys** | Independent; both touch Lhdn façades/docs — avoid mega-PRs combining key cutover + webhook cutover |
| **Aura provision webhooks** | One endpoints already created with secrets; invoice events need `EnabledEvents` or empty-all — product call |
| **Frontend** | Out of scope; Ops already lists One webhooks/logs |

---

## 10. Done criteria (from FUTURE-WORK FW-2, expanded)

1. Customer LHDN lifecycle webhooks (`invoice.valid` / `invoice.invalid`) deliver **only** via:  
   - `one.WebhookDeliveryOutboxes`  
   - `OutboundWebhookDispatcherJob`  
   - Platform signing (and temporary dual-sign only if locked)  
2. `WebhookSenderService` not used for those events (deleted or unreachable).  
3. Matching One endpoints exist for previously registered LHDN URLs (or product accepts hard cut with zero rows).  
4. Tests: event → outbox; filter; signature; no direct HTTP from Lhdn.  
5. Docs: freeze section removed; integrator contract honest; changelog if breaking.  
6. Staging (+ prod when ready) verified.  
7. `FUTURE-WORK.md` FW-2 marked done; F06 exit checked.

---

## 11. Open questions checklist (copy into F05 lock doc)

- [ ] **S:** Hard cut One signing vs dual-sign window end date: ________  
- [ ] **P:** Envelope P-A / P-B / P-C: ________  
- [ ] **R:** Empty `EnabledEvents` includes invoice.* ? yes/no ________  
- [ ] **R:** Migrate registry vs keep Lhdn table: ________  
- [ ] **R:** Keep `/lhdn/webhooks` as One façade how long? ________  
- [ ] **Events:** MVP only valid/invalid? Add submitted/cancelled? ________  
- [ ] **Secrets:** Preserve on migrate vs force remint? ________  
- [ ] **Breaking notice channel:** changelog / email / SDK major: ________  
- [ ] **Prod active LHDN webhook count:** ________ (date ________)  
- [ ] **If cannot share One signing:** re-open 00.2? yes/no ________  

---

## 12. Appendix — code anchors (current)

### One signature header construction

```90:102:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
                var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var signature = OutboundWebhookSignature.ComputeHeaderValue(
                    endpoint.SecretKey,
                    delivery.Payload,
                    unixTs);

                request.Headers.TryAddWithoutValidation("X-Lazuar-Signature", signature);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Event", delivery.EventType);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Delivery-Id", delivery.Id.ToString());
                request.Headers.TryAddWithoutValidation("X-Lazuar-Webhook-Id", endpoint.Id.ToString());
```

### Lhdn body-only HMAC

```38:44:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var secretBytes = Encoding.UTF8.GetBytes(subscription.Secret);

            var signature = Convert.ToHexString(HMACSHA256.HashData(secretBytes, payloadBytes)).ToLowerInvariant();

            request.Headers.Add("X-Lazuar-Signature", signature);
```

### Decision 00.2 (locked)

```39:55:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md
## 00.2 Outbound webhooks

- **Platform model:** One `WebhookDeliveryOutbox` + dispatcher job + signing is the only platform-grade webhook system.
- **LHDN end-state (Phase 04):** **A** — route LHDN lifecycle customer webhooks through One dispatcher.
- **Rejected for this track:** **B** — second full Lhdn outbox/signing stack.
- **Interim (until A ships):** **C freeze** — fire-and-forget remains; document debt; observability only; no second stack “improvements.”
...
If product later discovers LHDN payloads/signing cannot share One without breaking integrators, **re-open 00.2** and choose B with an ADR — do not silently fork.
```

### Outbound event contract (fan-out)

```7:21:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs
/// <summary>
/// Requests durable outbound delivery to customer webhook endpoints for an organization.
/// When <see cref="TargetUrl"/> is null/empty, One fans out to all active workspace endpoints
/// (filtered by each endpoint's enabled_events). A non-empty TargetUrl is reserved for
/// optional future per-URL routing and is not used as a silent equality gate.
/// </summary>
public record OutboundWebhookRequestedIntegrationEvent(
    Guid OrganizationId,
    string? TargetUrl,
    string EventType,
    JsonElement Payload) : IIntegrationEvent
```

---

## 13. Analysis exit (this document)

| Deliverable | Status |
|-------------|--------|
| Current dual paths | Documented §2 |
| Signing differences + dual-verify | Documented §3 |
| Product decision blockers | Documented §4 |
| Design options (A1–A4, dual-verify) | Documented §5 |
| File-level change plan | Documented §6 |
| Tests | Documented §7 |
| Risks | Documented §8 |
| PR sequence | Documented §9 |
| App code changes | **None** (analysis only) |

**Next action for humans:** run F05 product lock (PR-0). Do not start F06 enqueue code until answers in §11 are written and committed.
