# Webhook convergence decisions — LHDN → One (R40 lock)

**Status:** LOCKED (product/engineering defaults)  
**Date:** 2026-08-09  
**Phase:** R40 — docs only; **no dispatcher / enqueue app code**  
**Branch:** `chore/remaining-005`  
**Supersedes interim guessing for:** R41–R43, FW-2 / F05–F06  
**Sources:**  
- `plans/004-maintenance/decisions.md` §00.2  
- `plans/005-remaining/wave-decisions.md` (R40 seed defaults)  
- `plans/005-remaining/02-lhdn-webhooks-one-dispatcher.md` (analysis HOW)  
**Checklist:** `plans/005-remaining/checklists/r40-webhooks-product-lock.md`

---

## 0. Executive lock

| Topic | Locked choice |
|-------|----------------|
| **End-state** | **A** — LHDN customer lifecycle webhooks route through **One** durable dispatcher |
| **Rejected** | **B** — second Lhdn outbox / signing / dispatcher stack |
| **Interim (until R42/R43 ship)** | **C freeze** remains — fire-and-forget + observability only; no second-stack “improvements” |
| **Design** | **A1** — Lhdn publishes `OutboundWebhookRequestedIntegrationEvent` |
| **Signing end-state** | One Standard Webhooks–style **`t=,v1=`** only |
| **Signing migration** | **Dual-verify window if prod LHDN subscription rows exist**; hard cut OK if prod active count is 0 |
| **Payload** | Platform envelope wrapping LHDN **`data`** (P-B) |
| **Routing / registry** | Migrate to `one.TenantWebhookEndpoints`; filter via `EnabledEvents`; **empty = all** (current code) |
| **Events (MVP)** | `invoice.valid`, `invoice.invalid` only |
| **Breaking notice** | **Yes** when signing and/or top-level payload shape change |
| **Staging/prod row counts** | **Pending ops** (same gate pattern as keys R04) |

Do **not** silently fork a durable path in Lhdn. If product later cannot share One signing without an unacceptable permanent dual scheme, **re-open** `decisions.md` §00.2 + ADR for B — do not invent B under freeze pressure.

---

## 1. Inventory refresh (live code, 2026-08-09)

### 1.1 LHDN customer webhook events

| Event | Customer webhook today? | Source |
|-------|-------------------------|--------|
| `invoice.valid` | Yes | `DispatchExternalWebhookCommand` → `event = invoice.{status.ToLower()}` when status VALID |
| `invoice.invalid` | Yes | Same command path when status INVALID |
| `invoice.submitted` | No | Out of R40 MVP |
| `invoice.cancelled` | No | Out of R40 MVP |

**List API honesty gap:** `LhdnQueries` list hardcodes `["invoice.valid","invoice.invalid"]`; TypeSpec `events[]` on register is **not stored** on `WebhookSubscription` (no event filter column).

**Internal (not customer webhooks):** `LhdnDocumentValidatedIntegrationEvent` (Billing); no customer-facing invalid integration event today — INVALID only fires `DispatchExternalWebhookCommand`.

### 1.2 One platform signing (live)

| Aspect | Value |
|--------|--------|
| Helper | `Modules.One.Infrastructure.Workers.OutboundWebhookSignature` |
| Algorithm | HMAC-SHA256 |
| Signed material | `"{unixTimestamp}.{rawBody}"` |
| Header | `X-Lazuar-Signature: t={unix},v1={hmac_hex}` (lowercase hex) |
| Verify | `TryVerify` — parse `t`/`v1`, default skew **300s**, fixed-time hex compare |
| Extra headers | `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id` |
| Secret model | Platform-minted `whsec_…` on create; HMAC uses full string as UTF-8 key (prefix **not** stripped) |

Anchors:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`

### 1.3 LHDN signing (live, frozen)

| Aspect | Value |
|--------|--------|
| Helper | Inline in `WebhookSenderService` |
| Algorithm | HMAC-SHA256 of **body bytes only** |
| Header | `X-Lazuar-Signature: {hmac_hex}` (raw hex, **no** `t`/`v1`) |
| Retries | None (fire-and-forget; log + `RecordWebhookFailed("lhdn")`) |
| Extra headers | None |

Anchor:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs`

