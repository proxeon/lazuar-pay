<!-- Source subagent: 019fc650-3513-7032-806d-65ba892ac832 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Ops, CRM & Messaging Modules Gap Analysis

Deep read of module sources under `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/{Ops,CRM,Messaging}/`, wiring in `Program.cs`, TypeSpec specs, building-block buses/workers, and cross-module consumers (Commerce, Communications, One, Billing, ops-page).

---

## Ops Module Inventory & Gaps

### Stated purpose
**Internal operations agent** (“Lazuar Ops”): tenant-scoped chat, LLM tool-calling over other modules’ `[AgentTool]` commands/queries, human-in-the-loop write approval, UI form collection for missing params, conversation persistence.

### Layer inventory

| Layer | Contents | Notes |
|--------|-----------|--------|
| **Domain** | `OpsConversation`, `OpsMessage` | Soft-delete conversations; messages store tool/UI JSON blobs |
| **Application** | `IOpsRepository`, `IToolRegistry`/`ToolRegistry`, `ILlmOrchestratorService`, commands: rename/delete/request-form | `DependencyInjection` is empty marker |
| **Contracts** | **Empty** (csproj only) | No public integration surface for other modules |
| **Infrastructure** | Endpoints, `OpsDbContext`, repo, LLM services (partials), inbox/outbox jobs | Schema `ops` |
| **Frontend** | `apps/ops-page` consumes stream, conversations, execute-action, resolve | Primary consumer |

### Domain model
- **`OpsConversation`**: `Id`, `OrganizationId`, `Title`, soft-delete (`IsDeleted`/`DeletedAt`), timestamps. Query filter excludes deleted.
- **`OpsMessage`**: `Role` (`user`/`assistant`/`system`), `Content`, `ExecutedToolsJson`, `ProposedActionJson`, `UiRequestJson`, `IsResolved`. No FK to conversation in EF config (orphans possible).

### Capabilities present
1. List conversations (paginated shell; **total always 0**).
2. Get messages for conversation.
3. Non-stream chat (`POST /ops/chat`) — **incomplete agent loop**.
4. SSE stream chat (`POST /ops/chat/stream`) — full tool loop, UI request, proposed action, persistence.
5. System message inject, rename, soft-delete conversation.
6. Resolve UI request flag on message.
7. Execute approved write tool (`POST /ops/execute-action`) with memory-cache idempotency + injects `OrganizationId`/`RecordedBy` + `IsAgentAction`.
8. Reflection-based **ToolRegistry** discovers `[AgentTool]` types across loaded assemblies.
9. Module prompt injection via `IAgentPromptProvider` (e.g. Billing).
10. Token usage logging (FinOps log only; no billing of tokens).

### Registered tools (platform-wide, not Ops-owned)

| Tool | Module | Type |
|------|--------|------|
| `RequestFormInputCommand` | Ops | Special (UI form) |
| One: workspace details, members, entitlements, invite, remove member, toggle app | One | R/W |
| `GetFinancialHealthAgentQuery` | Billing | Read |
| `GetPaymentConfigAgentQuery` | Payments | Read |
| `ListLhdnSubmissionsAgentQuery` | Lhdn | Read |

**Missing tools for product surface**: Commerce (products/subscribers/orders), Communications (broadcasts/templates), CRM (customer lookup), Messaging (none expected). System prompt mentions bulk broadcasts and dedicated batch tools, but **Commerce/Communications have zero `[AgentTool]`**.

### Workers
- `OpsInboxConsumerJob`, `OpsOutboxPublisherJob` registered.
- `UseOpsSubscriptions()` is a **no-op** — no events consumed/published.
- Inbox/outbox tables exist but are **dead infrastructure** unless future events are wired.

### Schema (`ops`)
- `Conversations`, `Messages`, `InboxMessages`, `OutboxMessages`
- Indexes on org / (org, conversation); soft-delete filter on conversations only (messages of deleted convs still load if you know the id).

### Gaps (Ops) — prioritized

