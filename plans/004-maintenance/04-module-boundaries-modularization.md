# 04 — Module Boundaries & Modularization Analysis

**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** `apps/lazuar-api` modular monolith — modules, contracts, integration events, cross-module references, `Program.cs` DI composition  
**Method:** Source inventory of `Modules/*`, `.csproj` ProjectReferences, `DependencyInjection` event subscriptions, architecture tests, ADR product strategy (019/021/022/023), prior gap docs under `docs/001-gaps/*`  
**Constraint:** Analysis only — no application code was modified  

---

## 1. Executive Verdict

### Is the modular monolith “fat”?

**No, it is not a fat monolith in the pejorative sense** (one deployable with no internal seams). It is a **working modular monolith** with:

| Property | Status | Evidence |
|----------|--------|----------|
| Physical module projects | **Yes** | 9 modules under `apps/lazuar-api/Modules/` |
| Per-module DB schema + migrations history | **Yes** | e.g. `MigrationsHistoryTable("__EFMigrationsHistory", "commerce")` |
| Contracts-only cross-module references (compile-time) | **Mostly enforced** | `ModuleBoundaryTests.Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts` |
| Domain isolation | **Enforced** | `ModuleBoundaryTests.Domain_Should_Remain_Completely_Isolated` |
| Outbox publisher per module | **Enforced** | `All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` |
| Integration-event backbone | **Yes** | ~30+ integration events; hybrid outbox → `InMemoryEventBus` |
| Host composition root | **Centralized** | `Program.cs` registers all modules, migrations, subscriptions, endpoints |

**However, several modules are domain-fat** (too many concerns inside one bounded context), and there are **runtime boundary leaks** (cross-schema SQL) that architecture tests cannot catch. Those are more dangerous than “too few modules.”

### Is further modularization justified?

| Action | Justified now? | Rationale |
|--------|----------------|-----------|
| Split into many new top-level modules | **No — premature** | Solo-founder / Pure CaaS MVP (ADR 023); overhead of 4-project modules + schemas + outbox/inbox jobs + arch tests is high |
| Merge over-split thin modules | **Soft yes (1 candidate)** | Messaging is a thin transport that Communications already orchestrates |
| Extract dual concerns from fat modules | **Later, when change-rate diverges** | Credits wallet vs ledger; outbound webhooks vs identity; dunning vs catalog |
| Fix boundary leaks without new modules | **Yes — highest ROI** | Cross-schema Dapper joins, host Application refs, dual API-key models |

**Bottom line:** The system is **well modularized for a Compliance CaaS MVP**. Pain is not “too few modules” — it is (a) a few **god consumers/hubs**, (b) **SQL that ignores schemas**, and (c) **historical dual systems** (LHDN keys vs One credentials). Extract only when a concern has a different team, deploy cadence, or failure domain — not to make the folder tree prettier.

---

## 2. Module Catalog (As Implemented)

Nine product modules + technical shared layers:

| Module | Schemas / DbContext | Layers | Stated product role | ~Source surface (non-bin) |
|--------|---------------------|--------|---------------------|---------------------------|
| **One** | `one` / `OneDbContext` | D/A/C/I | Identity, workspaces, entitlements, API credentials, outbound webhook registry | Large (auth + provisioning + webhooks) |
| **Commerce** | `commerce` / `CommerceDbContext` | D/A/C/I | CaaS core: products, checkout, subscriptions, dunning, coupons, orders | **Largest** |
| **Payments** | `payments` / `PaymentsDbContext` | D/A/C/I | BYOK gateways, webhooks, integration checkout, off-session charge | Medium-large |
| **Billing** | `billing` / `BillingDbContext` | D/A/C/I | Double-entry ledger + prepaid credits + docs + B2C consolidation | Medium-large (event hub) |
| **Lhdn** | `lhdn` / `LhdnDbContext` | D/A/C/I | MyInvois UBL submit/validate, certs, TIN, legacy API keys | Large (infra-heavy) |
| **Communications** | `communications` | D/A/C/I | Templates, email BYOK, broadcasts, suppressions, lifecycle messaging | Medium |
| **Messaging** | `messaging` | D/A/C/I | Message dispatch + tenant replica | **Thin** |
| **CRM** | `crm` | D/C/I (**no Application**) | Tenant customer PII registry | Thin by design |
| **Ops** | `ops` | D/A/C/I | Internal LLM agent / conversations | Medium; **Contracts empty** |

**BuildingBlocks** (`Domain` / `Application` / `Infrastructure`) + **SharedKernel**: technical core (entities, CQRS, outbox/inbox, JWT, email/messaging ports). Architecture tests assert BuildingBlocks does not reference module assemblies.

**CRM exception:** documented in `ModuleBoundaryTests.ModulesWithoutApplication` — handlers live in Infrastructure. Acceptable for a tiny PII module; do not treat as a template for new fat modules.

---

## 3. Intended Communication Rules (Evidence of Intent)

From `apps/lazuar-api/docs/001-cross-module-communication.md` and ADR 001 / 003:

1. **No direct DB joins** across module schemas.  
2. **No write-model references** across modules.  
3. **No cross-schema FKs** — store foreign Guids as primitives.  
4. **Sync reads** only via `.Contracts` (`I*QueryService` / MediatR queries).  
5. **Mutations across modules** via integration events + outbox (default async).  
6. **Host** should reference only `Infrastructure` projects.

