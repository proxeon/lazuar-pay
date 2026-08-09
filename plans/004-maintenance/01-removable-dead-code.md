# 01 — Removable / Dead / Orphaned Inventory (Backend + TypeSpec)

**Scope:** `apps/lazuar-api/**`, `packages/api-spec/**`, `packages/api-types-ts/**`, `packages/api-types-dotnet/**`, `packages/lhdn-sdk-*/**`, root `Taskfile.yml` gen tasks, backend-touching scripts.  
**Excluded:** frontend apps (except incidental backend-contract mentions), docs archaeology except where it documents dead backend contracts.  
**Method:** directory inventory, solution graph, greps for community/vault renames, obsolete attributes, commented blocks, dual generated outputs, migration chains, endpoint vs TypeSpec surface, fixture usage.  
**Date:** 2026-08-09  
**Constraint:** analysis only — no application code changes.

---

## Executive summary

| Bucket | Count (approx) | Theme |
|--------|----------------|-------|
| **Confident delete** | ~15 items | Stale duplicate generated DTOs, fully commented empty test class, unused embedded fixture, secret cookie jar, obsolete README drafts that describe deleted modules |
| **Review before delete** | ~25 items | Legacy LHDN API-key dual-path, unregistered `RevenueRecognitionJob`, scope-probe endpoint, SharedKernel empty shell, community-era backend docs, TypeSpec blanks, Console messaging stub, migration rename debt |
| **Keep** | many | All live modules (One, Commerce, Payments, Billing, Lhdn, Communications, Messaging, CRM, Ops), EF migration histories, committed NSwag/TS gen outputs, Kiota SDK sources, DropLegacySchemas migration |

**Community / Vault modules:** already **gone** from the solution and `Program.cs`. Residual debt is docs, copy, template orphans, TypeSpec README fiction, and one intentional schema-drop migration — not live module code.

---

## 1. Confident delete

These are safe to remove with low risk of breaking runtime, build, or contract generation. Prefer a PR that only deletes + regenerates where needed.

### 1.1 Duplicate / dead NSwag output

| Path | Reason |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-dotnet/Generated/Models.cs` | **Dead sibling.** `Lazuar.ApiContracts.csproj` sets `EnableDefaultCompileItems=false` and compiles **only** `Lazuar.ApiContracts.cs`. `nswag.json` outputs to `Lazuar.ApiContracts.cs`. `Generated/Models.cs` is a stale NSwag artifact with the same namespace (`Lazuar.ApiTypes`) but is never compiled. ADR 005 and gap doc `docs/001-gaps/13-typespec-api-contracts.md` already call this out. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-dotnet/Generated/` (directory) | Empty after deleting `Models.cs`. No other consumers. |

**Evidence:**

```14:17:packages/api-types-dotnet/Lazuar.ApiContracts.csproj
  <ItemGroup>
    <!-- 2. Explicitly tell the compiler to ONLY compile the NSwag generated file -->
    <Compile Include="Lazuar.ApiContracts.cs" />
  </ItemGroup>
```

```31:32:packages/api-types-dotnet/nswag.json
      "generateJsonMethods": true,
      "output": "Lazuar.ApiContracts.cs"
```

Also update ADR 005 (`docs/architecture-decision-log/005-typespec-api-contract-generation.md`) which still says output is `Generated/Models.cs` — doc fix only.

### 1.2 Fully commented-out test class (no active tests)

| Path | Reason |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs` | Entire body is `//`-commented (~500 lines of golden XML). Class has **zero** live `[Test]` methods. Fixture class is a no-op that always “passes” by doing nothing. Delete file or restore real tests from golden master later under a new file. |

**Evidence:** grep for `public void` / `[Test]` only finds commented lines; file ends at line 513 with only commented assertions.

### 1.3 Unused embedded test fixture

