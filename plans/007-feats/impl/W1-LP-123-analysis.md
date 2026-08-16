# W1-LP-123 — PDPA delete / anonymize (finish the existing CRM path)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-123`. Tracker: *PDPA buyer-data deletion / anonymize* — Lazuar **P**.  
**Not this ID:** merchant-account erasure / delete-workspace (controller of **founder** email) is later. Do not grow CRM into HubSpot (`LP-168` refuse). Do not add a public buyer self-serve “delete my data” portal.

**Invariant:** When a merchant (or privacy@ forwarding) runs anonymize for a tenant `ClientProfile`, PII is wiped in CRM, mail cannot restart, commerce access ends, and the action is reachable without SQL.

---

## 0. Scope lock

In scope:

- Existing `AnonymizeClientProfileCommand` + outbox event
- Existing Commerce cancel + Communications suppress consumers
- One OrgAdmin HTTP trigger + one Ops button
- Wipe denormalized buyer PII on **Commerce transaction logs** for that profile’s email
- Tests that the fan-out actually persists

Out of scope:

- Buyer-facing rights portal
- Deleting LHDN / tax documents (legal retention)
- Deleting ledger amounts (keep money truth; drop names if present)
- Marketing consent UI
- Cross-tenant “global user” wipe (One `GlobalUser` is the merchant, not the buyer)

---

## 1. Verdict

The **domain path is real**. The **product loop is not**.

| Step | Status |
|------|--------|
| `ClientProfile.Anonymize()` → `deleted_{id}@localhost` | **Y** |
| Command publishes `ClientProfileAnonymizedIntegrationEvent` then `SaveChanges` | **Y** |
| CRM outbox / inbox jobs registered | **Y** (Aug-3 “stuck forever” is stale) |
| Commerce: cancel non-`CANCELED` subs + `SubscriptionCanceled` | **Y** |
| Communications: suppress pre-wipe email as `ANONYMIZED` | **Y** |
| HTTP / TypeSpec / Ops button | **N** |
| Wipe `TransactionLogs.CustomerName/Email` | **N** |
| Handler integration tests | **N** (only domain unit tests) |

Privacy copy (`apps/lazuar-portal/src/app/legal/privacy/page.tsx`) tells buyers to email the Creator or `privacy@lazuar.com`. Staff still cannot run the command without a debugger.

---

## 2. Current files

### 2.1 CRM (writer)

| Path | Role |
|------|------|
| `Modules/CRM/Domain/ClientProfileEntity.cs` | `Anonymize()` wipes name/email/phone/TIN/ids/address/consent/`GlobalUserId` |
| `Modules/CRM/Contracts/AnonymizeClientProfileCommand.cs` | `(OrganizationId, ClientProfileId)` |
| `Modules/CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` | Load org-bound → capture email/phone → anonymize → publish → save |
| `Modules/CRM/Contracts/ClientProfileAnonymizedIntegrationEvent.cs` | Pre-wipe email/phone |
| `Modules/CRM/README.md` | **No HTTP**; other modules use `ICrmQueryService` |
| `packages/api-spec/modules/crm/models.tsp` | Models only; **no routes** (comment says do not add unless CRM gains an edge) |

CRM is a documented 3-layer exception (no Application project). A thin HTTP edge in Infrastructure is allowed if architecture tests still treat CRM as `ModulesWithoutApplication`. Prefer **Commerce admin** wrapping the command so CRM stays HTTP-less — see §4.

### 2.2 Downstream consumers