These rules are **compile-time mostly held**, **runtime partially violated** (see §7).

---

## 4. Coupling Matrix

### 4.1 Compile-time: who references whose Contracts

Edges are **ProjectReference** from `Application` or `Infrastructure` of module A → `Modules.B.Contracts` (Domain never references other modules — tested).

| Consumer ↓ / Provider → | One | Messaging | CRM | Payments | Billing | Lhdn | Commerce | Communications | Ops |
|-------------------------|:---:|:---------:|:---:|:--------:|:-------:|:----:|:--------:|:--------------:|:---:|
| **One** | — | App | | Infra | | | Infra | | |
| **Messaging** | App | — | App | | Infra | | | Infra | |
| **CRM** | Infra | | — | | | | | | |
| **Payments** | | | | — | | | Infra | | |
| **Ops** | Infra | | | | | | | | — |
| **Billing** | Infra | | | Infra | — | Infra | Infra | | |
| **Lhdn** | App+Infra | | | Infra | App+Infra | — | | | |
| **Commerce** | App | | App | App+Infra | | | — | App+Infra | |
| **Communications** | App+Infra | App+Infra | App | | App+Infra | | App+Infra | — | |

**Legend notes:**

- **Ops.Contracts is empty** — no other module depends on Ops. Ops is a leaf (orchestrator UI/agent only).  
- **Nobody depends on Ops** → Ops can evolve freely without fan-out cost.  
- **One is the most depended-on** identity/tenant source.  
- **Commerce is a hub** (Payments, One, Billing, Communications all touch it).  
- **Communications is a high fan-out consumer** (Commerce + One + CRM + Messaging + Billing).

### 4.2 Dependency degree summary

| Module | Outbound contract deps (unique) | Inbound (others depend on it) | Role |
|--------|----------------------------------|-------------------------------|------|
| **One** | 2 (Messaging, Payments, Commerce) | **7** modules | Platform core hub |
| **Commerce** | 4 (One, CRM, Payments, Communications) | **4** (One, Payments, Billing, Communications) | Product/CaaS hub |
| **Communications** | **5** (Commerce, Messaging, One, CRM, Billing) | 2 (Commerce, Messaging) | Orchestration hub |
| **Billing** | 4 (Payments, Lhdn, Commerce, One) | 3 (Lhdn, Messaging, Communications) | Financial hub |
| **Lhdn** | 3 (Billing, One, Payments) | 1 (Billing) | Compliance satellite |
| **Payments** | 1 (Commerce) | 4 (One, Billing, Lhdn, Commerce) | Gateway pipe |
| **Messaging** | 4 (One, CRM, Billing, Communications) | 2 (One App, Communications) | Transport |
| **CRM** | 1 (One) | 3 (Messaging, Commerce, Communications) | PII leaf |
| **Ops** | 1 (One) | **0** | Isolated agent |

### 4.3 Event-driven coupling (runtime subscriptions)

Registered in each module’s `Use*Subscriptions` (`DependencyInjection.cs`) plus host dual-subscribes in `Program.cs`.

#### Published integration events (by Contracts inventory)

| Publisher module | Events (Contracts) |
|------------------|--------------------|
| **Payments** | `GatewayPaymentCompleted`, `GatewayPaymentFailed`, `GatewayRefundRequested/Completed/Failed`, `GatewayDisputeCreated`, `ExecuteOffSessionCharge`, `ApiCreditPurchased` |
| **Commerce** | `SubscriptionActivated/Suspended/Canceled/Resumed`, `OrderCompleted`, `ManualSubscriberEnrolled`, `ZeroAmountCheckoutCompleted`, `FulfillmentRequested`, `OutboundWebhookRequested`, `ExecuteOffSessionCharge` (**duplicate/stale**), coupon lifecycle only domain-level |
| **Billing** | `InvoiceIssued`, `ConsolidatedInvoiceIssued`, `DocumentPublished`, `CommissionAccrued`, `ManualPaymentRecorded` |
| **Lhdn** | `LhdnDocumentSubmitted/Validated/Cancelled`, `ApiKeyRevoked` |
| **One** | `TenantProvisioned`, `TenantUpdated`, `WorkspaceUpdated`, `GlobalUserProfileUpdated`, `AppEntitlementGranted`, `ApiKeyRevoked` |
| **CRM** | `ClientProfileAnonymized` |
| **Communications** | `DefaultTemplatesSeeded` |
| **Messaging** | `DispatchMessage` (inbound command-as-event) |
| **Ops** | *(none)* |

#### Subscription fan-in (handlers registered)

| Module | # event subscriptions | Notable consumed events |
|--------|----------------------|-------------------------|
| **Billing** | **12** (highest) | Gateway pay/refund/dispute, LHDN doc lifecycle, commerce zero-amount/manual enroll, One entitlement, internal invoice/commission |
| **Commerce** | **10** | Gateway pay/fail/refund, own lifecycle, Communications templates seeded, CRM anonymize |
| **Communications** | **7** | Entitlement, subscription suspend/cancel, fulfillment, document published, CRM anonymize, order completed |
| **Payments** | 4 | Refund requested, off-session charge, gateway complete/fail (integration checkout) |
| **Messaging** | 4 | Tenant provisioned/updated, workspace updated, DispatchMessage |
| **Lhdn** | 3 | InvoiceIssued, ConsolidatedInvoiceIssued, GatewayRefundCompleted |
| **One** | 1 | OutboundWebhookRequested (from Commerce) |
| **CRM** | 1 | GlobalUserProfileUpdated |
| **Ops** | **0** | `UseOpsSubscriptions` is effectively empty |
| **Host (Lazuar.Api)** | 3 | Dual `ApiKeyRevoked` (One + Lhdn), `WorkspaceUpdated` |