| Path | Reason |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json` | Embedded as `EmbeddedResource` in `Lazuar.ArchitectureTests.csproj`, but **no test code** references it (`GetManifestResourceStream`, `PreHashedJson`, `ExpectedBase64Hash` — zero hits under `tests/**/*.cs`). Orphan fixture left after UBL golden tests were commented out. |
| Csproj item | Remove the `<EmbeddedResource Include="TestData\lhdn-golden-master.json" />` line when deleting the file. |

### 1.4 Secret / runtime artifact that must not live in repo

| Path | Reason |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/lhdn_sandbox/cookies.txt` | Netscape cookie jar containing a **live-looking JWT** (`lazuar_auth` for `sysadmin@lazuars.io`, role SUPER_ADMIN). Sandbox helper side-effect; not a source asset. Delete immediately; add `scripts/lhdn_sandbox/cookies.txt` (or `**/cookies.txt`) to `.gitignore` if scripts recreate it. |

### 1.5 Obsolete TypeSpec README (wrong module tree)

| Path | Reason |
|------|--------|
| Content of `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/README.md` | Documents `modules/auth/`, `modules/community/`, `modules/messaging/routes.tsp` and Community refund examples. Actual tree has one/commerce/payments/billing/lhdn/ops/communications/crm/platform — **no community**. Not “delete the package,” but **rewrite or replace** this README; treating current text as removable draft is correct. Confident: delete Community-centric sections and regenerate structure doc from `main.tsp` + `modules/*`. |

### 1.6 Local build outputs (already gitignored; safe to wipe locally)

| Path | Reason |
|------|--------|
| All `bin/`, `obj/` under `apps/lazuar-api/**` and `packages/api-types-dotnet/**`, `packages/lhdn-sdk-dotnet/**` | Covered by root `.gitignore` (`[Bb]in/`, `[Oo]bj/`). Present on disk from local builds; not source. |
| `packages/api-spec/dist/` | Root `.gitignore` has `dist/`. Produced by `task gen:spec` / `pnpm --filter @repo/api-spec build`. Do not commit; regenerate. |
| `packages/lhdn-sdk-ts/dist/` | Same `dist/` ignore; produced by `tsc`. Source of truth is `src/` (+ Kiota generated under `src/generated`). |

### 1.7 Empty solution folder noise (cosmetic)

| Path | Reason |
|------|--------|
| Empty folders in `apps/lazuar-api/Lazuar.slnx` such as `<Folder Name="/Modules/Lhdn/Application/" />`, `/Modules/Lhdn/Domain/`, `/Modules/Lhdn/Infrastructure/`, `/Modules/Billing/Infrastructure/`, bare `/Modules/` | Projects are already nested under real module folders; these empty folder nodes are VS solution clutter. Safe to prune from `.slnx` without touching projects. |

### 1.8 One-off note outside product surface

| Path | Reason |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/script/second-app-proof.md` | Singular `script/` (not `scripts/`) proof note; not wired to Taskfile, packages, or API. Out of primary backend scope but orphaned. Safe delete if no one claims it. |

---

## 2. Review before delete

Items that look dead or half-migrated but need product/ops confirmation, data migration, or a follow-up PR to rewire consumers.

### 2.1 Community / Vault residual (modules already deleted)

| Path / area | Status | Review question |
|-------------|--------|-----------------|
| `Modules/Community`, `Modules/Vault` directories | **Absent** — not in solution, not in `Program.cs` | No code to delete. Done for backend modules. |
| `packages/api-spec/modules/community`, `vault`, `docs-community.tsp`, `docs-vault.tsp` | **Absent** from disk; `main.tsp` has no imports | Done for contracts. |
| `apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260704104342_DropLegacySchemas.cs` | **Keep** — intentionally `DROP SCHEMA IF EXISTS community CASCADE; DROP SCHEMA IF EXISTS vault CASCADE;` | Do not delete; this is the cleanup migration for environments that still had those schemas. |
| `apps/lazuar-api/docs/001-cross-module-communication.md` | Stale examples (`Community` module, `CommunitySubscription`) | Rewrite against Commerce/CRM or archive under `docs/archive/`. |
| `apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md` | Mentions Community aggregate circular-dep example | Same. |
| `apps/lazuar-api/docs/003-data-sanitization-domain-rule-alignment.md` | Built around `CommunityPlan` / `CommunitySubscription` invariants | Obsolete for current product; archive or rewrite for Commerce subscriptions. |
| `apps/lazuar-api/docs/004-transactional-import-protocol.md` | Seeding steps target `community.Plans/Subscriptions/PaymentRecords` | Dangerous if followed; schemas dropped. Archive. |
| `apps/lazuar-api/docs/005-tenant-isolation-mapping-backfilling.md` | Lists `CommunityPlan`, `CommunitySubscription` | Archive/update. |
| `apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md` | SQL against `community.ReminderDispatchLogs` / `CommunityLifecycleJob` | Archive/update. |
| `Modules/Messaging/README.md`, `Modules/CRM/README.md` | Still describe Community/Vault as consumers of events | Doc-only; update when touching those modules. |
| `DefaultMessageTemplates.OrphanNames` | Lists `"Community Welcome"`, `"Community Payment Success"`, etc. | **Keep list** until ops run `DELETE …/templates/legacy-cleanup` on all tenants; then can shrink. |
| `TemplateEndpoints` preview sample `https://community.lazuar.com/checkout` | Hardcoded demo URL for `{{renewal_link}}` | Replace with portal/checkout URL when cleaning copy. |
| `CommunicationsQueryService` template vars `{{group_link}}` / Telegram wording | Product copy leftover | Confirm whether fulfillment still uses these tags. |
| `AppOptions.ClientUrl` comment “Community Enrollment page at port 3020” | Misleading default docs | Comment/default port only; keep property. |