| Path | Role |
|------|------|
| `Modules/Commerce/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | Cancel ACTIVE/PAST_DUE/SUSPENDED/TRIALING/PENDING (and any other non-canceled); publish `SubscriptionCanceled` |
| `Modules/Communications/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | `SuppressAsync(…, "ANONYMIZED", "gdpr_client_profile_anonymized")`; skip `deleted_*@localhost` |
| `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | `IsSuppressedAsync` skips **all** email (see LP-154 split) |

### 2.3 Callers today

**None.** Grep: only the handler class and tests. Inventory: BACKEND-ONLY.

### 2.4 Tests today

`tests/Lazuar.ModuleTests/CRM/ClientProfileAnonymizedEventTests.cs`: event shape + `Anonymize()` wipe + consent default. **No** handler, **no** HTTP, **no** cancel/suppress.

### 2.5 Ops surface for the button

`SubscribersPage.tsx` already has a detail side panel (`selectedSub` includes `client_profile_id` on `CommerceSubscriptionDto`). That is the only merchant place a human can identify a buyer.

---

## 3. Gaps

### G1 — No caller (P0)

Command cannot be run from Ops or API.

### G2 — Transaction log still holds the email

`CommerceTransactionLog` denormalizes `CustomerName` / `CustomerEmail`. After anonymize, list/export (LP-097) still show the person. PDPA wipe is incomplete.

Checkout sessions / custom quote rows may also store email — wipe if a column exists; do not invent a new PII catalog.

### G3 — No proof the outbox hop lands

Same class of bug as Wave 0 unpublished Commerce second-hop: consumer tests must use a real bus + `SaveChanges`, not only `profile.Anonymize()`.

### G4 — Idempotency

Second anonymize: profile already dummy; event would re-publish with `deleted_*@localhost`; comms handler already no-ops that email. Commerce: no subs left. Safe. Command should **200 no-op** if already anonymized (`Email` starts with `deleted_`) instead of throwing.

**Not gaps**

| Observation | Why not |
|-------------|---------|
| LHDN XML still has TIN | Retention; do not delete tax docs |
| Ledger amounts remain | Money truth |
| `TRIALING` in cancel list | Dead vocabulary; harmless |
| Buyer cannot self-delete | By design this wave |

---

## 4. Minimal changes

**Design lock:** keep CRM HTTP-less. Commerce admin already has the subscriber and the profile id.

### 4.1 Must

| File | Change |
|------|--------|
| `AnonymizeClientProfileCommandHandler` | If already anonymized (`Email` like `deleted_%@localhost`), return without publishing. |
| New `AnonymizeSubscriberCommand` (Commerce Application) **or** SubscriberEndpoints calling CRM command + a Commerce “scrub logs” command | OrgAdmin: resolve `client_profile_id` from path **or** from subscription id; `Send(AnonymizeClientProfileCommand)`; then `UPDATE` transaction logs for that org + old email → `Anonymized User` / `deleted_{profileId}@localhost`. Prefer **subscription id** as the ops handle so the button does not invent a CRM route. |
| `SubscriberEndpoints.cs` | `POST /admin/commerce/subscribers/{id}/anonymize` → 200 `{ status: "anonymized" }`; 404 unknown sub; tenant-bound. |
| TypeSpec `admin-routes.tsp` | Document the POST. |
| `SubscribersPage.tsx` | Destructive confirm (“This cannot be undone. Subscriptions cancel. Emails stop.”) then POST. |

Do not add `/admin/crm/*` unless architecture tests force it.

### 4.2 Should

- After cancel, existing `SubscriptionCanceled` outbound webhook still fires (already). Do not add a new event type.
- Privacy page one line: “Creators can anonymize a buyer from Subscribers → Anonymize.”

### 4.3 Do not

- Public unauthenticated delete-by-email.
- Hard DELETE of the profile row (breaks FKs).
- Wipe LHDN documents.
- Mint a buyer JWT to self-serve this.

---

## 5. Tests

### 5.1 Command

Extend CRM tests + new Commerce handler test:

| Case | Expect |
|------|--------|
| Happy path | Email `deleted_{id}@localhost`; TIN null; event has **pre-wipe** email |
| Already anonymized | No second event |
| Wrong org | Throw / 404 |
| Commerce consumer | Subs for that profile `CANCELED`; other profiles untouched |
| Comms consumer | `SuppressionEntries` reason `ANONYMIZED` |
| Transaction logs | Name/email scrubbed for that org+old email |

Use real `CrmDbContext` + `OutboxEventBus` if other CRM tests do; otherwise send the command through MediatR in the module fixture.

### 5.2 Endpoint (optional if no WebApplicationFactory)

- OrgAdmin + tenant header → 200  
- Missing sub → 404  
- API_CLIENT without a new scope → 401/403 (route stays OrgAdmin)

### 5.3 Manual

1. Checkout as buyer@example.com  
2. Ops → subscriber → Anonymize  
3. Profile dummy; sub CANCELED; send test receipt to that address skipped; transaction table shows anonymized  

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Unpublished CRM outbox | Jobs are registered; test SaveChanges |
| Cancel fires access-revoke webhooks | Correct for PDPA |
| Bookkeeper CSV loses names | Required; amounts stay |
| Staff clicks wrong row | Confirm modal + show email |

---

## 7. Acceptance

1. Ops can anonymize a subscriber without SQL.  
2. CRM PII is dummy; marketing consent false.  
3. All that profile’s commerce subscriptions are `CANCELED` (or already were).  
4. Pre-wipe email is suppressed (`ANONYMIZED`).  
5. Transaction log PII for that buyer is gone.  
6. Second click is a no-op.  
7. LHDN / ledger money rows remain.  
8. Tests §5.1 pass.  
9. Tracker **P → Y**.

---

## 8. Implement order

1. Idempotent command + log scrub  
2. POST + TypeSpec + Ops button  
3. Fan-out tests  