**Interpretation:** Billing and Commerce are **event hubs**. That is expected for a ledger + CaaS core, not automatically a modularization failure — but it means any new payment/compliance flow will touch these modules first.

### 4.4 Synchronous contract interfaces (query ports)

| Contract | Owning module | Primary consumers |
|----------|---------------|-------------------|
| `IOneQueryService` | One | Commerce, Billing, Communications, Ops LLM, host middleware |
| `IApiCredentialService` | One | Platform API auth path |
| `ICrmQueryService` | CRM | Commerce, Communications, Messaging |
| `ICommunicationsQueryService` | Communications | Commerce (checkout gate on email config), admin endpoints |
| `ISuppressionService` | Communications | Broadcast / compliance |
| `ISubscriberQueryService` | Commerce | Communications broadcast fanout |
| `ICommerceDocumentLookup` | Commerce | Billing document generation |
| `IBillingQueryService` | Billing | Admin/finance surfaces |
| `ICreditCostService` | Billing | Communications broadcasts, Lhdn credit paths |
| Payments queries (`GenerateCheckoutSession*`, `GetPaymentConfig`) | Payments | Commerce, Billing endpoints |

Checkout path example (sync composition is intentional and documented):

```19:49:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
public class InitiateCheckoutCommandHandler : ICommandHandler<InitiateCheckoutCommand, CheckoutResultDto>
{
    private readonly IOneQueryService _oneQueryService;
    // ...
    private readonly ICommunicationsQueryService _communicationsQueryService;
    // ...
        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        // ...
        var hasEmailConfig = await _communicationsQueryService.HasValidEmailConfigAsync(tenantId.Value);
```

This is **acceptable sync read coupling** for a single checkout transaction. It does make Commerce → One + Communications hard dependencies.

### 4.5 Visual graph (contract + event hubs)

```
                    ┌─────────────┐
                    │     Ops     │  (leaf agent; contracts empty)
                    └──────▲──────┘
                           │ IOneQueryService
┌──────────┐         ┌─────┴──────┐         ┌──────────────┐
│ Messaging│◄────────│    One     │────────►│  Payments    │
│ (dispatch)│ tenant │ identity + │ webhooks│  (gateways)  │
└────▲─────┘ replica │ credentials│         └──────┬───────┘
     │               └─────┬──────┘                │ Gateway* events
     │ DispatchMessage     │                        │
┌────┴────────────┐        │                   ┌────▼────────┐
│ Communications  │◄───────┼───────────────────│  Commerce   │◄── CaaS core
│ templates/bcast │        │  CRM queries      │ products,   │
└────────┬────────┘        │                   │ subs, dunning│
         │                 │                   └────┬────────┘
         │                 ▼                        │ lifecycle events
         │          ┌────────────┐                  │
         └─────────►│    CRM     │◄─────────────────┘
                    │ customer   │
                    │ PII        │
                    └────────────┘

Payments/Commerce/Lhdn events ──► Billing (ledger + wallet) ──► Lhdn (submit)
                                   │
                                   DocumentPublished ──► Communications ──► Messaging
```

---

## 5. God Modules (Too Many Concerns)

### 5.1 Scoring criteria used

A module is “god-like” when **≥2** of:

1. Multiple independent product domains under one schema  
2. Highest event fan-in or fan-out  
3. README “is NOT” claims violated by implementation  
4. Dual models that never reconcile (e.g. ledger vs wallet)  
5. Size + worker count disproportionate to single responsibility  

### 5.2 Ranked findings

#### A. Commerce — **Primary domain-fat module (justified hub)**

**Concerns packed together:**

| Concern | Evidence |
|---------|----------|
| Product catalog | `Product` aggregate, ProductEndpoints |
| Checkout sessions (product + ad-hoc) | `CheckoutSession`, custom payment links |
| Subscriptions / renewals | `Subscription`, `BillingEngineJob` |
| Dunning engine | `DunningCampaign`, `DunningEngineJob`, step entities |
| Coupons | `Coupon` + domain events (handlers missing) |
| One-time orders | `Order` |
| Transaction read model | `CommerceTransactionLog` |
| Payment-config HTTP façade | `PaymentConfigEndpoints` re-exports Payments commands |
| Portal / public checkout APIs | PublicEndpoints, SubscriberEndpoints |

**Workers:** BillingEngineJob, DunningEngineJob, CheckoutSessionExpiryJob, outbox/inbox.

**Why it feels fat:** Pure CaaS (ADR 019/023) *is* this module. Catalog + checkout + subscription state + dunning are one money lifecycle.

**When to split:** Only if dunning campaign complexity (multi-channel sequences, experimentation, AI copy) starts dominating PR surface and blocking product/checkout changes — see extraction plan §9.1.

#### B. Billing — **Event hub + dual financial models**

**Concerns:**

