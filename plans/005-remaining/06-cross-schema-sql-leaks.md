# 06 — Cross-schema SQL / runtime boundary leaks (FW-4)

**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** `apps/lazuar-api` production runtime SQL that names foreign module schemas  
**Track:** FUTURE-WORK **FW-4**; future checklists F07 (inventory) / F08 (fix)  
**Method:** Fresh grep of schema-qualified SQL (`one.`, `commerce.`, `billing.`, `lhdn.`, `crm.`, `communications.`, `payments.`, `messaging.`, `ops.`), `FromSql` / Dapper / `NpgsqlCommand`, JOINs across schemas; cross-check plan `004-maintenance/04-module-boundaries-modularization.md` §7.1 and `docs/001-cross-module-communication.md` golden rule  
**Constraint:** Analysis only — **no application code was modified**

---

## 1. Executive summary

Compile-time modularity in lazuar-api is largely real (`ModuleBoundaryTests`, Contracts-only ProjectReferences). **Runtime SQL ignores that map.** Architecture tests cannot see string literals that `JOIN one."Organizations"` or `FROM lhdn."TaxDocuments"`.

| Category | Count (this inventory) | Action |
|----------|------------------------|--------|
| **Confirmed production path leaks** (consumer module/BB reads foreign private schema) | **6 code sites** (some multi-query) | Fix in F08 PRs |
| **Host dual-read (intentional interim, FW-1)** | **1** middleware | Expire with API-key cutover; not a “move SQL to Contracts” priority alone |
| **Dead / unused leak code still in tree** | **1** method | Delete or port |
| **Historical migration-only cross-schema SQL** | **1** | No runtime fix; note only |
| **Same-schema raw SQL (not a leak)** | Dozens | Out of scope for FW-4 fixes |
| **Good Contracts patterns already in place** | Several | Preserve; use as templates |

**Golden rule** (`apps/lazuar-api/docs/001-cross-module-communication.md`):

> A database query in the `commerce` schema must never execute a join to a table in the `crm` or `payments` schemas.  
> Sync reads only via `.Contracts`. Mutations across modules via integration events.

**Plan 004 §7.1** listed three leaks; this inventory **reconfirms all three** and **adds** Public arrears multi-schema join, Commerce→CRM inside `ICommerceDocumentLookup`, BB metrics LHDN product SQL, and host dual-read as a related boundary class. Billing’s former draft-doc cross-schema Dapper is **already fixed** via `ICommerceDocumentLookup` (implementation still has a residual CRM join inside Commerce — see L-05).

---

## 2. Module schema map (owners)

| Schema | Owning module | Typical private tables touched by leaks |
|--------|---------------|----------------------------------------|
| `one` | One | `GlobalUsers`, `Organizations`, `ApiCredentials` |
| `commerce` | Commerce | `TransactionLogs`, `CheckoutSessions`, `Subscriptions`, `Products` |
| `billing` | Billing | `LedgerEntries`, `LedgerLines`, outbox |
| `crm` | CRM | `ClientProfiles` |
| `communications` | Communications | `MessageTemplates`, `TenantEmailConfigurations` |
| `lhdn` | Lhdn | `TaxDocuments`, `DeveloperApiKeys` (legacy) |
| `payments` | Payments | (no foreign-schema *consumer* leaks found; Payments is a *source* of L-02) |
| `messaging` | Messaging | outbox/inbox only (via metrics) |
| `ops` | Ops | outbox/inbox only (via metrics) |

Shared connection string: all modules share one PostgreSQL database; schema isolation is logical only. Cross-schema JOINs work at runtime — which is why they ship unnoticed.

---

## 3. Search methodology & coverage

### 3.1 Greps executed

| Pattern | Intent |
|---------|--------|
| `FROM\|JOIN\|INTO\|UPDATE` + schema-qualified tables | Direct SQL ownership |
| `JOIN (crm\|one\|billing\|commerce\|…)` | Multi-schema joins |
| `FromSql` / `FromSqlRaw` / Dapper `QueryAsync` / `NpgsqlCommand` | Raw SQL surfaces |
| `GetDefaultTemplateIdsAsync`, `DocumentPublished`, `PlatformMetrics` | Known suspects from plan 04 / FW-4 |