| Priority | Gap | Detail |
|----------|-----|--------|
| P0 | **Role elevation** | `BuildChatOptions` hardcodes `"SUPER_ADMIN"` for tool filtering; endpoint only requires `CLIENT` or `ADMIN`. Any authorized role gets SUPER_ADMIN tool set. |
| P0 | **Non-stream path incomplete** | `ProcessChatAsync` single-shot completion, no tool execution, no conversation create/persist of full agent turn. Spec still exposes `POST /ops/chat`. |
| P0 | **Idempotency not durable** | `IMemoryCache` 5-min keys — multi-instance / restart unsafe for write actions. |
| P1 | **Pagination total always 0** | `PaginatedResponse(..., 0, ...)` — UI cannot know total pages. |
| P1 | **Spec drift** | OpenAPI routes omit stream, system-message; models omit some runtime fields. |
| P1 | **Tool coverage vs product** | Ops UI modules (commerce products/subscribers/campaigns/templates) have no agent tools; prompt rules reference tools that do not exist. |
| P1 | **No FinOps chargeback** | Tokens logged only; not tied to Billing credits. |
| P2 | **Empty Contracts** | Cannot cleanly expose Ops query surface if needed. |
| P2 | **Dead inbox/outbox** | Workers burn poll cycles for unused tables. |
| P2 | **No message↔conversation integrity** | No FK/cascade; delete conversation soft-deletes only conversation. |
| P2 | **Failure recovery UX** | On tool JSON failures, stop after 3; stream errors yield text then break — OK but no retry semantics. |
| P2 | **Tests** | Only unit tests for `ExecuteReadToolAsync` reflection path; no stream/integration/security tests. |
| P3 | **Reasoning effort `xhigh`** always — cost/latency not configurable per request. |

---

## CRM Module Inventory & Gaps

### Stated purpose (README)
**PII registry**: tenant-scoped `ClientProfile`, sync from One global user updates, GDPR/PDPA anonymization + broadcast, `ICrmQueryService` for cross-module reads. Explicitly **not** leads/deals/tickets/messaging/access control.

### Layer inventory

| Layer | Contents |
|--------|-----------|
| **Domain** | `ClientProfileEntity` (anemic public setters), `BillingAddress` value object |
| **Application** | **Missing** — no Application project |
| **Contracts** | Create/Resolve/Anonymize commands, `ClientProfileAnonymizedIntegrationEvent`, `ICrmQueryService` |
| **Infrastructure** | Handlers, `CrmQueryService`, config, one event handler, DbContext, migration | No endpoints, no workers |
| **README** | Present and accurate for intent |

### Domain fields
`Id`, `OrganizationId`, `GlobalUserId?`, `FullName`, `Email`, `Phone`, `Tin`, `IdType`, `IdValue`, `Address` (owned), `ConsentedToMarketing`.  
`Anonymize()` wipes PII to dummy values and clears global link/consent/tax ids.

### Commands / queries (actual)

| Contract | Handler | Callers |
|----------|---------|---------|
| `ResolveClientProfileCommand` | Yes | Commerce checkout / manual subscriber / custom checkout |
| `CreateClientProfileCommand` | Yes | **No callers** outside CRM |
| `AnonymizeClientProfileCommand` | Yes | **No callers**; no HTTP |
| `ICrmQueryService` | Yes | Commerce query services, Communications lifecycle/fulfillment, payment handler, tests |

### Events
- **Consumed**: `GlobalUserProfileUpdatedIntegrationEvent` → updates name/email on all profiles with matching `GlobalUserId` (**direct** handler; no inbox durability).
- **Published**: `ClientProfileAnonymizedIntegrationEvent` via keyed `CrmEventBus` (outbox row).

### Critical pipeline bug (CRM)
1. Anonymize writes to `crm.OutboxMessages` via `OutboxEventBus<CrmDbContext>`.
2. **No `CrmOutboxPublisherJob` and no `CrmInboxConsumerJob`** are registered in `AddCrmModule`.
3. **No module subscribes** to `ClientProfileAnonymizedIntegrationEvent`.
4. Result: GDPR anonymize **never fan-outs**; outbox rows **never leave CRM**; README claim about Community ban/cancel is **unimplemented**.