| Concern | Aggregates / workers |
|---------|----------------------|
| Double-entry ledger | `LedgerEntry` / `LedgerLine`, ValidateBalanced |
| Prepaid utility wallet | `TenantCreditBalance`, `CreditLedger`, holds, clawbacks |
| Document generation | QuestPDF, sequences, `DocumentPublished` |
| B2C LHDN prep | `B2cConsolidationJob`, consolidation statuses on ledger |
| Revenue recognition | `DeferredRevenueSchedule` entity; job **not registered** |

**Subscriptions: 12** — every payment/compliance path lands here.

**README drift:** README claims Billing “does *not* publish events that trigger side-effects” and historically claimed no cross-schema joins. Implementation publishes `DocumentPublished` and `ConsolidatedInvoiceIssued` (side-effectful), and doc generation uses `ICommerceDocumentLookup` (good) while Communications still joins billing/one/commerce for receipt email (bad — on Communications side).

**Dual models that never reconcile:** Ledger (money truth) vs Credits (utility units). They share a schema and event bus but different math. Strong extraction candidate **later** (§9.2).

#### C. One — **Platform kitchen sink (growing)**

**Concerns under `one` schema:**

| Concern | Types |
|---------|-------|
| Global CIAM | `GlobalUser`, passwords, email verification |
| Multi-tenant workspaces | `Organization`, memberships, invitations |
| App entitlements | `TenantAppEntitlement` |
| Platform API credentials | `ApiCredential`, scopes in Domain |
| Developer outbound webhooks | `TenantWebhookEndpoint`, `WebhookDeliveryOutbox`, `OutboundWebhookDispatcherJob` |
| Integrator provision | rate limiter + secret settings |

README still mentions Community subscription auto-membership and `AppAccessRequest` onboarding — product surface has shifted (Community removed per ADR 022). Webhooks are a **developer platform** concern hitchhiking on identity.

**Not “fat” yet by LOC**, but **semantically multi-domain**. Extraction of Webhooks is the cleanest future cut (§9.3).

#### D. Lhdn — **Compliance engine + legacy developer surface**

**Core:** TaxDocument lifecycle, UBL strategies, XSD, cert vault, TIN validation, submit/poll jobs.

**Extra concerns:**

- `DeveloperApiKey` aggregate + scopes (overlaps One `ApiCredential` — dual key systems; host dual-subscribes both revoke events)  
- Module-local webhook subscriptions (`WebhookSubscription`) vs One outbound webhooks  

LHDN is **infra-heavy** (XSDs, templates) more than “too many domains,” but **API keys should finish migrating to One** rather than extracting a new module.

#### E. Communications — **Orchestration hub (acceptable)**

Templates + email config + broadcasts + suppressions + lifecycle handlers. Fans out to Messaging via `DispatchMessageIntegrationEvent`. High *outbound* coupling is by design (orchestrator pattern). Not a god module of write models; more of a **coordinator**.

#### F. Payments — **Correctly scoped**

Gateways, webhook ingress, integration checkout sessions, off-session charges. Domain is small (`TenantPaymentConfiguration`, `IntegrationCheckoutSession`, `PaymentWebhookLog`). Slight smell: `PlatformEndpoints` lives under Payments but authenticates via **raw SQL into `one.GlobalUsers`** (§7).

#### G. Messaging — **Underweight / over-split candidate**

Essentially: tenant replica for messaging context + `DispatchMessage` handler + delivery logs. Thin Application layer. Communications already owns templates and decides *what* to send.

#### H. CRM — **Correct thin module**

Single aggregate focus (`ClientProfileEntity`). No HTTP endpoints — pure internal service. **Do not merge** into Commerce (PDPA/anonymization blast radius).

#### I. Ops — **Ceremony-heavy relative to contracts**

Full 4-layer module + inbox/outbox workers, but **empty Contracts**, **no event subscriptions**, agent tools discovered by reflection across assemblies. Functionally a host feature. Not harmful enough to merge into BuildingBlocks; just do not copy this shape for new domains.

---

## 6. Program.cs DI Composition (Composition Root)

### 6.1 Registration order (evidence)

From `apps/lazuar-api/src/Lazuar.Api/Program.cs`:

1. **MediatR** registers handlers from:
   - Host assembly  
   - All module **Application** assemblies (except CRM — none)  
   - All module **Infrastructure** assemblies (including CRM)  
2. **`Add*Module`** for all 9 modules (configuration-bound DbContexts, workers, keyed SQL factories, outbox buses).  
3. **Boot migrations** for all 9 DbContexts sequentially.  
4. **Auth middleware** stack including `ApiKeyAuthenticationMiddleware`, `TenantSecurityMiddleware` (One contracts).  
5. **`Use*Subscriptions`** for all 9 modules.  
6. **Host-level event subscriptions** for API key revoke (dual) + workspace updated.  
7. **Endpoint mapping** under versioned groups: One, Messaging, Payments (+ integration + platform), Ops, Billing, Lhdn, Commerce, Communications. **CRM has no Map endpoints** (internal only).

### 6.2 Composition-root smells