### 2.2 Legacy LHDN API keys → One platform credentials (migration window)

| Path | Why review |
|------|------------|
| `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Entity still mapped to `lhdn."DeveloperApiKeys"`. New mint path uses `One.ApiCredential` + `IApiCredentialService`. |
| `ILhdnRepository` methods `GetDeveloperApiKeyAsync` / `ListDeveloperApiKeysAsync` / `AddDeveloperApiKey` | **No production callers** (only interface + repository impl). Dead API surface. |
| `[Obsolete]` façades: `GenerateApiKeyCommand`, `RevokeApiKeyCommand`, `ListApiKeysQuery` (+ handlers) | Endpoints already call `IApiCredentialService` directly. Only tests use façades (`GenerateAndListApiKeysTests`). |
| `ApiKeyAuthenticationMiddleware` dual-read | Still falls back to SQL on `lhdn."DeveloperApiKeys"` after One credentials. Required until all live keys migrated. |
| Dual event subscribe in `Program.cs` | Subscribes both `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent` and `Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent`. |
| Migrations creating/altering `DeveloperApiKeys` | **Never delete applied EF migrations.** Future work: new migration to drop table after dual-read removed. |

**Suggested exit criteria before delete:** zero rows in `lhdn.DeveloperApiKeys` (or all hashes mirrored in One), dual-read removed from middleware, obsolete commands/tests removed, then EF migration drops table.

### 2.3 Unregistered but still compiled workers / jobs

| Path | Why review |
|------|------------|
| `Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs` | Fully implemented `BackgroundService`. **Intentionally not registered** in DI: |
| | `// services.AddHostedService<RevenueRecognitionJob>();` with comment “C.1 deferred schedules not created yet.” Entity/table kept. Re-enable later or delete job + amortization tables together. |

### 2.4 Temporary / phase-probe HTTP surface

| Path | Why review |
|------|------------|
| `GET /api/v1/one/integrations/payments/checkouts/_scope-probe` | Comment says Phase 1 policy probe; real M2M routes already exist at `/integrations/payments/checkouts`. **No TypeSpec**, no FE references. Candidate delete after confirming no external integrator health-checks hit it. |

### 2.5 TypeSpec surface gaps / blanks (contract debt, not runtime dead code)

| Path | Why review |
|------|------------|
| `packages/api-spec/modules/messaging/models.tsp` | File body: “Left intentionally blank… Templates migrated… (e.g., Community).” Backend still has `POST /messaging/notify`, `GET /messaging/delivery-logs`. Either document as internal-only or add routes.tsp. |
| `packages/api-spec/modules/crm/models.tsp` | DTOs only; **no routes.tsp**. Matches intentional design (CRM is command/query contracts, no public HTTP). Keep models if shared; or drop unused DTOs not emitted into OpenAPI if never referenced. |
| Communications admin TypeSpec | Spec has `POST /broadcasts` only. Runtime also has `GET /broadcasts/preview`, `GET /broadcasts/{id}`, `DELETE /templates/legacy-cleanup`, public unsubscribe/resend webhooks — **not in TypeSpec**. Either add or mark internal. |
| Messaging HTTP endpoints | Live in C#, absent from OpenAPI. |