### Schema (`crm`)
- Unique index `(OrganizationId, Email, Phone)` — composite, not email-only.
- Resolve dedupes by **email only**; Create by **email OR phone**.
- Risk: same email + different phone can violate unique index or create inconsistent match behavior between Create vs Resolve.
- Inbox/Outbox tables present but **workers absent**.

### API / TypeSpec
- Models: `ClientProfileDto`, `CreateClientProfileRequestDto`, `UpdateClientProfileRequestDto`.
- **No CRM routes** in TypeSpec; **no `MapCrmEndpoints`**.
- OpenAPI DTOs are orphaned relative to HTTP.

### Consumers & boundary violations
- Proper: Commerce/Communications use `ICrmQueryService` / MediatR commands.
- Improper: Billing `GenerateDraftDocumentQueryHandler` does raw SQL `LEFT JOIN crm."ClientProfiles"` — **cross-schema join**, bypasses contracts and multi-DB future.

### Gaps (CRM)

| Priority | Gap | Detail |
|----------|-----|--------|
| P0 | **Anonymize event never published** | No outbox worker |
| P0 | **No anonymize consumers** | Commerce subscriptions/orders never react |
| P0 | **No public/admin API for privacy ops** | Anonymize only via internal MediatR if something called it |
| P1 | **Consent always forced true** | Create/Resolve set `ConsentedToMarketing = true`; DB default true; entity default false — PDPA/marketing compliance gap |
| P1 | **No Update/List/Search/Merge** | Spec has `UpdateClientProfileRequestDto` but zero implementation |
| P1 | **Create unused / Resolve-only path** | Two near-duplicate create flows; Create is dead code |
| P1 | **Global profile sync incomplete** | Syncs name/email only; not phone; no conflict policy if local staff-edited |
| P1 | **No Application layer / workers** | Diverges from modular standard (One/Commerce/etc.) |
| P2 | **Anemic domain** | Public setters; no invariants (email format, required phone, tin format) |
| P2 | **Malaysia-centric phone normalize** | Leading `0` → `60`; empty phone allowed |
| P2 | **Query service ignores tenant filter** | Uses `IgnoreQueryFilters` by design; OK for cross-tenant bulk by id but risky if misused |
| P2 | **No CRM tests** | Architecture lists CRM; no CRM unit/module tests |
| P3 | **Dead CRM→Messaging Application reference reverse?** | Messaging.Application references CRM.Contracts unused |

---

## Messaging Module Inventory & Gaps

### Stated purpose (README)
**Dispatch router / dumb pipe**: consume fully rendered `DispatchMessageIntegrationEvent`, route Email/WhatsApp, keep `TenantReplica`, strip HTML for SMS/WhatsApp. Not templates, not campaigns, context-blind.

### Layer inventory

| Layer | Contents |
|--------|-----------|
| **Domain** | `TenantReplica` only |
| **Application** | Tenant replica repository port, MediatR handlers for tenant/workspace events, `SendTenantNotificationCommand` |
| **Contracts** | `DispatchMessageIntegrationEvent` only |
| **Infrastructure** | Dispatch handler, inbox writers for One events, DbContext, workers, thin `/messaging/notify` endpoint |

### Core flow — dispatch
Publishers (via their outboxes → `InMemoryEventBus`):
- **Communications**: broadcast fan-out, lifecycle, document published, template test, dunning/reminder fulfillment
- **One**: password reset, email verify, workspace invite

`DispatchMessageIntegrationEventHandler` (direct `IIntegrationEventHandler`, **not** messaging inbox):
1. System-tenant detection (`Empty` or hard-coded `...0001`).
2. Channel flags: `EMAIL` / `WHATSAPP` / `ALL` (no SMS).
3. Email suppression via `ISuppressionService` (Communications).
4. WhatsApp credit pre-check + post-send deduct (Billing), skip if `CreditHoldId` set.
5. Email: optional BYOK from `ICommunicationsQueryService.GetEmailConfigAsync` + `EmailTemplateBuilder.WrapWithBrandHtml` + `IEmailService` (Resend).
6. WhatsApp: `IMessagingService.SendMessageAsync` — **registered as `ConsoleMessagingService`** (log only).