| Smell | Evidence | Severity |
|-------|----------|----------|
| Host references **Application** projects for Commerce + Communications | `Lazuar.Api.csproj` lines 20–21; ADR 001 says host → Infrastructure only | Medium — blurs layering; likely MediatR anchor convenience |
| Dual `ApiKeyRevokedIntegrationEvent` types | One + Lhdn contracts; host handler implements both | Medium — migration incomplete |
| Platform auth endpoints under Payments | `MapPlatformEndpoints` SQL against `one.GlobalUsers` | High boundary leak |
| Scope policies reference `Modules.One.Domain.PlatformApiScopes` | Program.cs auth policies | Mild — host knows Domain enum; prefer Contracts constants |
| Global MediatR registration of every module | Single mediator catalog | Acceptable for modular monolith; becomes painful only if modules become services |

### 6.3 Host as integration seam

Host-owned handlers (`Lazuar.Api/EventHandlers/*`) are correct for **cross-cutting cache invalidation** (API key revoke) that is not owned by any single module. Prefer **not** putting business ledger logic in the host.

---

## 7. Boundary Violations (Runtime / SQL / Duplicates)

Architecture tests enforce **assembly** boundaries. They do **not** enforce schema isolation in raw SQL. Confirmed leaks:

### 7.1 Cross-schema SQL (violates golden rule)

| Location | Schemas touched | Problem |
|----------|-----------------|---------|
| `Communications/.../DocumentPublishedIntegrationEventHandler.cs` | `billing` + `one` + `commerce` | JOIN across 3 modules to build receipt email context |
| `Commerce/.../CommerceRepository.GetDefaultTemplateIdsAsync` | `communications.MessageTemplates` | Commerce reads Communications tables directly |
| `Payments/.../PlatformEndpoints.cs` | `one.GlobalUsers` | Payments module authenticates super-admins against One tables |

**Remediation (no new modules):**

1. Expand `DocumentPublishedIntegrationEvent` payload with `TenantSlug`, `BusinessName`, `CustomerName`, `CustomerEmail` (Billing/Commerce already know these when publishing).  
2. Replace Commerce template ID SQL with `ICommunicationsQueryService.GetDefaultTemplateIdsAsync`.  
3. Move platform auth endpoints into One (or host) using `IOneQueryService` / One repository.

### 7.2 Stale / duplicate contracts

| Item | Status |
|------|--------|
| `Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` | Duplicate of Payments version; gap doc notes Commerce copy is unused/stale |
| `Lhdn` vs `One` `ApiKeyRevokedIntegrationEvent` | Dual systems during migration |
| README / ADR references to Community/Vault | Modules removed (ADR 022); some READMEs still mention Community events |

### 7.3 Dead / hollow module infrastructure

| Module | Hollow piece |
|--------|--------------|
| Ops | Empty Contracts; empty event subscriptions; inbox/outbox polled with no messages |
| Billing | `RevenueRecognitionJob` present but not live (README acknowledges) |
| Messaging | WhatsApp path still `ConsoleMessagingService` (product gap, not modularization) |

---

## 8. Boundaries vs Product Domains

Product strategy stack (still coexisting in docs — see `docs/001-gaps/20-architecture-intent-vs-implementation.md`):

| Pillar | ADR | Module mapping |
|--------|-----|----------------|
| **CaaS / headless checkout** | 019, 023 | **Commerce** (catalog, session, sub) + **Payments** (BYOK gateways) + **CRM** (buyer) + **Communications** (receipts/dunning messages) + **One** (tenant slug, webhooks) |
| **Compliance / LHDN** | 021 | **Billing** (ledger, B2C consolidation prep, tax liability) + **Lhdn** (UBL submit/validate) |
| **Utility monetization** | 019 | **Billing** credits wallet + deduct paths from Lhdn / Messaging / Communications |
| **Platform identity** | — | **One** |
| **Internal agent** | — | **Ops** |
| **Killed / removed** | 022 | Community, Vault modules **gone** from `Modules/` |

### Alignment quality

| Product domain | Module fit | Notes |
|----------------|------------|-------|
| Checkout & subscriptions | **Commerce + Payments** clean split | Payments = dumb pipe; Commerce = access/lifecycle — matches Billing README golden rule |
| Dunning | **Commerce** (orchestration) + **Communications** (templates) + **Messaging** (send) | 3-hop is correct; WhatsApp adapter missing |
| e-Invoice | **Billing → Lhdn** event chain | Coherent; UI lobotomized (ADR 023) but backend kept |
| Developer integration API | Split: **Payments** integration checkout + **One** credentials/webhooks + **Lhdn** legacy keys | Needs consolidation under One credentials, not a new module |
| Super-admin platform | **Payments.PlatformEndpoints** (misplaced) + **One** | Should live under One/host |

**Conclusion on product fit:** Module boundaries **already mirror Compliance CaaS pillars** better than the old 15-app super-app vision. Do not reintroduce “one module per micro-app.” Thin fulfillment wrappers stay as Commerce product metadata / fulfillment targets (ADR 019), not new backend modules.

---

## 9. Candidates for NEW Modules (Extraction Plans)

Only extract when the concern has **independent change rate, failure domain, or compliance blast radius**. Suggested order by value.

### 9.1 Candidate: `Dunning` extracted from Commerce — **Defer**

**When justified:** Multi-channel campaigns, A/B testing, external ESP integration, dedicated team/UI growth.

**What moves:**