### 3.2 What is **not** a FW-4 leak

| Pattern | Why excluded |
|---------|--------------|
| Module SQL only against **own** schema (e.g. `BillingQueryService` → `billing.*`, `CommerceQueryService.*` → `commerce.*`) | Correct isolation |
| Outbox/inbox `FromSqlRaw` inside a module worker targeting that module’s tables | Per-module messaging spine |
| EF migrations historical SQL (including one-time `communications`→`commerce` copy) | Not production request path |
| Integration/module **tests** that `CREATE TABLE billing…` | Test fixtures |
| Scope **strings** like `lhdn.documents:write` | Not SQL |
| Contracts-based reads (`ICrmQueryService`, `IOneQueryService`, `ICommunicationsQueryService.HasValidEmailConfigAsync`) | Correct sync boundary |

### 3.3 FromSql / Dapper inventory (modules)

| Area | Raw SQL? | Foreign schema? |
|------|----------|-----------------|
| Communications event handlers | Dapper in DocumentPublished | **Yes — L-01** |
| Payments PlatformEndpoints | Dapper | **Yes — L-02** |
| Commerce PublicArrearsEndpoints | Dapper | **Yes — L-03** |
| Commerce CommerceDocumentLookup | Dapper | **Partial — L-05** (crm) |
| Commerce CommerceRepository.GetDefaultTemplateIdsAsync | Dapper | **Yes — L-04** (dead) |
| BuildingBlocks PlatformMetricsCollector | NpgsqlCommand | **Yes — L-06** (all + lhdn product) |
| Host ApiKeyAuthenticationMiddleware | Dapper | **Yes — L-07** (host dual-read) |
| Billing *QueryService / sequence / workers | Dapper / SQL | Own schema only |
| Lhdn query/workers | Dapper / FromSql | Own schema only |
| Messaging / Ops | No Dapper/FromSql product SQL found | Clean for FW-4 |
| Communications QueryService / Broadcast job | Own schema only | Clean |

---

## 4. Leak inventory (exhaustive)

Priority guide:

| Pri | Meaning |
|-----|---------|
| **P0** | Production path; multi-schema join or wrong-module auth against private tables; blocks extract / multi-DB story |
| **P1** | Production path or platform infra; single foreign schema or BB product SQL; fix in dedicated PR |
| **P2** | Dead code, migration history, or low-risk residual; delete/port opportunistically |

---

### L-01 — Communications receipt context: 3-schema JOIN

| Field | Value |
|-------|--------|
| **ID** | L-01 |
| **Priority** | **P0** |
| **Consumer module** | Communications |
| **Foreign schemas** | `billing`, `one`, `commerce` |
| **Owning modules of tables** | Billing (`LedgerEntries`), One (`Organizations`), Commerce (`TransactionLogs`) |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| **Lines** | 41–54 (query); uses result at 56–99 |
| **Trigger** | Inbox/handler for `DocumentPublishedIntegrationEvent` after Billing stores a PDF |
| **PR group** | **PR-A** (`fix(comms): DocumentPublished payload / no cross-schema receipt SQL`) |

**Evidence (SQL):**

```41:52:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
        const string query = @"
            SELECT 
                org.""Slug"" as TenantSlug,
                org.""Name"" as BusinessName,
                t.""CustomerName"", 
                t.""CustomerEmail""
            FROM billing.""LedgerEntries"" e
            JOIN one.""Organizations"" org ON e.""OrganizationId"" = org.""Id""
            LEFT JOIN commerce.""TransactionLogs"" t ON e.""OrganizationId"" = t.""OrganizationId"" 
                AND (t.""ExternalReference"" = e.""ReferenceId"" OR t.""Id""::text = e.""ReferenceId"")
            WHERE e.""Id"" = @LedgerEntryId
            LIMIT 1";
```

**Event payload today** (too thin — forces re-query):

```6:10:apps/lazuar-api/Modules/Billing/Contracts/Events/DocumentPublishedIntegrationEvent.cs
public record DocumentPublishedIntegrationEvent(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string DocumentType,
    string StoragePath) : IIntegrationEvent
```

**Publisher already has customer display fields** when generating the PDF:

```56:59:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs
        var customer = await _commerceDocumentLookup.GetCustomerByGatewayTransactionAsync(
            request.OrganizationId, entry.ReferenceId, ct);
        var customerName = customer?.Name ?? "Customer";
        var customerEmail = customer?.Email ?? "";
```

…but does **not** put them on the integration event (publish at lines 117–122).

**Why it hurts:**

- Bypasses Contracts; couples Communications to three foreign write-models’ physical columns.
- Receipt email silently no-ops if join misses (`data == null` / empty email/slug).
- Blocks multi-database extract of any of the three modules.

**Proposed Contracts / event fix (preferred):**

1. Expand `DocumentPublishedIntegrationEvent` with denormalized receipt context:
   - `TenantSlug`, `BusinessName` (from One workspace snapshot or Billing profile + One at publish time)
   - `CustomerName`, `CustomerEmail` (already resolved via `ICommerceDocumentLookup` in publisher)
2. Optionally `DocumentLink` inputs only (`LedgerEntryId` already present).
3. Rewrite `DocumentPublishedIntegrationEventHandler` to use **only** event fields + local `communications.MessageTemplates` (EF already used for template body).
4. **Do not** add `IBillingQueryService.GetReceiptContext` that still JOINs foreign schemas inside Billing — denormalize on publish.

**Alternate port fix (if payload growth is rejected):**

- Billing Contracts: `IBillingDocumentPublishedContextQuery` returning DTO built **only** from `billing` + data Billing already stores/denormalizes (customer email should be on ledger or event history, not live JOIN to commerce).
- One: use existing `IOneQueryService.GetWorkspaceByIdAsync` for slug/name (Communications already depends on One.Contracts elsewhere — e.g. Fulfillment handler).
- Commerce: use `ICommerceDocumentLookup.GetCustomerByGatewayTransactionAsync` from Communications (would require LedgerEntry → ReferenceId on the event or a Billing port).