### Core flow — tenant replica
- Subscribe One: provisioned / updated / workspace updated → **write messaging inbox**.
- `MessagingInboxConsumerJob` → MediatR `INotification` → Application handlers upsert `TenantReplica`.
- `TenantProvisionedSeedingHandler` only logs (typo “Replia”).
- **Dispatch path never reads `TenantReplica`.** Only `SendTenantNotificationCommand` does (and passes **slug as messaging recipient** — nonsensical for phone/SMS).

### Workers
- `MessagingInboxConsumerJob` — used for tenant replica events.
- `MessagingOutboxPublisherJob` — registered; module publishes **nothing** (README admits terminal sink).

### Endpoints
- `POST /api/v1/messaging/notify` — **no auth**, accepts `SendTenantNotificationCommand` body, returns 202.
- TypeSpec messaging models intentionally blank; notify not in OpenAPI.

### Providers
| Channel | Implementation |
|---------|----------------|
| Email | Resend (BYOK or platform for system tenant only) |
| WhatsApp/SMS | Console logger only |

### Architecture boundary issues
- Messaging.Infrastructure references **`Modules.Communications.Application`** for `ICommunicationsQueryService` (lives in Application, not Contracts) — fails the spirit of architecture tests (“outer layers only through Contracts”).
- Coupling: suppression, email config, credit costs all inside dispatch handler — Messaging is no longer a pure “dumb pipe”; it is a **policy-aware delivery orchestrator**.

### Gaps (Messaging)

| Priority | Gap | Detail |
|----------|-----|--------|
| P0 | **No real WhatsApp/SMS provider** | Console only; product cannot deliver phone messages |
| P0 | **Unauthenticated notify endpoint** | Potential spam/abuse surface if exposed |
| P0 | **Delivery failures marked processed** | Outbox publisher always sets `ProcessedAt` even on handler exception — **lost dispatches** with no retry (platform-level, hits Messaging hard) |
| P1 | **No delivery log / audit entity** | Cannot answer “was this email sent?”; UI “Delivery Logs” is webhooks only (One) |
| P1 | **README vs code** | Claims HTML strip / SMS; neither implemented; channel has no SMS |
| P1 | **Lifecycle WhatsApp broken upstream** | Some Communications publishers pass `PlainTextPhoneBody: null` even with phone channel |
| P1 | **CreditHoldId misuse** | Broadcast fan-out passes `broadcast.Id` as `CreditHoldId` to skip wallet — semantic abuse, risk if hold release ever keys on it |
| P1 | **TenantReplica underused** | Maintained but unused by dispatch (active check, branding, rate limits unused) |
| P1 | **Dispatch not inbox-durable** | Runs inline on InMemory bus; crashes mid-handler = partial send + possible double-send on replay |
| P2 | **No MessageDelivered/Failed events** | Terminal sink cannot drive CRM suppressions from bounces (bounce webhooks may live elsewhere; not in Messaging) |
| P2 | **Dead CRM project reference** on Messaging.Application |
| P2 | **No Messaging tests** |
| P3 | **Outbox job for empty publish path** |

---

## Cross-Module Responsibilities Clarity

```text
┌─────────────┐   GlobalUserProfileUpdated    ┌─────────┐
│     One     │ ─────────────────────────────►│   CRM   │  PII registry
│  Identity   │   TenantProvisioned/Updated   └────┬────┘
└──────┬──────┘ ───────────────────────────┐       │ ICrmQueryService / Resolve*
       │                                   │       ▼
       │ DispatchMessage (system emails)   │  Commerce (orders/subs store ClientProfileId)
       ▼                                   ▼
┌─────────────┐ ◄── DispatchMessage ── ┌────────────────┐
│  Messaging  │                        │ Communications │ templates, broadcasts,
│  dispatch   │ ──► Resend / Console   │ suppression,   │ email BYOK config
└──────┬──────┘ ◄── credits / config ──│ email config   │
       │         Billing + Comm        └────────────────┘
       │
┌──────▼──────┐   Agent tools (MediatR) across modules
│     Ops     │ ◄── IOneQueryService (apps), IAgentPromptProvider
│  LLM agent  │ ──► execute-action (writes)
└─────────────┘
```