- Aggregates: `DunningCampaign`, `DunningStep`, reminder dispatch logs  
- Workers: `DunningEngineJob` (maybe pause/resume commands)  
- Events: publish `FulfillmentRequested` / subscription suspend-cancel (still coordinate with Commerce subscriptions)

**What stays in Commerce:** `Subscription` state machine (`PAST_DUE`, dunning pause flags, campaign id as Guid ref).

**Steps:**

1. Freeze dunning public API in TypeSpec.  
2. New module projects + `dunning` schema; migrate tables.  
3. Commerce publishes `SubscriptionEnteredPastDue` / `SubscriptionBillingDue`; Dunning subscribes.  
4. Dunning never owns subscription write model — only campaign progress + step dispatch.  
5. Architecture tests + outbox jobs.  
6. Dual-run: keep engine in Commerce behind feature flag until parity.

**Cost:** High (subscription state is tightly coupled to dunning progress fields).  
**Recommendation:** **Do not extract for MVP.** Keep as `Commerce/Domain` + workers; optionally **namespace folder** `Commerce/Dunning/*` only.

---

### 9.2 Candidate: `Credits` / `Wallet` extracted from Billing — **Strongest future extract**

**When justified:** Credits become primary SaaS monetization with packages, promos, multi-currency, or separate FinOps reporting from merchant ledger.

**What moves:**

- `TenantCreditBalance`, `CreditLedger`, `CreditHold`, idempotency log  
- Commands: `DeductTenantCredit`, reserve/consume/release, clawback  
- `ICreditCostService`, platform top-up handlers (`PlatformTopUpEventHandler`, `StarterCreditSeederHandler`, chargeback clawback)  
- Events: optionally `CreditsDeducted` / `CreditsToppedUp` for audit consumers  

**What stays in Billing:** Double-entry ledger, tax, consolidation, documents, deferred revenue.

**Steps:**

1. Define wallet Contracts first (`ICreditWalletService`, commands).  
2. New `Modules/Credits` (or `Wallet`) schema `credits`.  
3. Move handlers; keep **same command type names** in a compatibility package for one release if needed.  
4. Lhdn / Communications / Messaging switch ProjectReferences from Billing.Contracts credit types to Credits.Contracts.  
5. Do **not** put merchant transaction ledger into Credits.  
6. Billing may subscribe to top-up payment events only if you want accounting entries for credit sales (optional double-write via event).

**Cost:** Medium — credit types already form a natural seam in Domain.  
**Recommendation:** Extract **only after** credit monetization is product-critical and ledger PRs constantly conflict with wallet PRs.

---

### 9.3 Candidate: `Webhooks` / `DeveloperPlatform` extracted from One — **Medium future**

**When justified:** Multi-endpoint fan-out, delivery observability product, third-party OAuth apps, rate limiting per integrator.

**What moves:**

- `TenantWebhookEndpoint`, `WebhookDeliveryOutbox`  
- `OutboundWebhookDispatcherJob`, signature helpers  
- Subscribe to `OutboundWebhookRequestedIntegrationEvent` (and future event catalog)  
- Possibly consolidate **API credentials** here later (or keep credentials in One)

**What stays in One:** Users, orgs, memberships, entitlements.

**Steps:**

1. Contracts: `IWebhookRegistrationService`, delivery query DTOs.  
2. Schema `webhooks` or `developer`.  
3. One stops hosting dispatcher; publishes only identity events.  
4. Migrate endpoints from One `SaveWebhookCommand` routes.  
5. Host API-key middleware continues using One or new Developer credentials service.

**Cost:** Medium.  
**Recommendation:** Prefer **finishing Lhdn → One credential migration** first; extract webhooks when delivery product grows.

---

### 9.4 Candidate: `Catalog` split from Commerce — **Not recommended**

Products + coupons as separate module from subscriptions creates sync pain on every checkout. Cohesion of “what we sell” + “who bought it” is high. **Keep together.**

---

### 9.5 Candidate: `Identity` vs `Tenancy` split inside One — **Not recommended**

GlobalUser vs Organization is a classic split, but every request needs both. Overhead exceeds benefit at current scale.

---

### 9.6 New module inventory — summary table

| Proposed module | From | Priority | Trigger |
|-----------------|------|----------|---------|
| **Credits / Wallet** | Billing | P2 | Credit monetization + PR conflict with ledger |
| **Webhooks / Developer** | One | P2–P3 | Multi-endpoint product + delivery SLAs |
| **Dunning** | Commerce | P3 | Campaign engine complexity explosion |
| Catalog | Commerce | **Reject** | Artificial split |
| Identity / Tenancy | One | **Reject** | Always co-loaded |

---

## 10. Candidates to MERGE (Over-Split)

### 10.1 Messaging → Communications — **Primary merge candidate**

**Why over-split:**

| Aspect | Messaging | Communications |
|--------|-----------|----------------|
| Domain richness | TenantReplica, MessageDeliveryLog | Templates, Broadcasts, Suppressions, Email config |
| Public product surface | Minimal notify endpoint | Full admin templates/broadcasts |
| Who decides content? | No | Yes — then emits `DispatchMessage` |
| Adapter reality | Console WA + email BB | Owns BYOK email credentials |

**Merge plan (if chosen):**