**Same header name, different semantics** — receivers that only implement One parse fail on LHDN deliveries and vice versa.

### 1.4 One platform event types published today (non-LHDN)

| Event type | Publisher area |
|------------|----------------|
| `subscription.activated` / `.suspended` / `.canceled` / `.resumed` | Commerce lifecycle |
| `subscription.past_due` | Commerce `BillingEngineJob` |
| `order.completed` | Commerce order completed |
| `payment_link.paid` | Commerce open checkout |
| `payment.completed` / `payment.failed` | Payments integration checkout |

Contract: `Modules.Commerce.Contracts.Events.OutboundWebhookRequestedIntegrationEvent`  
(`OrganizationId`, `TargetUrl?`, `EventType`, `Payload` JsonElement). Fan-out when `TargetUrl` is null.

### 1.5 Payload shapes (live)

**One envelope** (`OutboundWebhookEventHandlers`):

```json
{
  "id": "<uuid v7>",
  "event_type": "<EventType>",
  "created_at": "<ISO>",
  "data": { }
}
```

**LHDN wire body** (`DispatchExternalWebhookCommandHandler`):

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

Notes: LHDN top-level key is **`event`**; One uses **`event_type`**. Compliance-relevant fields live under `data.*`.

### 1.6 Registry / AcceptsEvent (live)

| Path | Table | Event filter |
|------|-------|--------------|
| One | `one.TenantWebhookEndpoints` | `EnabledEvents`; **empty = accept all** (`TenantWebhookEndpoint.AcceptsEvent`) |
| Lhdn | `lhdn.WebhookSubscriptions` | No filter column — all active URLs get all LHDN invoice events |

Tests already assert empty filter accepts `invoice.valid` (`OutboundWebhookTests.AcceptsEvent_Empty_Means_All`).

### 1.7 Staging / prod row counts

| Env | LHDN active webhook rows | Distinct orgs | One active endpoints | Status |
|-----|--------------------------|---------------|----------------------|--------|
| Local | Not run this phase | — | — | Optional; not a substitute for staging/prod |
| Staging | **Pending ops** | **Pending ops** | **Pending ops** | Blocked like keys R04 |
| Prod | **Pending ops** | **Pending ops** | **Pending ops** | Blocked like keys R04 |

**Do not invent counts.** Ops must run before choosing hard-cut vs dual-sign duration:

```sql
-- LHDN active customer webhooks
SELECT COUNT(*) FROM lhdn."WebhookSubscriptions" WHERE "IsActive" = true;

-- Distinct orgs still on LHDN registry
SELECT COUNT(DISTINCT "OrganizationId")
FROM lhdn."WebhookSubscriptions"
WHERE "IsActive" = true;

-- One endpoints already present (dual-registration risk)
SELECT COUNT(*) FROM one."TenantWebhookEndpoints" WHERE "IsActive" = true;
```

Paste results into R41 notes / ops log when available. **R41 backfill** and **dual-sign window length** consume these numbers.

---

## 2. Product locks (authoritative answers)

### 2.1 End-state and rejects

| ID | Lock |
|----|------|
| **End-state A** | LHDN lifecycle customer webhooks deliver **only** via One: `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` + platform signing/headers/retries |
| **Reject B** | No Lhdn outbox table, no second hosted dispatcher, no permanent second signing stack |
| **C freeze until cutover** | Keep fire-and-forget until R42 switches path; do not “half-upgrade” sender into durable |

Reinforces `decisions.md` §00.2; R40 does not reopen 00.2.

### 2.2 Signing (**S-C with conditional hard cut**)

| Item | Lock |
|------|------|
| **End-state** | One only: `X-Lazuar-Signature: t=<unix>,v1=<hex>` over `{t}.{body}` |
| **Hard cut (S-B)** | Allowed when **prod** active LHDN subscription count is **0** (or all known integrators already dual-verify) |
| **Dual-verify window (S-C)** | **Required if prod LHDN subs exist** — integrators accept either One `t=,v1=` **or** legacy body-hex until dated end |
| **Platform dual-sign (optional assist)** | Prefer **D1**: keep canonical `X-Lazuar-Signature` = One format; optional temporary `X-Lazuar-Signature-Legacy` = body-only HMAC hex on One dispatcher under config flag. **Not** D3 (two POSTs). Remove legacy after window. |
| **Window end date** | Set in R42 docs/config after ops paste prod count; target **30–60 days** from dual-sign enable if rows > 0; if rows = 0, skip dual-sign code path |
| **Permanent body-hex (S-A)** | **Rejected** as platform default |