### Clear boundaries (good)
| Module | Owns | Does not own |
|--------|------|--------------|
| **CRM** | Customer PII, resolve/create, anonymize intent | Access control, messaging, subscriptions |
| **Messaging** | Physical send + channel routing | Templates, segments, campaign logic |
| **Communications** | Templates, broadcast, suppression, tenant email config | Customer master data, chat agent |
| **Ops** | Agent UX + conversation memory + HITL execution | Business data ownership |

### Ambiguities / leaks

| Issue | Why it matters |
|-------|----------------|
| **Messaging policy brain** | Credit, suppression, BYOK inside Messaging blurs “dumb pipe” vs Communications |
| **CRM has no UX surface** | Customers only appear via Commerce/Communications; “CRM module” is invisible in product nav |
| **Ops vs domain modules** | Agent is the only “ops” API; product admin UI is mostly Commerce/One endpoints, not Ops |
| **Billing joins CRM schema** | Violates modular DB isolation claimed by CRM README |
| **PII in multiple places** | GlobalUser (One) + ClientProfile (CRM) + possibly order snapshots — sync is partial |
| **Anonymize story incomplete** | Documented end-to-end path does not exist |
| **Communications vs Messaging naming** | Easy to confuse “message templates” with “message dispatch” |

### Data ownership of a “customer”
1. Identity login → One `GlobalUser`
2. Checkout creates/links → CRM `ClientProfile` (+ optional `GlobalUserId`)
3. Commerce holds `ClientProfileId` on order/subscription
4. Communications resolves profile for template variables
5. Messaging never stores customer rows (correct)

---

## Endpoints & Contracts

### Ops HTTP (`/api/v1/ops`, roles `CLIENT`|`ADMIN`)

| Method | Path | Status |
|--------|------|--------|
| GET | `/chat/conversations` | Implemented; total count broken |
| GET | `/chat/conversations/{id}/messages` | OK |
| POST | `/chat` | Spec + code; **agent incomplete** |
| POST | `/chat/stream` | Implemented; **missing from OpenAPI routes** |
| POST | `/chat/conversations/{id}/system-message` | Implemented; **not in OpenAPI** |
| PUT | `/chat/conversations/{id}/title` | OK |
| DELETE | `/chat/conversations/{id}` | Soft-delete OK |
| PUT | `/chat/messages/{id}/resolve` | OK |
| POST | `/execute-action` | OK (idempotency weak) |

**Contracts project**: empty — all DTOs from shared `Lazuar.ApiTypes`.

### CRM HTTP
**None.**

| Contract (MediatR / DI) | Status |
|-------------------------|--------|
| `ResolveClientProfileCommand` | Live |
| `CreateClientProfileCommand` | Dead |
| `AnonymizeClientProfileCommand` | Dead end (no workers/consumers) |
| `ICrmQueryService` | Live |
| `ClientProfileAnonymizedIntegrationEvent` | Written to outbox only |
| TypeSpec Create/Update DTOs | Spec-only |

### Messaging HTTP

| Method | Path | Auth | Status |
|--------|------|------|--------|
| POST | `/messaging/notify` | **None** | Dev/debug-ish; wrong semantics |

| Contract | Status |
|----------|--------|
| `DispatchMessageIntegrationEvent` | Live, primary API of module |
| Tenant replica events (One contracts) | Live via inbox |

---

## Workers

| Worker | Module | Registered | Productive? |
|--------|--------|------------|-------------|
| `OpsInboxConsumerJob` | Ops | Yes | No events |
| `OpsOutboxPublisherJob` | Ops | Yes | Nothing published |
| **CRM inbox/outbox jobs** | CRM | **No** | Tables idle; **anonymize stuck** |
| `MessagingInboxConsumerJob` | Messaging | Yes | Tenant replica only |
| `MessagingOutboxPublisherJob` | Messaging | Yes | Nothing published |
| (Upstream) Communications/One outbox publishers | Other | Yes | Drive `DispatchMessage` into Messaging handler |