1. Move `DispatchMessageIntegrationEvent` handling + delivery logs into Communications Infrastructure.  
2. Move tenant replica into Communications **or** delete replica if queries can use One.Contracts snapshots.  
3. Drop Messaging module projects, schema migration to `communications`, update arch tests.  
4. Keep `IMessagingService` / `IEmailService` in BuildingBlocks as technical ports.  
5. Update Communications DI to own outbox for dispatch side-effects already local.

**Benefits:** One less schema, fewer hosted jobs, clearer “notification domain.”  
**Risks:** Communications becomes slightly fatter; Messaging isolation for future multi-provider scaling is lost (acceptable until WhatsApp is real and high-volume).

**Recommendation:** **Merge when touching WhatsApp integration** (one place for channel adapters). Until then, leave as-is if merge cost > feature work.

### 10.2 CRM → Commerce — **Reject**

PDPA anonymization and PII registry must remain isolatable. Multiple modules already consume `ICrmQueryService`. Merge would pollute Commerce with GDPR workflows.

### 10.3 Ops → Host / BuildingBlocks — **Soft optional**

Could become `Lazuar.Api` feature folder + BuildingBlocks LLM. Benefits small; module already leaf. **Leave** unless eliminating hollow inbox/outbox is a maintenance goal (then remove workers, keep module).

### 10.4 Lhdn → Billing — **Reject**

Different failure domain (government API, certs, XML). Correct satellite module. Billing prepares; Lhdn submits.

### 10.5 Payments → Commerce — **Reject**

ADR/README explicitly: Payments is dumb pipe. Gateway adapters must not entangle subscription math.

---

## 11. Is Further Modularization Premature?

### 11.1 Arguments *for* more modules now

- Billing dual models (ledger vs wallet) confuse onboarding.  
- One accumulates developer platform features.  
- Event hubs hard to reason about without diagrams.  

### 11.2 Arguments *against* (stronger today)

1. **ADR 023 Pure CaaS MVP** prioritizes shipping checkout + dunning, not perfect taxonomy.  
2. **Each module costs** 4 csproj + schema + dual workers + arch-test anchors + migration discipline + Program.cs lines.  
3. **Boundaries already encode product pillars** (CaaS / Payments / Ledger / LHDN / Identity).  
4. **Highest defects are leaky SQL and dual keys**, not missing folders.  
5. **Solo-founder scale** (ADR 016 theme): reverse proxy and deploy stay single API; microservices not on the horizon.  

### 11.3 Decision framework (use before any extract)

Extract a new module **only if all** hold:

1. Two concerns change for **different reasons** on a regular basis.  
2. Independent **test/deploy** desire (or different compliance boundary).  
3. Existing contracts already form a **clean cut** (few dual-write transactions).  
4. Team can afford **2–4 weeks** migration + dual-run without blocking revenue features.

Otherwise: **namespaces + folders inside the module** + fix leaks.

---

## 12. Recommended Near-Term Work (No New Modules)

Priority order for maintenance without premature modularization:

| P | Work | Why |
|---|------|-----|
| **P0** | Eliminate cross-schema SQL (DocumentPublished payload, Commerce template lookup via Contracts, platform auth out of Payments) | Restores golden rule; enables future extracts |
| **P0** | Finish API credential unification (Lhdn keys → One); delete dual revoke path | Removes host dual-subscribe complexity |
| **P1** | Delete stale `Commerce.ExecuteOffSessionChargeIntegrationEvent` | Contract hygiene |
| **P1** | Drop host ProjectReferences to Application assemblies; MediatR only via Infrastructure entrypoints | ADR 001 alignment |
| **P1** | Update module READMEs (Community references, Billing side-effect publishing truth) | Prevent false architecture |
| **P2** | Namespace internal partitions: `Commerce/Dunning`, `Billing/Wallet`, `One/Webhooks` | Cognitive modularization without project tax |
| **P2** | Consider Messaging→Communications merge when implementing real WhatsApp | Reduce over-split |
| **P3** | Credits extract if wallet product expands | §9.2 |
| **P3** | Webhooks extract if developer platform expands | §9.3 |

---

## 13. Extraction Plan Details (If Approved Later)

### 13.1 Credits module — detailed plan

**Target layout (ADR 001):**

```
Modules/Credits/
  Contracts/   # Deduct/Reserve/Clawback commands, ICreditCostService, top-up events
  Domain/      # TenantCreditBalance, CreditLedger, CreditHold, idempotency
  Application/ # handlers or thin ports
  Infrastructure/ # CreditsDbContext schema "credits", workers, DI
```

**Migration sequence:**

1. **Introduce** Contracts + empty Infrastructure registered in Program.cs (no dual write).  
2. **Copy** tables via EF migration from `billing.TenantCreditBalances` etc. to `credits.*` (or rename schema with careful downtime).  
3. **Switch** writers (Deduct handlers) to Credits; leave Billing ledger handlers alone.  
4. **Retarget** Lhdn/Communications/Messaging ProjectReferences.  
5. **Delete** credit entities from Billing Domain; keep only optional ledger entries for “credit pack purchased” if accounting needs them (subscribe to `CreditsToppedUp` → Billing ledger).  
6. **Arch tests** + module list update in `ModuleBoundaryTests.ModuleNamespaces`.  

**Acceptance:**

- No ProjectReference from Credits → Commerce domain  
- Deduct idempotency preserved  
- Platform top-up still via `GatewayPaymentCompleted` metadata type  

### 13.2 Webhooks module — detailed plan