### 2.6 SharedKernel empty project

| Path | Why review |
|------|------------|
| `apps/lazuar-api/SharedKernel/` | Only `SharedKernelMarker.cs` + csproj referencing BuildingBlocks.Domain. All module Domain projects reference it. Architecture tests assert “must not contain Entity types.” Removing requires rewiring ~9 csproj refs + architecture tests. **Keep as intentional empty boundary** unless you collapse SharedKernel into BuildingBlocks.Domain by policy change. |

### 2.7 Duplicate PlatformDbContext (host vs building blocks)

| Path | Why review |
|------|------------|
| `apps/lazuar-api/src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` | **Unreferenced** abstract base (namespace `Lazuar.Api.Infrastructure.Data`). No usings elsewhere. Modules inherit `BuildingBlocks.Infrastructure.PlatformDbContext`. Likely pre-extraction leftover. **High confidence dead** but verify no reflection/source-gen. Prefer delete after one green build. |
| `BuildingBlocks/Infrastructure/PlatformDbContext.cs` | **Keep** — real base for all module DbContexts. |

### 2.8 Console messaging stub

| Path | Why review |
|------|------------|
| `BuildingBlocks/Infrastructure/ConsoleMessagingService.cs` wired in `Program.cs` as `IMessagingService` | SMS/WhatsApp path logs to console — not “dead,” but **stub**. Do not delete until a real Twilio/Meta adapter exists; otherwise production silent-fails to console. Product decision, not garbage collection. |

### 2.9 Broadcast credit fields vs migration rename

| Path | Why review |
|------|------------|
| Migration `20260705133017_RemoveBroadcasts` | **Misnamed** — does **not** drop Broadcasts table; only drops `CreditHoldId`, `CreditsReserved`, `CreditsUsed` columns. Broadcast feature is still live (endpoints, `BroadcastFanoutJob`, DbSet). |
| `BroadcastStatusDto.CreditsReserved` / `CreditsUsed` | Still on contract DTO; always zero after column drop. Can remove fields after FE consumers checked. |
| `BroadcastEndpoints` preview forces credits to 0 “free now” | Product intentional; not dead. |

### 2.10 Split test projects (not dead, possible consolidation)

| Path | Why review |
|------|------------|
| `tests/Modules.Billing.Tests` (CreditHold, TenantCreditBalance) | Separate from `Lazuar.ModuleTests/Billing/*`. Both in solution and `task api:test`. Not duplicate fixtures; organizational split. Consolidate only if desired. |
| `tests/Modules.Ops.Tests` | Same pattern for LLM orchestrator. Keep unless merging test strategy. |

### 2.11 Template legacy-cleanup endpoint

| Path | Why review |
|------|------------|
| `DELETE /admin/communications/templates/legacy-cleanup` | Operational one-shot to delete orphan community-era template names. Not in TypeSpec. Keep until all tenants cleaned; then remove endpoint + `OrphanNames` list. |

### 2.12 LHDN dual api-keys façades on HTTP

| Path | Why review |
|------|------------|
| `MapLhdnEndpoints` `/lhdn/api-keys` | Product façade over `IApiCredentialService` — **not dead**, intentional for LHDN console UX. Also mirrored under `/one/api-keys`. Keep both unless product unifies. |

### 2.13 Generated clients: commit policy

| Artifact | Policy today | Review |
|----------|--------------|--------|
| `packages/api-types-dotnet/Lazuar.ApiContracts.cs` | **Committed** (Taskfile `gen` generates; csproj compiles it) | Keep committed so CI/backend builds without NSwag on every machine. |
| `packages/api-types-ts/src/index.ts` | **Committed** | Same for frontends. |
| `packages/lhdn-sdk-ts/src/generated/**`, `packages/lhdn-sdk-dotnet/src/Generated/**` | **Committed** (Kiota via `task gen:sdk-lhdn`) | Keep; dist for TS is build-only. |
| `packages/api-spec/dist/**` | **Gitignored** via `dist/` | Must run `task gen:spec` before type gen. CI should always gen first. |

### 2.14 Misleading migration / task names