### Platform job semantics affecting these modules
- Inbox/outbox jobs **always mark messages processed** even on failure (poison-message protection) → **at-most-once with drop on error**, not at-least-once with retry.
- For email/WhatsApp, that is a **reliability gap**.

---

## Maturity Assessment

| Dimension | Ops | CRM | Messaging |
|-----------|-----|-----|-----------|
| **Domain model richness** | Medium | Low (anemic registry) | Very low (replica only) |
| **Vertical completeness** | High for chat agent MVP | Partial (resolve+query only) | Partial (email OK, phone stub) |
| **API maturity** | High (used by ops-page) | None | Token endpoint only |
| **Eventing maturity** | Scaffold only | Broken publish path | Asymmetric (inbox for replica; direct for dispatch) |
| **Cross-module integration** | Good tool discovery pattern | Good query/command consumers | Good publishers; policy coupling |
| **Compliance (PDPA/GDPR)** | N/A | **Weak** (consent, anonymize) | Suppression only on email |
| **Observability** | Token logs | None | Send logs via providers |
| **Tests** | Thin unit | None | None |
| **Docs (README)** | None | Good intent | Good intent; slightly stale |
| **Production readiness** | Usable agent with security caveats | **Not** for privacy features | Email production-capable if BYOK; WhatsApp not |

### Scores (1–5)

| Module | Score | One-liner |
|--------|-------|-----------|
| **Ops** | **3.5** | Real product surface; tool ecosystem and authz incomplete |
| **CRM** | **2.0** | Solid core for checkout identity; compliance/API/workers incomplete |
| **Messaging** | **2.5** | Email dispatch works in architecture; phone + reliability + audit incomplete |

---

## Recommendations

### Immediate (P0)
1. **CRM workers**: Register `CrmOutboxPublisherJob` (+ inbox if adopting durable inbound pattern for `GlobalUserProfileUpdated`).
2. **Anonymize consumers**: Commerce (cancel/ban sub), Communications (suppress), optionally One; document compensation.
3. **Ops tool RBAC**: Pass `IExecutionContextAccessor.UserRole` into `GetAvailableTools` instead of hard-coded `SUPER_ADMIN`.
4. **Secure `/messaging/notify`**: Require auth or remove from public map; or restrict to SUPER_ADMIN/internal.
5. **Real WhatsApp provider** or feature-flag channel off until ready (stop silent console “success”).
6. **Outbox failure policy**: Retry with backoff before permanent fail + dead-letter; critical for Messaging.

### Short-term (P1)
7. **CRM Application layer** + unify Create into Resolve (or vice versa); implement Update + consent fields correctly (default **false**, explicit opt-in).
8. **CRM admin APIs** or confirm “internal-only” and remove orphan TypeSpec create/update if no product plan.
9. **Move `ICommunicationsQueryService` email config / suppression contracts** into Communications.Contracts; stop Messaging→Application reference.
10. **Ops**: Either implement full tool loop on `POST /ops/chat` or deprecate; sync OpenAPI (stream, system-message).
11. **Durable execute-action idempotency** (DB unique key / Redis).
12. **Fix conversation pagination total**.
13. **Stop Billing raw SQL join to CRM** — use `ICrmQueryService`.
14. **Add agent tools** for high-value Commerce/Communications reads if Ops is the admin copilot.
15. **Messaging delivery log** aggregate (channel, recipient hash, status, provider id, org, correlation to event id).

### Medium-term (P2)
16. Use **TenantReplica** in dispatch for inactive-tenant block and branding defaults.
17. Emit `MessageDispatchFailedIntegrationEvent` for credit refund / operator alerts.
18. CRM search by phone/email/name for Ops/Commerce admin; merge duplicates.
19. Align Create/Resolve uniqueness with DB unique index (email-unique per org is usually enough).
20. Ops conversation hard-delete or cascade soft-delete messages; retention policy for LLM logs (PII in chat).
21. Token cost → Billing credit optional FinOps.
22. Tests: CRM resolve/anonymize/outbox; Messaging dispatch matrix; Ops role filtering + execute-action.