**Target:** `Modules/Webhooks` (or `Developer`)

**Owned:**

- Endpoint registration CRUD  
- Delivery outbox + dispatcher  
- Signature verification helpers  
- Optional: event catalog allow-list  

**Inbound events:** keep `OutboundWebhookRequestedIntegrationEvent` in **Commerce.Contracts** (or move to Webhooks.Contracts and have Commerce reference it — prefer event owned by **publisher** Commerce, handler in Webhooks).

**Do not move:** tenant identity, API keys (until second phase).

### 13.3 Messaging merge — detailed plan

1. Inventory all publishers of `DispatchMessageIntegrationEvent` (Communications, One domain handlers, etc.).  
2. Move handler + `MessageDeliveryLog` + jobs into Communications.  
3. Collapse `TenantReplica` into query via One or drop if unused for critical paths.  
4. Remove Messaging from Program.cs, arch tests, TypeSpec if any.  
5. Single outbox for communications schema covers dispatch.  

---

## 14. Host DI Composition Map (Reference)

```
Program.cs
├── MediatR ← Host + {One,Messaging,Payments,Ops,Billing,Lhdn,Commerce,Communications}.Application
│             + all modules' Infrastructure (incl. CRM)
├── AddOneModule / AddMessagingModule / AddCrmModule / AddPaymentsModule
│   AddOpsModule / AddBillingModule / AddLhdnModule / AddCommerceModule / AddCommunicationsModule
├── MigrateAsync × 9 DbContexts
├── Middleware: Correlation → CORS → Auth → ApiKey → TenantSecurity → Authorization
├── Use*Subscriptions × 9
├── Host Subscribe: ApiKeyRevoked (One + Lhdn), WorkspaceUpdated
└── Map endpoints: One, Messaging, Payments, PaymentsIntegration, Ops, Billing,
                   Lhdn, Commerce, Communications, Platform
```

**Module Add*Module typically registers:**

- DbContext + schema-local migrations history  
- Keyed `ISqlConnectionFactory`  
- Keyed `IEventBus` → `OutboxEventBus<TDbContext>`  
- Repositories / query services implementing Contracts  
- Hosted: `*OutboxPublisherJob`, `*InboxConsumerJob`, domain jobs  

---

## 15. Coupling Heatmap (Qualitative)

| | One | Commerce | Payments | Billing | Lhdn | Comm | Messaging | CRM | Ops |
|--|-----|----------|----------|---------|------|------|-----------|-----|-----|
| **Structural importance** | Critical | Critical | Critical | Critical | High | High | Low | Medium | Low |
| **Fatness** | Medium↑ | **High** | Low | **High** (dual) | Medium | Medium | Low | Low | Low |
| **Extract urgency** | Webhooks P3 | Dunning P3 | None | **Wallet P2** | Keys cleanup | Merge Msg P2 | Merge target | None | Ceremony only |
| **Boundary health** | Good | Good* | Leak (platform SQL) | Good docs vs events | Dual keys | **SQL leak** | OK | Good | Hollow |

\*Commerce good at contracts; one SQL leak to communications templates.

---

## 16. Final Recommendations

1. **Do not declare the modular monolith “too fat” overall.** It is intentionally a modular monolith with real seams, schema isolation, and architecture tests — aligned with Compliance CaaS.  

2. **Do treat Commerce and Billing as intentionally large cores**, not accidental god classes. Split only along **Credits** and (later) **Webhooks** / **Dunning** when product forces it.  

3. **Do treat Messaging as over-split** relative to Communications; merge opportunistically.  

4. **Do not create modules** for Catalog, Identity-vs-Tenancy, or micro-apps (Community/Vault stay dead).  

5. **Invest maintenance effort in boundary hygiene** (cross-schema SQL, dual API keys, host Application refs) before any new `Modules/*` folder. That work **enables** extraction; extraction without hygiene only multiplies leaks.  

6. **Further modularization is mostly premature** relative to MVP revenue path (checkout + dunning + payments). The architecture is “thick in the right places.”

---

## 17. Evidence Index (Paths)

| Artifact | Path |
|----------|------|
| Modules root | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/` |
| Composition root | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs` |
| Host csproj | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj` |
| Boundary tests | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` |
| Cross-module rules | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/001-cross-module-communication.md` |
| New module ADR | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/001-implementing-new-module.md` |
| Events vs BB | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/003-event-driven-vs-building-blocks.md` |
| CaaS pivot | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` |
| Compliance pivot | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` |
| Intent vs impl | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/20-architecture-intent-vs-implementation.md` |
| Commerce gap | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/07-commerce-module.md` |
| Billing gap | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/05-billing-module.md` |
| Ops/CRM/Messaging gap | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/11-ops-crm-messaging.md` |
| Cross-schema receipt join | `Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| Commerce→communications SQL | `Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` (`GetDefaultTemplateIdsAsync`) |
| Payments→one SQL | `Modules/Payments/Infrastructure/PlatformEndpoints.cs` |
| Billing subscriptions | `Modules/Billing/Infrastructure/DependencyInjection.cs` (`UseBillingSubscriptions`) |
| Commerce subscriptions | `Modules/Commerce/Infrastructure/DependencyInjection.cs` (`UseCommerceSubscriptions`) |

---

*End of uncondensed analysis. No application code was modified.*