| Item | Review |
|------|--------|
| `task api:migrations:purge` | Deletes all Migrations folders — nuclear reset for greenfield only; dangerous on shared DBs. Not dead code; document risk. |
| Commerce migration `SyncCommerceModel` | Typical “model drift catch-up” name; keep history. |
| Communications `RemoveBroadcasts` | Rename only in future docs; never rewrite applied migration. |

### 2.15 Gap / ADR docs still naming community (outside api package, for awareness)

Not in deletion scope for this file’s “modify app code” constraint, but maintenance owners should treat as stale:

- `docs/architecture-decision-log/007-product-scoped-api-references.md` (still shows `docs-community.tsp` build line)
- `docs/architecture-decision-log/017`, `014`, `020` (roadmap / portal community routes) — historical; watermarked by 021/022/023
- `docs/001-gaps/20-architecture-intent-vs-implementation.md` already tracks residual community debt

---

## 3. Keep (not removable)

### 3.1 Live module projects (all referenced from host + solution)

| Module | Layers present | Host registration |
|--------|----------------|-------------------|
| One | Contracts/Domain/Application/Infrastructure | `AddOneModule`, `MapOneEndpoints`, subscriptions |
| Messaging | full stack | `AddMessagingModule`, `MapMessagingEndpoints` |
| CRM | Contracts/Domain/Infrastructure (**no Application layer** — intentional thin module) | `AddCrmModule` (no public MapCrmEndpoints) |
| Payments | full stack + IntegrationEndpoints + PlatformEndpoints | `MapPaymentsEndpoints`, `MapPaymentsIntegrationEndpoints` |
| Ops | full stack | `MapOpsEndpoints` |
| Billing | full stack | `MapBillingEndpoints` |
| Lhdn | full stack | `MapLhdnEndpoints` |
| Commerce | full stack | `MapCommerceEndpoints` |
| Communications | full stack | `MapCommunicationsEndpoints` |
| BuildingBlocks Domain/Application/Infrastructure | shared | used by all |
| SharedKernel | marker only | architecture boundary — keep until policy change |
| Host `src/Lazuar.Api` | Program, middleware, event handlers | composition root |
| Packages `Lazuar.ApiContracts` + `Lazuar.Lhdn.Sdk` | solution `/Packages/` and Lhdn folder | DTOs + external SDK |

### 3.2 Intentional dual surfaces