**Recommended path:** event payload enrichment (matches plan 004 §7.1 remediation #1).  
**Contrast good path:** `FulfillmentRequestedIntegrationEventHandler` already uses `ICrmQueryService` + `IOneQueryService` with no SQL.

---

### L-02 — Payments platform auth: SQL into `one.GlobalUsers`

| Field | Value |
|-------|--------|
| **ID** | L-02 |
| **Priority** | **P0** |
| **Consumer module** | Payments (misplaced host/admin feature) |
| **Foreign schema** | `one` |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` |
| **Lines** | 53–54 (`/auth/login`); 84–85 (`/auth/me`) |
| **Route registration** | Host maps `/api/v1/platform` → `MapPlatformEndpoints()` in `src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` (~78–82) |
| **PR group** | **PR-B** (`fix(platform): move super-admin auth out of Payments`) |

**Evidence:**

```52:54:apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs
            using var conn = sqlFactory.CreateConnection();
            var query = @"SELECT ""Id"", ""Email"", ""Name"", ""PasswordHash"", ""SecurityStamp"", ""IsSystemAdmin"", ""IsEmailVerified"", ""IsActive"" FROM one.""GlobalUsers"" WHERE ""Email"" = @Email LIMIT 1";
            var user = await conn.QuerySingleOrDefaultAsync<GlobalUserDto>(query, new { Email = email });
```

```83:85:apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs
            using var conn = sqlFactory.CreateConnection();
            var query = @"SELECT ""Id"", ""Email"", ""Name"", ""SecurityStamp"", ""IsSystemAdmin"", ""IsEmailVerified"", ""IsActive"" FROM one.""GlobalUsers"" WHERE ""Id"" = @Id LIMIT 1";
            var user = await conn.QuerySingleOrDefaultAsync<GlobalUserDto>(query, new { Id = userId });
```

Note: injects **`OneSqlConnectionFactory`** while living in **Payments** — dual smell (wrong module + foreign schema).

Same file also maps **payment-config** GET/PUT via MediatR (legitimate Payments domain) mixed with **super-admin auth**.

**Why it hurts:**

- Payments must not know GlobalUser password/security stamp columns.
- Super-admin cookie path lives next to tenant payment BYOK config — product confusion.
- Plan 004 §6.2 / §7.1 / §12 marks this **High / P0**.

**Proposed Contracts port fix:**

1. Add One Contracts port, e.g.:
   - `IPlatformAdminAuthQuery` / methods on `IOneQueryService`:
     - `Task<PlatformAdminUser?> GetSystemAdminByEmailAsync(string email)`
     - `Task<PlatformAdminUser?> GetSystemAdminByIdAsync(Guid id)`
   - DTO: `Id`, `Email`, `Name`, `PasswordHash`, `SecurityStamp`, `IsSystemAdmin`, `IsEmailVerified`, `IsActive` (or split “for login” vs “for me” to avoid over-fetching hash on `/me`).
2. Implement in `Modules.One.Infrastructure` against `one.GlobalUsers` only.
3. **Move** `MapPlatformEndpoints` auth routes (`/auth/login`, `/auth/logout`, `/auth/me`) into **One** Infrastructure endpoints (or host composition thin wrappers calling One). Keep payment-config routes on Payments under a non-auth group if still needed under `/platform`.
4. Cookie issuance helper can stay with the endpoint owner once moved.

**Do not:** leave SQL in Payments and only swap to a shared BB helper that still hardcodes `one.` tables.

---

### L-03 — Commerce public arrears “update-payment”: JOIN `crm` + `one`

| Field | Value |
|-------|--------|
| **ID** | L-03 |
| **Priority** | **P0** |
| **Consumer module** | Commerce |
| **Foreign schemas** | `crm`, `one` |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` |
| **Lines** | 44–55 (POST update-payment); GET arrears at 27–31 is **same-schema only** (not a leak) |
| **PR group** | **PR-C** (`fix(commerce): arrears update-payment via CRM + One contracts`) |

**Evidence:**

```44:53:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
            var query = @"
                SELECT s.""OrganizationId"", s.""ProductId"", s.""Status"", s.""CurrentDunningCampaignId"",
                       p.""Name"" as ProductName, p.""Price"", p.""Currency"", p.""GatewayName"" as ProductGatewayName,
                       cp.""Email"" as CustomerEmail,
                       org.""Slug"" as TenantSlug
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
                JOIN one.""Organizations"" org ON s.""OrganizationId"" = org.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
```

**Same file, GET is OK** (commerce-only):

```27:31:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
            var query = @"
                SELECT p.""Name"" as ProductName, p.""Price"" as Amount, p.""Currency"", s.""Status""
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
```

**Why it hurts:**

- Public unauthenticated path encodes physical CRM + One tables.
- Commerce already uses `ICrmQueryService` correctly in `CommerceQueryService.CustomCheckouts`, `SubscriberQueryService`, etc. — this endpoint is the regression.

**Proposed Contracts port fix:**

1. Load subscription + product via Commerce-owned SQL/EF only (keep `commerce` JOIN).
2. `ICrmQueryService.GetClientProfileAsync(subscription.ClientProfileId)` → email.
3. `IOneQueryService.GetWorkspaceByIdAsync(organizationId)` → `Slug`.
4. Build checkout metadata / URLs as today.

Optional: extract a small Application query `GetArrearsPaymentUpdateContextQuery` so the Minimal API stays thin.

---

### L-04 — Commerce dead template lookup: `communications.MessageTemplates`

| Field | Value |
|-------|--------|
| **ID** | L-04 |
| **Priority** | **P2** (dead on production paths; still a leak if called) |
| **Consumer module** | Commerce |
| **Foreign schema** | `communications` |
| **Files** | `Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` **118–138**; interface `Modules/Commerce/Application/ICommerceRepository.cs` **26** |
| **Callers** | **None** outside definition (confirmed grep; gap docs mark dead after dunning inline-copy refactor) |
| **PR group** | **PR-E** (`chore(commerce): remove GetDefaultTemplateIdsAsync cross-schema dead code`) |

**Evidence:**

```123:127:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
        const string query = @"
            SELECT ""Id"", ""Name"" 
            FROM communications.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""Name"" IN ('Subscription Renewal (3 Days)', 'Subscription Renewal Due Today', 'Subscription Renewal Overdue')";
```

**Proposed fix (prefer delete):**

1. **Delete** method + `ICommerceRepository` member if product still uses denormalized step copy (current dunning design).
2. If reintroduced later: `ICommunicationsQueryService.GetDefaultTemplateIdsAsync(orgId, names[])` or multi-`GetTemplateByNameAsync` — **never** raw `communications.*` from Commerce.

Plan 004 §7.1 still listed this as live; **status update:** leak code remains, **runtime path is dead**. Priority demoted to P2 vs plan’s original P0 bundle.

---

### L-05 — Commerce document lookup JOIN to `crm.ClientProfiles`

| Field | Value |
|-------|--------|
| **ID** | L-05 |
| **Priority** | **P1** |
| **Consumer module** | Commerce (implements a Contracts port used by Billing) |
| **Foreign schema** | `crm` |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| **Lines** | 57–61 (`GetDraftCheckoutSessionAsync`); `GetCustomerByGatewayTransactionAsync` (33–37) is **commerce-only — OK** |
| **Consumers of port** | `GenerateDraftDocumentQueryHandler`, `GenerateAndStoreDocumentCommandHandler` (Billing) |
| **PR group** | **PR-D** (`fix(commerce): CommerceDocumentLookup CRM via ICrmQueryService`) |

**Evidence:**

```57:61:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
        const string sql = @"
            SELECT c.""AdHocLineItems"", cp.""FullName"" AS CustomerName, cp.""Email"" AS CustomerEmail
            FROM commerce.""CheckoutSessions"" c
            LEFT JOIN crm.""ClientProfiles"" cp ON c.""ClientProfileId"" = cp.""Id""
            WHERE c.""Id"" = @SessionId AND c.""OrganizationId"" = @OrgId
            LIMIT 1";
```

**Context:** This port was introduced **to stop Billing** from doing commerce/crm SQL (good). Residual violation moved **into Commerce** — still a golden-rule break.

**Proposed Contracts port fix:**

1. SQL: select only `commerce.CheckoutSessions` (`AdHocLineItems`, `ClientProfileId`).
2. If `ClientProfileId` present: `ICrmQueryService.GetClientProfileAsync` for name/email.
3. Keep `ICommerceDocumentLookup` surface for Billing unchanged.

---

### L-06 — BuildingBlocks `PlatformMetricsCollector` multi-schema + LHDN product SQL

| Field | Value |
|-------|--------|
| **ID** | L-06 |
| **Priority** | **P1** (platform ops; couples BB to product domain) |
| **Consumer** | BuildingBlocks.Infrastructure (not a product module) |
| **Foreign schemas** | **All nine** module schemas for outbox/inbox; **`lhdn`** product table for stuck docs |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs` |
| **Lines** | 28–31 schema list; 139–175 outbox; 177–198 inbox; **200–225 LHDN stuck** |
| **Docs** | `apps/lazuar-api/docs/009-building-blocks-ownership.md` §3–4; FW-3/FW-4 ticket |
| **PR group** | **PR-F** (`refactor(metrics): pluginize PlatformMetricsCollector contributors`) |

**Evidence — hardcoded schema catalog:**

```28:31:apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs
    public static readonly string[] ModuleSchemas =
    [
        "one", "messaging", "payments", "crm", "ops", "billing", "lhdn", "commerce", "communications"
    ];
```

**Evidence — product-domain LHDN SQL (worst part):**

```206:211:apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs
        const string sql = """
            SELECT COUNT(*)
            FROM lhdn."TaxDocuments"
            WHERE "ValidationStatus" IN ('PENDING', 'SUBMITTED')
              AND "UpdatedAt" < (NOW() AT TIME ZONE 'UTC') - @threshold
            """;
```

**Nuance:**

| Sub-query | Severity | Notes |
|-----------|----------|-------|
| Outbox/inbox lag per schema | **Grey / technical** | Shared messaging spine; still hardcodes every schema name in BB (009 allows temporary god collector) |
| `lhdn.TaxDocuments` status vocabulary | **Hard leak** | Private product table + domain status strings in BB |

**Proposed Contracts / plugin fix (aligned with 009 + F13):**

1. Introduce `IPlatformMetricsContributor` (BB Application.Observability) returning partial snapshot fields.
2. Host or each module registers contributors:
   - **Technical:** per-module outbox/inbox contributor with schema name from DI registration (not a constant array in collector).
   - **Lhdn:** `LhdnStuckDocumentsContributor` in Lhdn.Infrastructure counting `TaxDocuments`.
3. `PlatformMetricsCollector` only aggregates registered contributors + publishes gauges.
4. **Do not** “fix” by adding more product SQL into BB.

---

### L-07 — Host API key dual-read: `one` + `lhdn` (FW-1 related)

| Field | Value |
|-------|--------|
| **ID** | L-07 |
| **Priority** | **P1** as boundary debt; **timeline owned by FW-1** (dual-read until 2026-11-30) |
| **Consumer** | Host `Lazuar.Api` middleware |
| **Foreign schemas** | `one`, `lhdn` |
| **File** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` |
| **Lines** | 17–28 SQL constants; 110–142 lookup |
| **PR group** | **PR-G** (with FW-1 cutover: One-only lookup; optional `IApiCredentialLookup` on One Contracts) |

**Evidence:**

```17:28:apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs
    private const string OneLookupSql = """
        SELECT "Id" AS "CredentialId", "OrganizationId", "Scopes"
        FROM one."ApiCredentials"
        WHERE "KeyHash" = @KeyHash AND "IsActive" = true
        LIMIT 1
        """;

    private const string LhdnLookupSql = """
        SELECT "Id" AS "CredentialId", "OrganizationId", "Scopes"
        FROM lhdn."DeveloperApiKeys"
        WHERE "KeyHash" = @KeyHash AND "IsActive" = true
        LIMIT 1
        """;
```

**Classification nuance (important for F08):**

- Host is an **allowed composition root** for cross-cutting auth (plan 004 §6.3; maintenance notes: “host correctly owns cross-schema auth”).
- Still **runtime multi-schema SQL** and a dual-key migration smell.
- Documented intentional dual-read window in the file itself (`LookupCredentialAsync` remarks: remove Lhdn branch by **2026-12-15**).

**Proposed fix (when FW-1 complete):**

1. Prefer `IApiCredentialService` / new `IApiCredentialAuthLookup.FindActiveByKeyHashAsync` on **One.Contracts** only.
2. Drop Lhdn SQL branch after legacy row count is zero.
3. Host middleware stays; it should not hardcode table DDL forever.

Until cutover, treat L-07 as **tracked exception**, not an F08 drive-by.

---

## 5. Historical / non-runtime (record only)

### H-01 — Commerce migration copied template bodies from communications

| Field | Value |
|-------|--------|
| **File** | `Modules/Commerce/Infrastructure/Migrations/20260704163126_RefactorDunningEngine.cs` ~85–104 |
| **SQL** | `UPDATE commerce."DunningSteps" … FROM communications."MessageTemplates"` |
| **Runtime impact** | None (already applied migration) |
| **Action** | No F08 PR; do not reintroduce live joins |

---

## 6. Confirmed non-leaks / fixed precedents (do not “fix”)

These were historical pain points or look similar in greps but **obey** the golden rule today.

| Location | Pattern | Status |
|----------|---------|--------|
| `GenerateDraftDocumentQueryHandler` | Billing uses `ICommerceDocumentLookup` | **Fixed** vs old Billing→crm SQL |
| `GenerateAndStoreDocumentCommandHandler` | Customer via `ICommerceDocumentLookup` | **Fixed** on Billing side; residual L-05 inside Commerce |
| `FulfillmentRequestedIntegrationEventHandler` | `ICrmQueryService` + `IOneQueryService` | **Correct** |
| `OrderCompletedDigitalDeliveryHandler`, lifecycle handlers | CRM contracts | **Correct** |
| `CommerceQueryService.CustomCheckouts` / `Subscribers` / `SubscriberQueryService` | commerce SQL + `GetClientProfilesAsync` | **Correct** (contrast L-03 / L-05) |
| `BillingQueryService`, Lhdn workers, Comms QueryService | Own-schema Dapper | **Correct** |
| `PublicArrearsEndpoints` GET | commerce-only JOIN | **Correct** |
| `CommerceDocumentLookup.GetCustomerByGatewayTransactionAsync` | commerce-only | **Correct** |
| OutboxPublisherJob / InboxConsumerJob | Per-module tables | **Correct** technical spine |

---

## 7. Existing Contracts ports relevant to fixes

| Port | Module | Use for |
|------|--------|---------|
| `IOneQueryService` | One | Workspace slug/name (L-01 alt, L-03); extend for system-admin auth (L-02) |
| `ICrmQueryService` | CRM | Profile email/name (L-03, L-05) |
| `ICommunicationsQueryService` | Communications | Template by name (L-04 if kept); already used by Commerce for email config |
| `ICommerceDocumentLookup` | Commerce | Billing document customer/session; fix internal CRM join (L-05) |
| `IBillingQueryService` | Billing | Ledger/credits — **not** currently used for receipt email; prefer event denorm (L-01) |
| `IApiCredentialService` | One | Credential admin API; extend or add auth lookup for L-07 |
| `DocumentPublishedIntegrationEvent` | Billing | **Expand** for L-01 |

Ops.Contracts is **empty** — no role in this inventory.

---

## 8. Master ticket table (F07-compatible)

| # | ID | Location | Foreign schema(s) | Consumer | Proposed fix | Pri | PR |
|---|----|----------|-------------------|----------|--------------|-----|-----|
| 1 | L-01 | `Communications/.../DocumentPublishedIntegrationEventHandler.cs:41-54` | billing, one, commerce | Communications | Enrich `DocumentPublishedIntegrationEvent`; drop multi-JOIN | **P0** | **A** |
| 2 | L-02 | `Payments/.../PlatformEndpoints.cs:53-54, 84-85` | one | Payments | One auth query port; move auth endpoints to One/host | **P0** | **B** |
| 3 | L-03 | `Commerce/.../PublicArrearsEndpoints.cs:44-53` | crm, one | Commerce | commerce SQL + `ICrmQueryService` + `IOneQueryService` | **P0** | **C** |
| 4 | L-05 | `Commerce/.../CommerceDocumentLookup.cs:57-61` | crm | Commerce (Billing consumer of port) | Split session SQL + CRM port | **P1** | **D** |
| 5 | L-06 | `BuildingBlocks/.../PlatformMetricsCollector.cs:28-31, 200-225` | all + lhdn product | BB | `IPlatformMetricsContributor`; LHDN stuck in Lhdn | **P1** | **F** |
| 6 | L-07 | `Lazuar.Api/.../ApiKeyAuthenticationMiddleware.cs:17-28, 110-142` | one, lhdn | Host | FW-1 One-only + optional contract; tracked exception until cutover | **P1*** | **G** |
| 7 | L-04 | `Commerce/.../CommerceRepository.cs:118-138` | communications | Commerce (dead) | Delete method | **P2** | **E** |
| 8 | H-01 | Commerce migration `RefactorDunningEngine` | communications | n/a | None | — | — |

\*P1 with **calendar gate** from FW-1, not a free-standing F08 first pick.

---

## 9. PR grouping (F08 — one family per PR)

Execute in this order for risk/ROI:

| PR | Title (suggested) | Leaks | Depends on | Notes |
|----|-------------------|-------|------------|-------|
| **PR-A** | `fix(comms): replace cross-schema receipt SQL with DocumentPublished payload (FW-4)` | L-01 | Billing event version + any subscriber tests | Highest product risk if join flaky; matches FUTURE-WORK ticket #5 |
| **PR-B** | `fix(platform): super-admin auth via One contracts; out of Payments (FW-4)` | L-02 | One Contracts DTO | May split: (B1) port+impl, (B2) move endpoints |
| **PR-C** | `fix(commerce): arrears update-payment without crm/one JOIN (FW-4)` | L-03 | Existing CRM/One ports | Small, isolated public endpoint |
| **PR-D** | `fix(commerce): CommerceDocumentLookup uses ICrmQueryService (FW-4)` | L-05 | CRM contracts already | Keeps Billing port stable |
| **PR-E** | `chore(commerce): remove dead GetDefaultTemplateIdsAsync (FW-4)` | L-04 | None | Trivial delete |
| **PR-F** | `refactor(metrics): pluginize PlatformMetricsCollector (FW-3/FW-4)` | L-06 | Optional F13 checklist | Larger; can trail product leaks |
| **PR-G** | `feat(one): API key middleware One-only after dual-read end (FW-1/FW-4)` | L-07 | FW-1 migration complete | Do not rush before 2026-11-30 |

**Rule (F08):** Prefer multiple PRs. Do not “fix” by moving foreign SQL into BuildingBlocks. Do not add cross-schema SQL to new code in review.

---

## 10. Implementation outline (per leak — F08 checklist)

For each P0/P1 leak:

1. Reproduce with unit/module test or document the production query path.
2. Define/expand **owning** module Contracts DTO or event fields.
3. Implement in **owning** Infrastructure.
4. Replace consumer SQL with port/event fields.
5. Add tenant-isolation / missing-data tests where applicable.
6. Optional: forbid pattern via architecture/integration test if practical (string grep of foreign schema in module is brittle but possible for worst offenders).
7. Update this inventory + `FUTURE-WORK.md` FW-4 status when closed.

---

## 11. Diff vs plan 004 §7.1 (status refresh)

| Plan 004 §7.1 item | This inventory | Status change |
|--------------------|----------------|---------------|
| DocumentPublished 3-way JOIN | **L-01** | Still **open P0** |
| Commerce `GetDefaultTemplateIdsAsync` | **L-04** | Still in tree; **dead** → demote **P2** |
| Payments `PlatformEndpoints` → `one.GlobalUsers` | **L-02** | Still **open P0** |
| *(not listed)* PublicArrears multi-JOIN | **L-03** | **New P0** |
| *(not listed)* CommerceDocumentLookup → crm | **L-05** | **New P1** (partial fix residue) |
| *(metrics / 009)* PlatformMetricsCollector | **L-06** | **Open P1** (also FW-3) |
| *(host dual keys)* ApiKey middleware | **L-07** | Tracked with **FW-1** |
| Billing draft doc cross-schema | — | **Already remediated** via `ICommerceDocumentLookup` |

---

## 12. Suggested optional guardrails (out of fix PRs)

| Guardrail | Value | Cost |
|-----------|-------|------|
| CI/script: fail if module M contains `"otherSchema".` literals outside allowlist | Catches regressions | Needs careful allowlist for host + BB transitional |
| Architecture test: Communications Infrastructure must not contain `billing.` / `commerce.` / `one.` SQL strings after PR-A | Locks L-01 | Fragile to false positives |
| Review checklist line: “No foreign-schema SQL; Contracts or events only” | Process | Zero code |

---

## 13. Done when (FW-4 definition, restated)

From `plans/004-maintenance/FUTURE-WORK.md` FW-4:

- No known **production** paths use private foreign-schema SQL without an **approved exception** (009 / ADR / this inventory’s L-07 dual-read window).
- Metrics no longer hardcode **product** domain status SQL (`lhdn.TaxDocuments` stuck) inside BuildingBlocks.
- Plan 004 P0 boundary hygiene items for SQL are closed or explicitly superseded by tickets above.

**Not required for FW-4 done:** module extracts (FW-5), full BB email/LLM moves (FW-3), TypeSpec Wave B (FW-6).

---

## 14. Evidence index

| Artifact | Absolute path |
|----------|----------------|
| This inventory | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/005-remaining/06-cross-schema-sql-leaks.md` |
| Modularization §7.1 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/04-module-boundaries-modularization.md` |
| FUTURE-WORK FW-4 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/FUTURE-WORK.md` |
| F07 checklist | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f07-cross-schema-inventory.md` |
| F08 checklist | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f08-cross-schema-fix-leaks.md` |
| Golden rule | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/001-cross-module-communication.md` |
| BB ownership / metrics | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/009-building-blocks-ownership.md` |
| L-01 | `.../Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| L-02 | `.../Modules/Payments/Infrastructure/PlatformEndpoints.cs` |
| L-03 | `.../Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` |
| L-04 | `.../Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` |
| L-05 | `.../Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| L-06 | `.../BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs` |
| L-07 | `.../src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` |

---

*End of uncondensed FW-4 inventory. No application code was modified. Ready for F08 one-PR-per-family execution.*