Receiver dual-verify recipe (docs/SDK during window):

```text
function verify(secret, rawBody, signatureHeader):
  if header looks like t=…,v1=…:
    return StandardWebhooksVerify(secret, rawBody, header)
  else:
    return ConstantTimeEq(hex(hmac_sha256(secret, rawBody)), header)
```

### 2.3 Payload (**P-B**)

| Item | Lock |
|------|------|
| **Wire body** | Platform envelope: `{ id, event_type, created_at, data }` |
| **`event_type`** | `invoice.valid` \| `invoice.invalid` |
| **`data` fields (stable)** | `internal_id`, `lhdn_uuid`, `status`, `qr_link`, `error_message` — same names as today’s LHDN `data` object |
| **`timestamp` in data** | Prefer envelope `created_at`; do not require a second timestamp in `data` for new integrators (may include for transitional parity if R42 tests need it — not a long-term dual-body) |
| **P-A (keep LHDN top-level `event` only)** | Rejected as end-state |
| **P-C (versioned dual body)** | Not default; only if a named major ERP cannot change — requires reopen of this doc |

**Breaking:** parsers of top-level `event` (vs `event_type` + envelope) must update. `data.*` field names stay stable to minimize compliance/ERP mapping churn.

Example end-state body:

```json
{
  "id": "<delivery-or-fanout-uuid>",
  "event_type": "invoice.valid",
  "created_at": "2026-08-09T12:00:00Z",
  "data": {
    "internal_id": "...",
    "lhdn_uuid": "...",
    "status": "VALID",
    "qr_link": "https://…",
    "error_message": null
  }
}
```

### 2.4 Routing / registry (**R-D + EnabledEvents; empty = all**)

| Item | Lock |
|------|------|
| **Registry end-state** | **R-D** — migrate LHDN URLs/secrets into `one.TenantWebhookEndpoints`; Lhdn table is not long-term config SSoT |
| **Backfill (R41)** | Active `lhdn.WebhookSubscriptions` → One endpoints |
| **EnabledEvents on migrated LHDN-only URLs** | **`["invoice.valid", "invoice.invalid"]`** — **not** empty — avoids accidental commerce/payment fan-out to e-invoice-only receivers |
| **Empty `EnabledEvents` semantics** | **Unchanged:** empty means **all platform events including `invoice.*`** (current `AcceptsEvent`) |
| **R-C (Lhdn table remains source of truth)** | Rejected as end-state |
| **Secrets on migrate** | **Preserve** Lhdn `Secret` → One `SecretKey` (no remint) so dual-verify works without customer remint |
| **New One endpoints** | Continue platform mint `whsec_…` |
| **URL validation** | Apply `WebhookUrlValidator` on new writes; quarantine invalid URLs on backfill (log + skip + ops list) |
| **`/lhdn/webhooks` API** | Short dual-write or façade after R41, then deprecate or façade over One (R43 / follow-on); not blocked for R40 |

**Integrator nuance (locked):** one Zapier URL with empty filter will receive **commerce + invoice** after convergence. LHDN-only URLs must be migrated with explicit invoice event list. Workspaces that want both should either use empty filter or list all desired events explicitly.

### 2.5 Design choice (**A1**)

| Item | Lock |
|------|------|
| **A1** | **Chosen** — Lhdn publishes `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl: null` (fan-out), `EventType: invoice.valid|invoice.invalid`, `Payload` = LHDN data object (snake_case JsonElement) |
| **A2** | Not chosen — would need `LhdnDocumentInvalidatedIntegrationEvent` + One→Lhdn contracts coupling for invalid path |
| **A3** | Rejected — Lhdn writing One outbox internals |
| **A4** | Avoid by default — dual delivery only as short staging safety if ops requires; never leave dual fire-and-forget + One on in prod |