- **LHDN api-keys façade** + **One api-keys** — both write platform credentials.
- **Presigned URL** at `/one/storage/presigned-url` (TypeSpec + C#) — vault module deleted; capability relocated to One (ADR 022).
- **Integration checkouts** TypeSpec + `IntegrationEndpoints.cs`.
- **Broadcasts** domain/job/endpoints — live product path (credits columns removed only).

### 3.3 EF migration chains

Do **not** squash/delete historical migrations in production-tracked branches without a deliberate rebaseline. Includes:

- `DropLegacySchemas` (community/vault DROP)
- All `AddOutboxInboxRetryAndDeadLetter` module migrations
- Commerce dunning/checkout evolution chain
- Communications `AddBroadcasts` then credit-column removal
- One `CreateApiCredentials`

### 3.4 Taskfile gen pipeline

```yaml
# Taskfile.yml — keep
gen → gen:spec → gen:types-ts → gen:types-dotnet → gen:sdk-lhdn
```

Sources: `packages/api-spec/**/*.tsp`. Outputs: openapi (local dist), `api-types-ts/src/index.ts`, `api-types-dotnet/Lazuar.ApiContracts.cs`, Kiota trees.

### 3.5 LHDN XSD/templates/schemas

`Modules/Lhdn/Infrastructure/Schemas/**`, `Templates/**` — required for UBL generation/validation. Keep.

### 3.6 Architecture + module + integration tests that assert live behavior

Everything under `tests/` except items listed in confident-delete and the dead UBL file / golden fixture.

### 3.7 Sandbox scripts (except cookies.txt)

`scripts/lhdn_sandbox/0*.sh`, `run_all.sh` — active LHDN sandbox automation. Keep; ignore cookies.

---

## 4. Endpoint / DTO orphan notes (detail)

### 4.1 Runtime endpoints not in TypeSpec (internal or debt)

| Runtime route | Module | Action |
|---------------|--------|--------|
| `GET/POST messaging/*` | Messaging | Document internal or add TypeSpec |
| `GET …/broadcasts/preview`, `GET …/broadcasts/{id}` | Communications | Add models+routes or FE uses untyped |
| `DELETE …/templates/legacy-cleanup` | Communications | Temporary ops; remove after cleanup |
| Public compliance: unsubscribe, resend webhook | Communications | Often omitted from product OpenAPI deliberately |
| `GET …/_scope-probe` | One | Candidate delete (see 2.4) |
| Platform admin auth under `platformGroup` | Payments.PlatformEndpoints | Super-admin surface; check if TypeSpec `platform/routes.tsp` covers |

### 4.2 TypeSpec models without HTTP

| Spec | Notes |
|------|-------|
| `crm/models.tsp` | No CRM public API — OK if only for shared DTO reuse; verify OpenAPI emission (imported by main.tsp — models may appear even without routes). |
| `messaging/models.tsp` | Blank file still imported by `main.tsp` — noise; delete import + file if nothing to emit. |

### 4.3 Obsolete command types still compiled

| Type | Callers |
|------|---------|
| `GenerateApiKeyCommand` / Handler | Tests only |
| `RevokeApiKeyCommand` / Handler | Likely tests only (endpoints use service) |
| `ListApiKeysQuery` / Handler | Tests only |
| Repository DeveloperApiKey methods | **No callers** |

### 4.4 Hand-maintained DTOs outside NSwag

| Type | Location | Notes |
|------|----------|-------|
| `BroadcastStatusDto`, `BroadcastCostPreviewDto` | `Modules.Communications.Contracts` | Not in TypeSpec/NSwag; credits fields stale |
| Integration checkout request/response records | Payments Contracts | Parallel to TypeSpec models — intentional tight control for M2M |

---

## 5. Migrations pattern inventory

| Pattern | Example | Removable? |
|---------|---------|------------|
| Initial schema per module | `InitialOneSchema`, `InitialCommerceSchema`, … | No |
| Cross-cutting retry/dead-letter | `AddOutboxInboxRetryAndDeadLetter` × modules | No |
| Legacy schema drop | `DropLegacySchemas` (community/vault) | No — historical truth |
| Feature add then partial reverse | `AddBroadcasts` → `RemoveBroadcasts` (columns only) | No; rename confusion only |
| Auth migration | `CreateApiCredentials` (One) + `AddDeveloperApiKeyScopesAndKeyHint` (Lhdn) | No until dual-path ended + drop table migration |
| `api:migrations:purge` + `api:migrations:init` | Taskfile nuclear reset | Keep tasks for greenfield; never run against prod |

**Obsolete pattern to stop doing:** documenting SQL against `community.*` tables (docs 004/006) — schemas gone.

---

## 6. Generated files: commit vs regenerate matrix

| Path | Generated by | Commit? | Delete? |
|------|--------------|---------|---------|
| `api-spec/dist/**/*.yaml` | TypeSpec | No (gitignored) | Wipe locally anytime |
| `api-types-ts/src/index.ts` | openapi-typescript | Yes | No — regenerate with `task gen:types-ts` |
| `api-types-dotnet/Lazuar.ApiContracts.cs` | NSwag | Yes | No — regenerate with `task gen:types-dotnet` |
| `api-types-dotnet/Generated/Models.cs` | old NSwag path | Should not | **Yes delete** |
| `lhdn-sdk-ts/src/generated/**` | Kiota | Yes | No — `task gen:sdk-lhdn` |
| `lhdn-sdk-dotnet/src/Generated/**` | Kiota | Yes | No |
| `lhdn-sdk-ts/dist/**` | tsc | No | Local wipe |
| EF `*ModelSnapshot.cs` / migration Designer | EF tools | Yes | Never manual delete |

---

## 7. Dead references after renames (community / vault)

| Kind | Finding |
|------|---------|
| Backend modules | **Removed.** No `Modules.Community` / `Modules.Vault` csproj. |
| Program.cs | **No** commented Community/Vault DI (unlike ADR 022 Phase 1 snapshot). Current host only maps live modules. ADR 022 text is partially obsolete (Phase 2+ done for backend). |
| TypeSpec | **No** community/vault imports in `main.tsp`. |
| Schema | Dropped via One migration `DropLegacySchemas`. |
| Secrets “vault” | **Keep** naming: `ISecretVault`, `AesSecretVault`, Azure Key Vault, LHDN `CertificateVaultService` — unrelated product module. |
| Commerce “vaulted token” | Payment method storage terminology — keep. |
| Docs / templates / README | Still reference Community — clean as docs maintenance. |
| Frontend | Out of scope; portal still has `src/modules/community/` per ADR 022 residual list. |

---

## 8. Prioritized cleanup checklist (suggested PR order)

### PR A — zero-risk deletes (confident)

1. Delete `packages/api-types-dotnet/Generated/Models.cs` (+ empty `Generated/`).
2. Delete `UblStrategyTests.cs` (or replace with real tests later).
3. Delete `lhdn-golden-master.json` + EmbeddedResource line.
4. Delete `scripts/lhdn_sandbox/cookies.txt`; gitignore cookies.
5. Prune empty folders in `Lazuar.slnx`.
6. Rewrite `packages/api-spec/README.md` to match actual modules.
7. Optional: delete host `PlatformDbContext.cs` after build verify.
8. Optional: delete `script/second-app-proof.md`.

### PR B — docs hygiene (backend docs)

1. Archive or rewrite `apps/lazuar-api/docs/001`–`006` community import/isolation playbooks.
2. Update Messaging/CRM READMEs (Community → Commerce).
3. Fix ADR 005 Generated/Models.cs path.
4. Fix TemplateEndpoints preview URL + AppOptions comment.

### PR C — LHDN API key migration finish (review)

1. Inventory prod `lhdn.DeveloperApiKeys` vs `one.ApiCredentials`.
2. Remove dual-read in middleware + dual event subscribe.
3. Delete Obsolete commands, repository key methods, entity mapping.
4. New migration: drop `lhdn.DeveloperApiKeys`.
5. Update tests.

### PR D — product decisions

1. Remove `_scope-probe` or promote to documented health check.
2. Register or delete `RevenueRecognitionJob` + amortization model.
3. TypeSpec coverage for messaging/broadcasts/public compliance or mark internal.
4. After tenant template cleanup, remove `legacy-cleanup` + `OrphanNames`.
5. Strip dead credit fields from `BroadcastStatusDto`.

---

## 9. Categorized master lists

### Confident-delete

1. `packages/api-types-dotnet/Generated/Models.cs` (+ directory)
2. `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs`
3. `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json` + csproj EmbeddedResource
4. `scripts/lhdn_sandbox/cookies.txt` (+ gitignore)
5. Obsolete community examples in `packages/api-spec/README.md` (rewrite)
6. Empty `.slnx` folder nodes under Lhdn/Billing/Modules
7. Local `bin/`/`obj/`/`dist/` trees (never commit; wipe freely)
8. `script/second-app-proof.md` (if unused)
9. Host `src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` (after build confirmation — borderline confident)

### Review-before-delete

1. LHDN `DeveloperApiKey` aggregate + EF config + repository methods
2. Obsolete LHDN Generate/Revoke/List API key commands/handlers
3. Dual-read auth middleware + dual ApiKeyRevoked subscriptions
4. `RevenueRecognitionJob` (+ DI comment block)
5. `_scope-probe` endpoint
6. `messaging/models.tsp` blank + missing messaging routes
7. CRM TypeSpec models without routes (confirm emission value)
8. Broadcast credit DTO fields; misnamed RemoveBroadcasts migration story
9. `legacy-cleanup` endpoint + `OrphanNames`
10. SharedKernel project (empty by design)
11. ConsoleMessagingService (stub, not garbage)
12. Backend docs 001–006 community content
13. Module READMEs (Messaging, CRM) community/vault language
14. Template Telegram/community copy and sample URLs
15. Split `Modules.Billing.Tests` / `Modules.Ops.Tests` (consolidate optional)
16. TypeSpec/OpenAPI gaps for live admin endpoints
17. `BroadcastStatusDto` / `BroadcastCostPreviewDto` hand-rolled contracts
18. ADR 022 residual checklist items still open (frontend out of scope)

### Keep

1. All nine business modules + BuildingBlocks + host Program.cs wiring
2. SharedKernel marker (until architecture policy changes)
3. All EF migrations including DropLegacySchemas
4. Committed NSwag + openapi-typescript + Kiota sources under packages
5. LHDN XSD, Scriban templates, gateway adapters (Stripe/Billplz/CHIP/Razorpay)
6. Communications Broadcasts feature stack
7. One storage presigned-url (ex-Vault utility)
8. Platform credentials (`ApiCredential`) as source of truth
9. Outbox/inbox/dead-letter workers and observability
10. Taskfile `gen*` and `api:db:migrate` / migration add tasks
11. Integration + architecture + module tests (active ones)
12. LHDN sandbox shell scripts (sans cookies)

---

## 10. Evidence appendix (key greps / files)

### Program.cs module registration (no Community/Vault)

```342:350:apps/lazuar-api/src/Lazuar.Api/Program.cs
builder.Services.AddOneModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddOpsModule(builder.Configuration);
builder.Services.AddBillingModule(builder.Configuration);
builder.Services.AddLhdnModule(builder.Configuration);
builder.Services.AddCommerceModule(builder.Configuration);
builder.Services.AddCommunicationsModule(builder.Configuration);
```

### RevenueRecognitionJob intentionally unregistered

```70:75:apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs
        services.AddHostedService<BillingInboxConsumerJob>();
        services.AddHostedService<BillingOutboxPublisherJob>();
        // RevenueRecognitionJob intentionally unregistered (C.1): deferred schedules are not
        // created from product periods yet. Keep entity/table; re-enable when amortization is wired.
        // services.AddHostedService<RevenueRecognitionJob>();
        services.AddHostedService<B2cConsolidationJob>();
```

### Community/Vault schema drop (keep migration)

```9:13:apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260704104342_DropLegacySchemas.cs
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS community CASCADE;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS vault CASCADE;");
        }
```

### TypeSpec main — no community/vault

```1:33:packages/api-spec/main.tsp
import "./common/models.tsp";
import "./modules/one/models.tsp";
// ... one, messaging, ops, commerce, communications, billing, lhdn, payments, crm, platform
// No community/vault imports
```

### Scope probe (review)

```514:519:apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs
        // Phase 1 policy probe for IntegrationPaymentsCheckoutsWrite (real M2M checkout routes land in Phase 2).
        endpoints.MapGet("/one/integrations/payments/checkouts/_scope-probe", () =>
                TypedResults.Ok(new StatusResponse { Status = "payments.checkouts:write" }))
            .RequireAuthorization("IntegrationPaymentsCheckoutsWrite")
            .RequireCors();
```

### Orphan community template names (keep until cleanup)

```71:83:apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs
    public static readonly IReadOnlyList<string> OrphanNames =
    [
        "Community Welcome",
        "Community Payment Success",
        "Event Ticket Confirmation",
        // ...
    ];
```

### Cookies secret (delete)

`scripts/lhdn_sandbox/cookies.txt` contains `lazuar_auth` JWT with SUPER_ADMIN claims.

### Gitignore relevant rules

```
dist/
[Bb]in/
[Oo]bj/
```

---

## 11. Out of scope leftovers (mentioned only)

- Frontend portal `src/modules/community/` — ADR 022 residual; not backend.
- Admin prompt-library “community plan” copy — frontend.
- ADR historical docs under `docs/architecture-decision-log/` — supersession watermarks already on 014/020; full rewrite is documentation track, not code delete.
- `apps/lazuar-api/docs/007`, `008` — still valid platform ops (outbox, password hashing); keep.

---

## 12. Conclusion

The largest **already completed** removal is Community/Vault modules themselves. Remaining dead weight on the backend/TypeSpec plane is:

1. **Noise:** stale Generated Models.cs, empty tests/fixtures, secret cookies, obsolete TypeSpec README, unused host PlatformDbContext.  
2. **Migration debt:** LHDN DeveloperApiKeys dual-path, obsolete command façades.  
3. **Deferred product:** RevenueRecognitionJob, scope-probe, broadcast credit DTO fields, template orphan cleanup.  
4. **Docs:** community-era playbooks under `apps/lazuar-api/docs/` that would mislead operators.

No empty backend module packages remain. No TypeSpec community/vault trees remain. Prefer PR A (confident deletes) before any schema-drop of `DeveloperApiKeys`.