### Architectural principles to reaffirm
- **Render at source, dispatch at edge** — keep (fixally already practiced).
- **CRM is PII SSOT for tenants** — enforce via contracts only (no cross-schema SQL).
- **Messaging stays terminal** — but if it keeps credit/suppression, document it as “Delivery Policy Orchestrator,” not dumb pipe.
- **Ops is presentation + agent**, not owner of customer or message data.

---

## File-by-File Notes

### Ops

| File | Notes |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Ops/Domain/OpsConversation.cs` | Clean aggregate; soft-delete OK |
| `.../Domain/OpsMessage.cs` | JSON columns as strings; `ResolveUiRequest` only flips flag (does not clear UI payload) |
| `.../Application/IOpsRepository.cs` | Minimal port; no count/total |
| `.../Application/DependencyInjection.cs` | Empty |
| `.../Application/Commands/DeleteConversationCommand.cs` | Soft-delete only |
| `.../Application/Commands/RenameConversationCommand.cs` | OK |
| `.../Application/Commands/RequestFormInputCommand.cs` | Intercepted in orchestrator; MediatR handler no-op |
| `.../Application/Services/ToolRegistry.cs` | Solid reflection schema gen; skips OrganizationId/Id/RecordedBy |
| `.../Application/Services/IToolRegistry.cs` | Role+app filters exist but underused |
| `.../Application/Services/ILlmOrchestratorService.cs` | Dual sync/stream |
| `.../Contracts/*` | Empty shell |
| `.../Infrastructure/Endpoints.cs` | Full surface; pagination total 0; memory idempotency; injects agent audit |
| `.../Infrastructure/DependencyInjection.cs` | Workers on; subscriptions empty |
| `.../Infrastructure/OpsDbContext.cs` | Schema ops; no FK message→conversation |
| `.../Infrastructure/Repositories/OpsRepository.cs` | Tenant-scoped queries OK |
| `.../Infrastructure/Services/LlmOrchestratorService.cs` | Stream path production-quality; sync path stub; SUPER_ADMIN hardcoded in prompts partial |
| `.../Infrastructure/Services/LlmOrchestratorService.Prompts.cs` | Hardcoded SUPER_ADMIN tools; form tool manual schema |
| `.../Infrastructure/Services/LlmOrchestratorService.Tools.cs` | Write → proposed action; read via MediatR |
| `.../Infrastructure/Services/ToolCallAccumulator.cs` | Byte-safe stream accumulate (documented bugfix) |
| `.../Infrastructure/Workers/*` | Idle |
| `.../Infrastructure/Migrations/20260627124819_InitialOpsSchema.cs` | Matches model |
| Tests: `tests/Modules.Ops.Tests/Services/LlmOrchestratorServiceTests.cs` | ExecuteReadTool only |

### CRM

| File | Notes |
|------|--------|
| `.../CRM/README.md` | Strong docs; overclaims anonymize fan-out |
| `.../Domain/ClientProfileEntity.cs` | Anemic; Anonymize behavior good |
| `.../Domain/BillingAddress.cs` | Defaults country `MYS` |
| `.../Contracts/ICrmQueryService.cs` | Bulk + by email |
| `.../Contracts/CreateClientProfileCommand.cs` | Unused |
| `.../Contracts/ResolveClientProfileCommand.cs` | Production path |
| `.../Contracts/AnonymizeClientProfileCommand.cs` | No callers |
| `.../Contracts/ClientProfileAnonymizedIntegrationEvent.cs` | No subscribers |
| `.../Infrastructure/DependencyInjection.cs` | **No workers**; only GlobalUser subscription |
| `.../Infrastructure/CrmDbContext.cs` | Inbox/outbox tables without jobs |
| `.../Infrastructure/CrmQueryService.cs` | Mapping OK; IgnoreQueryFilters |
| `.../Infrastructure/CreateClientProfileCommandHandler.cs` | Consent forced true; email OR phone match |
| `.../Infrastructure/ResolveClientProfileCommandHandler.cs` | Email match; enrich-only empty fields |
| `.../Infrastructure/AnonymizeClientProfileCommandHandler.cs` | Outbox publish without publisher job |
| `.../Infrastructure/EventHandlers/GlobalUserProfileUpdatedIntegrationEventHandler.cs` | Name/email only; direct not inbox |
| `.../Infrastructure/Configurations/ClientProfileConfiguration.cs` | Composite unique; consent default true |
| `.../Infrastructure/Migrations/20260627124815_InitialCrmSchema.cs` | Matches |
| TypeSpec `packages/api-spec/modules/crm/models.tsp` | DTOs without routes |

### Messaging

| File | Notes |
|------|--------|
| `.../Messaging/README.md` | Golden rule good; HTML strip / SMS claims stale |
| `.../Domain/TenantReplica.cs` | Minimal cache |
| `.../Contracts/DispatchMessageIntegrationEvent.cs` | CreditHoldId escape hatch; Channel stringly typed |
| `.../Application/ITenantReplicaRepository.cs` | Basic |
| `.../Application/SendTenantNotificationCommandHandler.cs` | Uses slug as recipient; odd API |
| `.../Application/TenantCreatedEventHandler.cs` | Upsert on provision |
| `.../Application/TenantUpdatedEventHandler.cs` | Update fields |
| `.../Application/EventHandlers/WorkspaceUpdatedEventHandler.cs` | Preserves IsActive (workspace event has no active flag) |
| `.../Application/Modules.Messaging.Application.csproj` | **Unused CRM.Contracts reference** |
| `.../Infrastructure/DependencyInjection.cs` | MessagingConnection string; full subscriptions |
| `.../Infrastructure/Endpoints.cs` | Unauthenticated notify |
| `.../Infrastructure/MessagingDbContext.cs` | Schema messaging |
| `.../Infrastructure/TenantReplicaRepository.cs` | OK |
| `.../Infrastructure/TenantProvisionedIntegrationEventHandler.cs` | Inbox write only |
| `.../Infrastructure/TenantUpdatedIntegrationEventHandler.cs` | Inbox write only |
| `.../Infrastructure/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` | Inbox write only |
| `.../Infrastructure/TenantProvisionedSeedingHandler.cs` | Log-only dead weight |
| `.../Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Core product logic; policy-heavy; WhatsApp console |
| `.../Infrastructure/MessagingInboxConsumerJob.cs` | Replica path |
| `.../Infrastructure/MessagingOutboxPublisherJob.cs` | Idle |
| `.../Infrastructure/Modules.Messaging.Infrastructure.csproj` | References Communications.**Application** + Billing Contracts |
| `.../Infrastructure/Migrations/20260627124803_InitialMessagingSchema.cs` | Matches |
| TypeSpec `packages/api-spec/modules/messaging/models.tsp` | Intentionally blank |

### Related cross-cutting files
| File | Relevance |
|------|-----------|
| `apps/lazuar-api/src/Lazuar.Api/Program.cs` | Module registration order; MapMessaging/Ops; no CRM endpoints; ConsoleMessagingService |
| `BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Always marks processed → lost messages |
| `BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Same poison policy |
| `BuildingBlocks/Infrastructure/ConsoleMessagingService.cs` | WhatsApp stub |
| `BuildingBlocks/Infrastructure/ResendEmailService.cs` | Real email; strict BYOK for tenants |
| `Modules/Billing/.../GenerateDraftDocumentQueryHandler.cs` | Illegal CRM schema join |
| `apps/ops-page/src/hooks/use-chat-stream.ts` | Real Ops consumer |

---

### Summary snapshot

| Module | Strength | Biggest hole |
|--------|----------|--------------|
| **Ops** | Working streaming agent + HITL + tool discovery | Hardcoded SUPER_ADMIN tools; thin tool catalog vs product UI |
| **CRM** | Correctly used as checkout identity SSOT | Anonymize/outbox/API/consent incomplete |
| **Messaging** | Clean contract + email dispatch path | No real phone channel, weak reliability/audit, blurry policy ownership |

This is a **read-only gap analysis**; no code was changed. I can turn any P0 cluster into an implementation plan next if you want.