**Template:** Payments already references `Modules.Commerce.Contracts` and publishes the same event — Lhdn mirrors that pattern.

**Enqueue mapping (R42):**

```text
VALID  → EventType invoice.valid,  data.status = VALID
INVALID → EventType invoice.invalid, data.status = INVALID (+ error_message)
```

Same deploy that enables publish **removes** `IWebhookSenderService` call for those events (no double POST). Prefer R41 backfill before or with R42 when prod LHDN rows > 0.

### 2.6 Event catalog (MVP)

| Event | In MVP? |
|-------|---------|
| `invoice.valid` | **Yes** |
| `invoice.invalid` | **Yes** |
| `invoice.submitted` / `invoice.cancelled` / credit-note variants | **No** unless product expands later |

Honesty fix (R41/R43, not R40 code): TypeSpec `events[]` must map to One `EnabledEvents` or be removed from contract.

### 2.7 Breaking notice

| Item | Lock |
|------|------|
| **Required?** | **Yes** if signing scheme and/or top-level payload envelope change (they will for any integrator still on LHDN fire-and-forget) |
| **Channels** | Changelog entry; `apps/lazuar-docs/docs/integrations/webhooks.md` (invoice events + dual-verify recipe if window active); LHDN SDK / hub notes; email only if ops identifies named high-touch integrators from prod row query |
| **If zero prod LHDN rows** | Still document platform contract; no customer migration campaign required |

---

## 3. Dual-sign decision tree (ops after row counts)

```text
prod active lhdn.WebhookSubscriptions count
  │
  ├─ 0  → Hard cut to One t=,v1= (S-B); docs dual-verify optional for future-proof receivers
  │       R41 backfill may be no-op; still run count verify
  │
  └─ >0 → S-C dual-verify window
          Prefer D1 legacy header on One dispatcher for 30–60 days
          Preserve secrets on R41 backfill
          Dated end → remove legacy header + docs
```

Staging follows the same tree independently (staging count does not unlock prod hard cut).

---

## 4. Phase mapping (005 remaining)

| Phase | Role | Depends on this doc |
|-------|------|---------------------|
| **R40** | Product lock (this artifact) | — |
| **R41** | Registry backfill Lhdn → One; `EnabledEvents` invoice.*; preserve secrets | Locks §2.4, §2.7 |
| **R42** | A1 enqueue publish; payload P-B; dual-sign D1 if §3 says so | Locks §2.2–2.5 |
| **R43** | Retire fire-and-forget; README/docs honesty; façade/deprecation | All locks |

Serial rule (hard): **R40 → R41 → R42 → R43** (`wave-decisions.md`).

---

## 5. Explicit non-goals (R40 and convergence track)

- No app code in R40  
- No second Lhdn durable stack (B)  
- No permanent body-hex platform default  
- No `Modules/Webhooks` extract (00.2 / 00.6 — stay in One)  
- No inventing staging/prod subscription counts from this workstation  
- No silent URL equality gate between product URLs and webhook endpoints  
- Frontend Ops LHDN webhook UI not required (One UI already covers durable path)

---

## 6. Open items for ops (not blockers for R41 design)

| Item | Owner | Notes |
|------|-------|-------|
| Staging row counts (SQL §1.7) | Ops | Paste into R41 notes |
| Prod row counts | Ops | Gates dual-sign vs hard cut |
| Dual-sign end calendar date | Eng + Ops after prod count | Write into R42 notes / appsettings flag docs |
| Named integrator notify list | Ops / CS | Only if prod count > 0 |

---

## 7. Exit criteria (R40)

- [x] Inventory refreshed from live signing + event names (§1)  
- [x] S / P / R / events / design / reject-B written (§2)  
- [x] Staging/prod counts marked pending ops, not invented (§1.7)  
- [x] Artifact at `plans/005-remaining/webhook-convergence-decisions.md`  
- [x] R41 unblocked for design/backfill implementation  

**Next:** R41 registry backfill (no dispatcher rewrite required for fan-out if endpoints land with correct `EnabledEvents`).

---

*R40 complete as docs-only product lock. Implementation starts at R41.*
