# 09 — Data: greenfield vs migrate (and the ban on a second org table)

**Family:** 013-prods  
**Paper:** 09 — data plane for production-ready new Pay, then replace the old tree  
**Date:** 21 August 2026  
**Type:** Analysis only. **Do not implement** from this file. **Do not** add a Pay `organizations` table, a Pay `users` table, a Hub→Pay ETL, a dual-write outbox into `lazuar_mvp`, or a dump/restore of Hub Postgres into focused Pay “to unblock S1.”  
**Sibling identity:** `/Users/akmalfirdaus/Code/lazuar/lazuar-one`  
**Old tree (negative example + name inventory only):** `apps/lazuar-api/Modules/`  
**New host (empty of durable money):** `apps/lazuar-pay`

**Assigned slice:** whether production replace requires moving Hub Postgres data into focused Pay, and how orgs map to One tenants. This paper does **not** design gateway adapters ([06](./06-money-rails.md)) or UI ([04](./04-merchant-frontend.md)–[05](./05-checkout-frontend.md)). Host connection-string / secrets / deploy of the new DB belongs with [03](./03-host-production-seams.md). Dual-run of **processes** (old 8080 + new 8081) belongs with [02](./02-replace-old-cutover.md). Journal + `RCPT-` + SST **judgment** belongs with [07](./07-fulfillment-ledger-docs.md); this paper only names the small tables that judgment has to live in.

---

## SHAs considered (this write)

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `6f866ff0` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| **lazuar-one** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

011 papers cited below are dated 20 August 2026 on this Pay tree. 012 papers on this tree were written against Pay `6ca8f19f`; the **locks** (One tenant id is `org_id`, no second org table, two planes) did not move. Focused Pay on `6f866ff0` has whoami, org-ready, and an **in-memory** checkout fixture. It still has **no Postgres**.

**Honesty lock (inherited):** One staging proof is **NOT PASSED**. Pay is Consumer-0. Merchants are One humans. Buyers are not.

---

## 0. How to read this paper

This paper answers one question and refuses a second:

1. **Does replacing Hub in production require copying `lazuar_mvp` into focused Pay?**
2. **May Pay grow an `organizations` (or `org_map`) table so that copy has somewhere to land?**

The answers are already written as product law:

| Source | What it contributes here |
|--------|--------------------------|
| [011 00-why-leave.md](../011-new-lazuar-pay/00-why-leave.md) | Nine module schemas are the disease. Workers needed `IgnoreQueryFilters` because ambient tenant was empty. Cross-schema was a negotiation. Steal **judgment**, not folders. |
| [011 01-product.md](../011-new-lazuar-pay/01-product.md) | Dogfood: Ada on One, buyer pays without a One account, one `RCPT-` + balanced journal. Buyer plane is a small payer profile **inside Pay**. |
| [011 02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) | One tenant id **is** Pay `org_id` unless Pay writes a reason to map otherwise. Do not invent a second membership system. Do **not** call `POST /platform/tenants`. Cardholders never become Zitadel humans. |
| [011 03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) / [12](../011-new-lazuar-pay/12-first-slice-tracker.md) | Fail: Pay password form or second org table. Fail: buyer as Zitadel human. Create workspace = `POST /tenants`. |
| [011 04-linux-shape.md](../011-new-lazuar-pay/04-linux-shape.md) | One app, one database, `recordPayment()` updates ledger and receipt in the **same transaction**. Per-module schemas were the expensive shape first. |
| [011 07-separate-vs-one-binary.md](../011-new-lazuar-pay/07-separate-vs-one-binary.md) | Pay ↔ One is the justified split. Dual-write of money across processes is the tax we are trying not to re-buy **inside Pay**. |
| [011 11-checklist.md](../011-new-lazuar-pay/11-checklist.md) **NP-XX-014**, **NP-XX-009**, **NP-XX-013**, **NP-XX-023**, **NP-BUY-002** | Second org table = refuse. Per-module schemas / inbox-as-self-talk = refuse. Zitadel per cardholder = refuse. `POST /platform/tenants` = refuse. Payer profile is Pay. |
| [012 06-tenant-org.md](../012-one-to-pay/06-tenant-org.md) | Unrolls NP-XX-014. Mapping table is last resort. S1 money rows copy One’s uuid; they do not FK a Pay org row. Whoami is One `/me`. |
| 013 README | New stack must become production-ready, then **replace** old Hub — without cloning the cathedral and without Hub feature-parity as the bar. |

It is **not** an implement order. It is **not** an ETL spec. It does not flip tracker Status. It does not invent a Hub dump job. Sibling 013 papers `01`–`08` and `10` were **not on disk** at this write (`plans/013-prods/` contained only `README.md`); where this paper names “paper 07’s intent,” that intent is the 013 index line *Same-handler journal + `RCPT-`; SST judgment* plus 011 § Fulfillment / Money / Documents. If 07 later names different columns, **change the names, not the ban on an org table.**

---

## Binding answers (read this first)

These are the decisions this paper exists to keep from being “clarified” into a data-migration program.

| # | Decision | Lock |
|---|----------|------|
| 1 | **Production replace does not require moving Hub Postgres into focused Pay.** Dogfood is Ada on One. Greenfield is honest. | this paper §4, 011/01 |
| 2 | **One tenant UUID is Pay `org_id`.** Same bytes. No Pay-side surrogate. No Hub `Organization.Id` as the isolation key. | NP-ONE-009, NP-XX-014, 012/06 |
| 3 | **Pay does not have an `organizations` / `tenants` / `workspaces` / `org_map` table** in the production-ready first database. Merchant existence lives in One. | NP-XX-014 |
| 4 | **Do not call `POST /platform/tenants`.** That is One’s staff directory. Create workspace is `POST /tenants` with Ada’s Bearer, or pick a membership from `GET /me`. | NP-XX-023, 011/02 |
| 5 | **Buyers are Pay payer profiles, not One users.** If a future paper ever copies Hub CRM clients, they become Pay `payers` rows, **not** Zitadel humans, **not** `one.GlobalUsers`. Strip `GlobalUserId`. | NP-BUY-001/002, NP-XX-013 |
| 6 | **Old Hub DB is not One’s DB.** Hub: Postgres **16**, database **`lazuar_mvp`**, compose service `db` / container `lazuar-db`, host **5432** (or a remap). One: Postgres **17.10**, databases **`lazuar` / `zitadel` / `openfga`**, container `lazuar-postgres`. On the whoami laptop, One publishes **`lazuar` on host 5435** (`POSTGRES_PUBLISHED_PORT=5435` in One `.env`). **Do not merge those databases.** | 012/05, One `deploy/dev/postgres/init/01-databases.sql`, this paper §1.4 |
| 7 | **Steal judgment, not schemas.** Nine module schemas are the disease. New Pay is **one** database, **one** schema (recommend `public`), **one** migration timeline. | NP-XX-009, 011/00, 011/04 |
| 8 | **S1 inserts happen when Ada creates a product or a charge lands**, keyed by the One tenant id already in the path. Do not seed catalog on `tenant.created`. | 012/06 §6.3 vs NP-ONE-019 |
| 9 | **Dual-write of Hub rows and Pay rows for the same money is forbidden as a long-term strategy.** A short dual-run of **processes** is paper 02. | this paper §7, 011/07 |
| 10 | **If a named production Hub tenant must keep history, that is a separate migration project** with an explicit mapping (Hub org id → One tenant id, cookie user → Zitadel `sub`, Guid document numbers → `RCPT-` / `PENDING`). It is not implied by “replace the old tree.” As of this write, whether any such tenant exists is **unknown**. | this paper §5, §10 |

If an implementation PR adds `CREATE TABLE organizations` “for the migration,” that PR fails this paper, fails 012/06, and fails the first-slice lock: *“Pay password form or second org table.”*

---

## 1. Method / SHAs

### 1.1 What was opened

| Tree | What |
|------|------|
| Pay `6f866ff0` | `apps/lazuar-api/Modules/*/Infrastructure/*DbContext.cs` and `HasDefaultSchema`; CRM `ClientProfiles`; Commerce `Subscriptions` / `CheckoutSessions`; Billing `LedgerEntries` / `LedgerLines`; Payments checkout + webhook table names; `Directory.Build.props` (no schema list); Hub `appsettings*.json` connection strings; focused Pay `appsettings.json` (no `ConnectionStrings`); `CheckoutStore` in-memory; compose `docker-compose.yml` Postgres 16 / `lazuar_mvp`; `deploy/prod/*` Neon + `hub.lazuar.com`; 011 `00`/`01`/`02`/`03`/`04`/`07`/`09`/`11`/`12`; 012 `05`/`06`/`10`; 005 `r02-notes.md` (prod SQL **blocked**); `TODO.md` |
| One `0f79fe4` | `LazuarDbContext` (product DB `lazuar` only); `Tenant.cs`; compose Postgres 17.10; `deploy/dev/postgres/init/01-databases.sql` (`zitadel`, `openfga`, `lazuar`); `deploy/backups/README.md`; local `.env` `POSTGRES_PUBLISHED_PORT=5435` |

`Directory.Build.props` under `apps/lazuar-api/` pins TFM `net10.0`, nullable, central package versions. **It does not name schemas.** Schema names come from each module `DbContext.HasDefaultSchema(...)`. The historical nine-name list also appears in 005 `r16-notes.md` as the old `ModuleSchemas` array: `one`, `messaging`, `payments`, `crm`, `ops`, `billing`, `lhdn`, `commerce`, `communications`.

Focused Pay `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` has **zero** `PackageReference` (no Npgsql, no EF). `appsettings.json` has `One:BaseUrl` only.

### 1.2 What “production replace” is allowed to mean here

013’s problem statement: the new stack becomes something you can run in production and then **replace** the old Hub backend and frontends — without cloning the cathedral, without retargeting `lazuar-ops` / `lazuar-portal` at 8081, and without Hub feature-parity as the bar.

Replace is a **cut of traffic and process**, not a **copy of rows**. Paper 02 owns kill criteria and dual-run of processes. This paper owns: which database the new process talks to, which rows that database is allowed to contain on day one of dogfood, and why Hub’s nine schemas are not an import spec.

### 1.3 What was not done

- No live `psql` against Hub Neon / `hub.lazuar.com`. 005 R02 already recorded staging and prod SQL as **blocked** from this workstation (2026-08-09). This write does not invent tenant counts.
- No live `psql` against One `lazuar` on 5435. Database **name** and **published port** are taken from compose + `.env` + whoami topology, not from a dump of Ada’s tenant row.
- No design of Stripe/CHIP adapters, Vite OIDC, or hosted-page UX.

### 1.4 Three Postgres worlds (do not mash)

| World | Engine (compose) | Host port (this laptop / defaults) | Database name(s) | Who writes it |
|-------|------------------|--------------------------------------|------------------|---------------|
| **Hub (old Pay)** | `postgres:16-alpine`, container `lazuar-db`, volume `lazuar-pay_pgdata` | **5432** (`task infra:up` / `docker compose` `db`) | **`lazuar_mvp`** | `apps/lazuar-api` nine DbContexts. `ConnectionStrings:Default`, `TenantConnection`, and `MessagingConnection` are **the same string** in `appsettings.json` and compose. |
| **One product + identity** | `postgres:17.10-alpine`, container `lazuar-postgres`, volume `lazuar-one_lazuar_pgdata` | Default **5432**; **whoami proof on this machine: `POSTGRES_PUBLISHED_PORT=5435`** | **`lazuar`** (product), **`zitadel`**, **`openfga`**. Bootstrap DB is `postgres`. Init: `CREATE DATABASE zitadel; CREATE DATABASE openfga; CREATE DATABASE lazuar;` | One API `LazuarDbContext` → `lazuar` only. Comment on that type: *Never connect product code to `zitadel` or `openfga` SQL schemas.* Zitadel and OpenFGA have their own DSNs **inside** the compose network on container port 5432. |
| **Focused Pay (new)** | **None.** Compose still points at `apps/lazuar-api`. README: “Compose still points at `apps/lazuar-api`. Swap later.” | n/a | **Does not exist.** Checkout is `ConcurrentDictionary` in `CheckoutStore`. | Future S1 money. Paper 03 owns how the host gets a connection string. |

012/05 already said: volume names do not collide; data is not shared; you cannot point One at `lazuar_mvp` or Pay at `lazuar` without a deliberate (and version-skewed: PG 16 vs 17) mash-up. **Do not.** The whoami remap to **5435** exists *because* Hub wanted 5432. That is evidence the two clusters are already fighting over a port, not that they should share a catalog.

Hub `TenantConnection` is a leftover name. There is no per-tenant database. Nine **schemas** in one `lazuar_mvp` pretended to be nine services. Messaging’s `TenantReplicas` table then copied org name/slug into a tenth place. That is the disease 00-why-leave named.

One `appsettings.Development.json` still prints `Host=localhost;Port=5432;Database=lazuar`. Compose `.env` on the whoami machine publishes 5435. Host-run One API must override `ConnectionStrings__Lazuar` to `Port=5435` or whoami’s `/me` cannot see tenants. That override is an **operator** fact, not a reason to put One’s rows on Pay’s future DSN.

### 1.5 What focused Pay actually persists today

`CheckoutSession` is a CLR record: `Id`, `OrgId`, `Amount`, `Currency`, `Status`, URLs, `CreatedAt`. `OrgId` is already the One tenant string (fixture `POST /v1/checkouts`). `CheckoutStore` is explicit: *“In-memory fixture store. Not a ledger. Replace when money is real.”*

So: **production dogfood needs a new database.** It does **not** need Hub’s database.

---

## 2. Inventory of old module schemas / DbContexts (names)

Nine modules, nine PostgreSQL schemas, nine EF `DbContext` types, nine migration trains, nine copies of `OutboxMessages` / `InboxMessages`. `PlatformDbContext` stamps `IMustHaveTenant.OrganizationId` from ambient `IExecutionContextAccessor.TenantId` and attaches a global filter `OrganizationId == ExecutionContext.TenantId` (fail-closed: empty ambient matches **no** rows; workers must `IgnoreQueryFilters` **and** predicate on org). That is 00-why-leave in code.

`apps/lazuar-api/Directory.Build.props` does **not** list these names. They are on the DbContexts:

### 2.1 The nine

| # | Module folder | DbContext | `HasDefaultSchema` | Product tables (EF `ToTable` / `DbSet`; PascalCase as stored) | Outbox/Inbox in that schema |
|---|---------------|-----------|--------------------|---------------------------------------------------------------|------------------------------|
| 1 | `Modules/One` | `OneDbContext` | **`one`** | `Organizations`, `GlobalUsers`, `TenantMemberships`, `TenantAppEntitlements`, `WorkspaceInvitations`, `TenantWebhookEndpoints`, `WebhookDeliveryOutboxes`, `ApiCredentials`, `AuditEvents` | yes |
| 2 | `Modules/Commerce` | `CommerceDbContext` | **`commerce`** | `Products`, `ProductPrices`, `Coupons`, `Subscriptions`, `Orders`, `CheckoutSessions`, `ChargeAttemptLogs`, `DunningCampaigns`, `DunningSteps`, `ReminderDispatchLogs`, `TransactionLogs`, `Disputes`, `InvoiceReminderDispatchLogs` | yes |
| 3 | `Modules/Billing` | `BillingDbContext` | **`billing`** | `LedgerEntries`, `LedgerLines`, `DeferredRevenueSchedules`, `TenantCreditBalances`, `CreditLedgers`, `CreditHolds`, `CreditDeductionIdempotencyLogs`, `TenantBillingProfiles`, `DocumentSequences`, `WorkspaceSaasSubscriptions` | yes |
| 4 | `Modules/Payments` | `PaymentsDbContext` | **`payments`** | `TenantPaymentConfigurations`, `PaymentWebhookLogs`, `IntegrationCheckoutSessions` | yes |
| 5 | `Modules/Lhdn` | `LhdnDbContext` | **`lhdn`** | `TenantConfigs`, `TaxDocuments`, `WebhookSubscriptions`, `DeveloperApiKeys`, `IdempotencyLogs`, `MsicCodes`, `CountryCodes`, `TaxTypes`, `TinValidateCaches` | yes |
| 6 | `Modules/CRM` | `CrmDbContext` | **`crm`** | `ClientProfiles` (owned address columns on the same table) | yes |
| 7 | `Modules/Communications` | `CommunicationsDbContext` | **`communications`** | `MessageTemplates`, `SuppressionEntries`, `Broadcasts`, `TenantEmailConfigurations` | yes |
| 8 | `Modules/Messaging` | `MessagingDbContext` | **`messaging`** | `TenantReplicas`, `MessageDeliveryLogs` | yes |
| 9 | `Modules/Ops` | `OpsDbContext` | **`ops`** | `Conversations`, `Messages` | yes |

That is **nine**. Not eight, not ten. 011/09: *“Commerce / Billing / Payments / One / Lhdn / CRM / Communications / Messaging / Ops each have a schema and workers.”* 005 r16 listed the same nine strings as `ModuleSchemas`.

Each schema also carries EF’s `__EFMigrationsHistory` for that context. Dead-letter / retry are **columns** on `OutboxMessages` / `InboxMessages` (`Status`, `AttemptCount`, `NextAttemptAt`, `Error`), not a tenth product schema. There are nine pairs of box tables because each module was treated as a future service. NP-XX-009: do not rebuild that as the way Pay talks to itself.

### 2.2 Names this paper will keep repeating (money + buyer)

These are the Hub tables people will try to “just pg_dump” into new Pay. **Names only** here; mapping problems are §5.

| Hub relation | Schema | Job people think it has |
|--------------|--------|-------------------------|
| `commerce.Subscriptions` | commerce | Recurring access after pay. FK-shaped `ClientProfileId`, `ProductId`, `OrganizationId`. Vault token ids. Dunning snapshot JSON. |
| `commerce.CheckoutSessions` | commerce | Hosted / pay-link session. `DocumentNumber` (max 40), `IdempotencyKey`, `AdHocLineItems` jsonb, `MetadataJson`. |
| `payments.IntegrationCheckoutSessions` | payments | **Second** checkout table for M2M/integrator. Not Commerce’s session. Dual session SoT inside one database. |
| `billing.LedgerEntries` + `billing.LedgerLines` | billing | Double-entry. `TaxInvoiceId` is a **legacy dual-use** column (receipt #, LHDN UUID, consolidation ref). `CustomerDocumentNumber` is the later honest field. |
| `billing.DocumentSequences` | billing | Per-org prefix sequences (`RCPT-2026` → `RCPT-2026-00001`). Judgment to steal. |
| `crm.ClientProfiles` | crm | Buyer plane. `GlobalUserId` nullable — **staff identity mixed into buyer row**. Unique `(OrganizationId, Email, Phone)`. TIN / IdType / IdValue / address / marketing consent. |
| `one.Organizations` | one | Hub’s org SoT: `Id` Guid v7, `Slug`, `Name`, `ExternalProduct` + `ExternalOrgId` (Aura-class map), branding. **Not** One-the-product `tenants.id`. |
| `one.GlobalUsers` | one | Email, name, **BCrypt `PasswordHash`**, `SecurityStamp`, `IsSystemAdmin`. Hub cookie humans. |
| `payments.TenantPaymentConfigurations` | payments | Encrypted BYOK gateway keys. Steal the *job* (encrypted secrets keyed by org), not the ciphertext (wrong KMS, wrong org id). |
| `payments.PaymentWebhookLogs` | payments | Idempotency `(OrganizationId, EventId)` plus `BusinessKey`. Steal the *rule* `(org_id, provider, event_id)`. |

### 2.3 Cross-schema was the architecture

`apps/lazuar-api/docs/001-cross-module-communication.md`: *No Direct DB Joins. No Cross-Schema Foreign Keys.* Commerce must not SQL-join `crm`. Fulfillment is therefore: write Commerce → publish event → hope Billing’s inbox runs → hope Lhdn subscribed the live type and not the parked one → hope the worker used `IgnoreQueryFilters`. 011/04: that cost is the tax.

005 spent a wave closing live cross-schema SQL (L-01…L-07). The **shape** remains: nine schemas, nine boxes. New Pay does not import that shape even if every leak is “fixed.”

### 2.4 Hub identity tables are a refuse template (not a source of tenant ids)

`one.Organizations.Id` is minted by Hub (`Guid.CreateVersion7()` in `Organization` ctor). One-the-product mints `tenants.id` the same *algorithm* (also Guid v7) in a **different database**. Equality of algorithm is not equality of row. `ExternalProduct` / `ExternalOrgId` is Hub mapping **out** to Aura (and friends). It is not a mapping **in** from Lazuar One.

`one.GlobalUsers.PasswordHash` is why NP-XX-007 exists. New Pay holds no password store.

`crm.ClientProfiles.GlobalUserId` is the plane mix 012/06 already banned: a buyer row pointing at a staff identity table.

### 2.5 Dual checkout, dual keys, dual documents — already inside Hub

Before anyone proposes “migrate Hub,” notice Hub already dual-writes **itself**:

| Dual | Left | Right |
|------|------|-------|
| Checkout session | `commerce.CheckoutSessions` | `payments.IntegrationCheckoutSessions` |
| Machine keys | `lhdn.DeveloperApiKeys` (legacy; dual-read **removed** R05; table drop was still R06) | `one.ApiCredentials` (`sk_test_` / `sk_live_`, SHA-256, no pepper) |
| Document identity | `billing.LedgerEntries.TaxInvoiceId` (legacy dump) | `CustomerDocumentNumber` + `LhdnDocumentUuid` + `ConsolidationStatus` |
| Org replica | `one.Organizations` | `messaging.TenantReplicas` (id/name/slug/active copy) |

New Pay does not inherit a single one of those pairs.

---

## 3. What production dogfood actually needs in the NEW db

Paper 07’s assigned intent (013 index): **same-handler journal + `RCPT-`; SST judgment.** 011/01 dogfood sentence: Ada signs in through One, pastes CHIP or Stripe keys, a buyer pays on the hosted page **without** a One account, Pay shows one `RCPT-` and a **balanced journal**, webhook retry no-ops, invited MEMBER sees ops, VIEWER cannot charge.

012/06 §6.2 already listed when Pay-side rows appear. This section **names tables** for that list. Names are a proposal. One schema (`public`). Snake_case. Column **`org_id uuid`** on every merchant-scoped table, equal to One `tenants.id`, **no** `REFERENCES organizations(id)` because that table does not exist. Isolation is `WHERE org_id = :pathTenantId` after One `authz/check`. Jobs carry `org_id` on the payload. No global filter that treats missing tenant as all rows.

### 3.1 Tables for S1 dogfood (small list)

| Proposed table | Appears when | Why it exists | Hub analog to steal *judgment* from (do not copy schema) |
|----------------|--------------|---------------|----------------------------------------------------------|
| `products` | Ada creates a catalog item | Name, MYR, active. `org_id`. | `commerce.Products` (exclusive SST on **unit** lives with price, not here as LHDN type 01) |
| `product_prices` | Ada adds monthly/yearly (or one-off) | Amount, interval, currency `MYR`. | `commerce.ProductPrices` |
| `pay_links` | Ada creates a shareable link **or** omit and use checkout only | Public id the hosted page knows. `org_id` on the row so the buyer route does not take a tenant header. | Commerce pay-link / checkout bootstrap |
| `checkout_sessions` | Merchant or buyer opens pay | Amount, currency, `org_id`, status `open\|paid\|expired`, success/cancel URLs, idempotency key, payer email/name (NP-BUY-001), quantity. **Not** a second integrator table. | `commerce.CheckoutSessions` — **one** table, not also `payments.IntegrationCheckoutSessions` |
| `gateway_credentials` | Ada pastes Stripe **or** CHIP/Billplz | Encrypted BYOK. `org_id`. Rotate/disable without delete. Ciphertext is Pay’s KMS, not Hub’s `Kms__MasterKey` blob copied across. | `payments.TenantPaymentConfigurations` job |
| `webhook_events` | Provider POST hits Pay | Idempotency `(org_id, provider, event_id)`. Optional business key. Retry no-ops. | `payments.PaymentWebhookLogs` rule NP-GW-006 |
| `charges` | Gateway says money moved (or offline mark-paid if v1 keeps it) | Attempt/success/fail, provider refs, amount, fee if **actually** sent (`unknown` ≠ 0). | `commerce.ChargeAttemptLogs` + `TransactionLogs` **judgment**; not two SoTs |
| `subscriptions` | First successful recurring pay, **same handler** as journal + receipt (NP-FUL-001) | Buyer access = this row (NP-FUL-002). `org_id`, `payer_id`, product/price, period, status. **No** `client_profile_id` into CRM. **No** One user id. | `commerce.Subscriptions` lifecycle judgment (do not invent PAST_DUE without a real failed charge — that is 07/V1) |
| `journal_entries` | Same transaction as paid | Header: `org_id`, timestamp, `reference_type`/`reference_id` unique per org (idempotent with webhook). | `billing.LedgerEntries` **without** `TaxInvoiceId` / LHDN columns |
| `journal_lines` | Same transaction | Double-entry: cash, revenue, tax, fee. Insert-only (Hub already treated `LedgerLine` Modified→Added). | `billing.LedgerLines` + `AccountTypes` judgment |
| `receipts` | Same transaction | Commercial document. Number `RCPT-…` or `PENDING` if the sequence is unavailable. **Never** a UUID. **Never** titled Tax Invoice. Snapshot `merchant_name` at issue time (012/06 §7.2.9). | `DocumentSeries.Receipt` + `CustomerDocumentNumber`; not `lhdn.TaxDocuments` |
| `document_sequences` | First receipt (or first quote later) | `(org_id, prefix)` unique. Prefix `RCPT-2026` in MYT. | `billing.DocumentSequences` + `DocumentSeries.cs` |
| `payers` | First checkout with email/name, or first paid | Small profile: email, name, optional phone. `org_id` of the **merchant**. **No** `one_user_id`, **no** `global_user_id`, **no** password. | `crm.ClientProfiles` stripped (NP-BUY-002) |
| `audit_events` | Charge, key change (and later refund) | Same DB transaction as the write (NP-AUD-001/003). Not an audit service. | `one.AuditEvents` **job**, not Hub cookie actor model |
| `tax_profiles` | When Ada fills SST / legal name so V1 can fail-closed (NP-MON-004). **May wait until V1** if S1 dogfood is a known non-SST or explicit SST rate on the product. | `org_id` PK. SST registration known/unknown/registered. **Not** merchant identity. **Must not grow `members[]`.** | `billing.TenantBillingProfiles.SstRegistrationNumber` judgment |

That is the production-ready **money** database. S0 (whoami / ready) needs **zero** of these tables. S1 needs them because in-memory checkout is not a ledger.

### 3.2 Allowed to wait until V1 / soon (still not Hub import)

| Table (proposal) | Wave | Notes |
|------------------|------|-------|
| Dunning / PAST_DUE state | soon (NP-SOON-004) | Steal engine **judgment**, not `DunningCampaigns` JSON snapshots |
| Refund / dispute rows | V1 | Or journal reversals only; do not double-reverse (NP-MON-005/006) |
| Transactional mail log | S1/V1 | Same process; not a Notify database (NP-XX-019) |
| Quote / proforma | soon | `QT-` series; not tax invoice |

### 3.3 Explicitly not in the new database on day one

| Tempting table | Verdict |
|----------------|---------|
| `organizations` / `tenants` / `workspaces` / `org_map` | **Refuse** NP-XX-014 |
| `users` / `global_users` / `memberships` / `invites` | **Refuse** — One |
| Empty `products` seeded on `tenant.created` | **Refuse** 012/06 §6.3 |
| `lhdn.*` anything | **Refuse** NP-XX-001 |
| `credits_*` / `tenant_credit_balances` | Hub prepaid wallet for WhatsApp/LHDN.submit — not Pay GMV |
| `workspace_saas_subscriptions` | Hub charging the workspace for Hub — not merchant SaaS |
| Nine `outbox_messages` / `inbox_messages` pairs | Pay may have **one** outbox later for **One webhooks outbound** or mail; it does not talk to itself through nine boxes |
| `tenant_replicas` | Dual org SoT |
| `conversations` / ops LLM | Out of product |

### 3.4 One database, one migration timeline

011/04: *Schema change is “the database at commit N.” Not Commerce migration 40 plus Billing migration 22 plus a dual-use column because the other module cannot join.* `TaxInvoiceId` was that tax made visible.

New Pay: one EF (or one migrate tool) on one connection string, e.g. `ConnectionStrings:Pay` / `PAY_DATABASE_URL`, database name **`lazuar_pay`** (proposal — any name **except** `lazuar_mvp` and **except** One’s `lazuar`). Do not reuse Hub’s three aliases (`Default` / `TenantConnection` / `MessagingConnection`). Do not put Pay tables inside One’s `lazuar` so “Ada is already there.” Ada’s **tenant id** is already there; her **charges** must not be.

Paper 03 owns pooling, TLS, Neon vs compose Postgres version for the new DSN. This paper only forbids merging catalogs.

### 3.5 Alignment with paper 07 (intent)

When 07 specifies the journal:

- **Same handler** as webhook success: insert `charges` + `subscriptions` (or one-off complete) + `journal_entries`/`journal_lines` + `receipts` in **one** `BEGIN`…`COMMIT`.
- Receipt number from `document_sequences` using `DocumentSeries`-class judgment: never print a UUID; missing number is `PENDING` (`DocumentSeries.CustomerFacingNumber` already returns `"PENDING"` when the only other value `LooksLikeGuid`).
- SST: exclusive on the **unit**, then × seats; fail closed if `tax_profiles` cannot decide (NP-MON-003/004). That is a **column on price / line**, not an LHDN XML document.

If 07 later wants `journal_lines` only (no header table), collapse `journal_entries`. If 07 wants receipt number **on** the journal header and no `receipts` table, that is still one document series — **not** a reason to keep `TaxInvoiceId`.

---

## 4. Greenfield recommendation vs migrate-existing-Hub-merchants

### 4.1 Who is the customer of this rewrite?

011/01 dogfood, 012/06 Ada, 012/10, One login demo (`ada@acme.test` in the whoami runbook): the customer of **this** rewrite is **Ada on One**. She creates or picks a One tenant. That uuid is `org_id`. She pastes keys. A buyer who is **not** Ada pays. Pay writes money.

She is **not**:

- A Hub `GlobalUser` with cookie `lazuar_auth` role `CLIENT` and membership `ADMIN` after `X-Tenant-Id`.
- The seeded Hub demo `founder@acme.test` / slug `acme` in `lazuar_mvp` (012/05: that slug is a Hub seed; One `/me` will never list it).
- An Aura integrator org bound through `Organization.ExternalOrgId`.
- A cardholder.

013’s replace bar is **not** “every Hub row has a successor.” It is “new Pay is production-ready for that dogfood, then Hub goes dark” (paper 02/10). Hub feature-parity is refused by the 013 index.

**Greenfield is honest.** Ada has no Hub ledger she is entitled to see in Pay. Creating a product in Pay is not “losing” `commerce.Products` for a workspace One never heard of.

### 4.2 When migration would become a project (not implied)

Only if a **named** production Hub tenant must keep **history** (past receipts, live subscriptions, vaulted tokens, disputes) **and** continue as the same merchant after cutover. That sentence needs a **name** (legal entity, Hub `Organizations.Id`, volume). As of this write, the repo does not contain that name. See §10.

If that project is ever funded, it is **not** `pg_dump lazuar_mvp | psql lazuar_pay`. It is a one-shot with explicit maps (§5). It still does not create `pay.organizations`. It still does not call `POST /platform/tenants`. It still does not mint Zitadel humans for `crm.ClientProfiles`.

### 4.3 Mapping problems you inherit the moment you say “move Hub”

These are why “just migrate” is a program, not a weekend:

**1. Hub org id vs One tenant id**

Hub `one.Organizations.Id` is a Guid v7 Hub minted. One `tenants.id` is a Guid v7 One minted in database `lazuar`. They will not match. Slug is not a key (mutable; 012/06). `ExternalProduct`/`ExternalOrgId` maps Hub → Aura, not Hub → One. There is no column on Hub `Organizations` that *is* a One tenant uuid.

You cannot `INSERT INTO products (org_id) SELECT "OrganizationId" FROM commerce."Products"` and then call One `authz/check` on that uuid. One will 403: not a member of a tenant that does not exist.

**2. Hub cookie users vs Zitadel `sub`**

Hub staff: `GlobalUsers.Id` + BCrypt + JWT cookie `CLIENT`. One staff: Zitadel human, `GET /me.user_id` is Zitadel `sub` (string). Email match is a **hint**, not a join key (SSO, aliases, unverified Hub emails, `PLATFORM_ADMIN_EMAILS`). Invites, memberships, and `lzr_sk_` live in One already; they are not in Hub’s `TenantMemberships` (`ADMIN`/`MEMBER`/`VIEWER` vs One `owner`/`admin`/`member`).

Pay must not import `GlobalUsers`. Ada signs in through `:5175` and **exists**. Engineers she needs are One copy-link invites, not a password-hash copy.

**3. Guid document numbers**

00-why-leave: *Hub SaaS PDF sliced a Guid because that handler did not use the merchant numbering helper sitting one folder over.* `DocumentSeries.LooksLikeGuid` + `CustomerFacingNumber` → `"PENDING"` when the only stored value is a UUID. `LedgerEntry.TaxInvoiceId` is documented as legacy dual-use (receipt #, LHDN UUID, consolidation ref). Migrating `TaxInvoiceId` into Pay `receipts.number` would reprint UUIDs and fail NP-DOC-002.

A migration would have to: prefer `CustomerDocumentNumber` if it is a real `RCPT-`/`INV-` series; else allocate a **new** `RCPT-` in Pay (and keep Hub’s uuid in a **snapshot column** `legacy_hub_ref`, not as the public number); never copy LHDN UUID into the commercial number.

**4. Two checkout tables, two subscription FKs**

A Hub “in-flight pay” might sit in `commerce.CheckoutSessions` **or** `payments.IntegrationCheckoutSessions`. Subscriptions point at `ClientProfileId` in `crm`. New Pay subscriptions point at `payers.id`. CRM `GlobalUserId` must be dropped, not rewritten to Zitadel `sub`.

**5. Vaulted tokens and BYOK ciphertext**

`Subscriptions.VaultedCustomerId` / `VaultedTokenId` are gateway customer/PM ids. They might still work **if** the same Stripe/CHIP account is reused **and** the merchant pastes the same live keys into Pay’s `gateway_credentials` (new encryption). Copying Hub `TenantPaymentConfigurations.ApiKey` ciphertext without Hub’s `Kms__MasterKey` is garbage. Copying the master key into Pay to decrypt Hub blobs is a secrets incident. **Re-paste keys** is the honest greenfield path. Migration of ciphertext is a named crypto project.

**6. Buyer access tokens**

00-why-leave: portal tokens were subscription-shaped, so a paid quote with no `Subscriptions` row could not open documents. New Pay: buyer access is Pay subscription/session (NP-FUL-002). Do not import Hub magic-link subjects.

**7. Live vs parked money**

Billing README: `InvoiceIssued` subscribed but never constructed; `ManualPaymentRecorded` parked; `RevenueRecognitionJob` not registered; `ApiCreditPurchased` subscribed never published. A dump of `billing.LedgerEntries` includes whatever those gaps produced. Replaying Hub outbox into Pay would replay **parked** types into a kernel that must not know them.

**8. Credits, Hub SaaS, LHDN, WhatsApp**

Those balances are not GMV. Migrating them would smuggle a second product into Pay.

### 4.4 Recommendation (normative)

| Situation | What to do |
|-----------|------------|
| Dogfood / first production Pay (Ada on One, CHIP or Stripe, one buyer) | **Greenfield.** Empty `lazuar_pay`. `org_id` = One tenant id from `/me`. No Hub DSN in the Pay process. |
| Hub remains up during Pay dogfood | **Dual-run of processes** (paper 02). **Not** dual-write of rows (§7). Hub `lazuar_mvp` stays Hub’s. |
| A named Hub merchant must keep history | Write a **new** paper, name the tenant, fund an ETL. Maps in §5. Still no `organizations` table. Still no Zitadel buyers. Still no LHDN XML. Until that paper exists, **do not** build the ETL “just in case.” |

Greenfield is not throwing away judgment. SST unit math, fail-closed SST, `RCPT-` year series in MYT, wrap-rails, webhook idempotency, “VALID only if a provider said so” — those are **code notes**, already listed in 011/01. They are not row copies.

---

## 5. Mapping Hub Organization → One tenant (cannot be implicit)

### 5.1 The equality that is allowed

```text
Pay.org_id  ==  One.tenants.id  ==  JSON tenants[].id  ==  path {tenantId}
```

012/06 §3. That equality is **not**:

```text
Pay.org_id  ==  Hub.one.Organizations.Id          -- FALSE
Pay.org_id  ==  Hub.commerce.Products.OrganizationId  -- FALSE (same Hub uuid, still not One)
Pay.org_id  ==  One.tenants.slug                  -- FALSE (mutable)
Pay.org_id  ==  One.tenants.zitadel_org_id        -- FALSE (NP-XX-017 / NP-ONE-020)
Pay.org_id  ==  Hub.ExternalOrgId                 -- FALSE (Aura)
```

There is **no implicit function** from a Hub org uuid to a One tenant uuid. Algorithms matching (both Guid v7) is a coincidence of `CreateVersion7()`, not a join.

### 5.2 How Ada gets an `org_id` without a map

1. Ada is a One human (OIDC `:5175`).
2. `GET /me` lists `tenants[]` or she `POST /tenants` `{ name, slug }` with **her** Bearer and an Idempotency-Key.
3. 201 body `id` is a guid string. Pay stores that string as `org_id` on the first product or charge.
4. Pay **does not INSERT** an org row. Pay **does not** call `POST /platform/tenants`.

If she already has a tenant from `lazuar-app` `:5174`, picking it is enough (011/12 step 3). Whoami on Pay `6f866ff0` already returns `orgs[].org_id` equal to One `tenants[].id`. That is the map. It is identity.

### 5.3 What Hub thought an org was (so you can see the mismatch)

`Modules/One/Domain/Organization.cs`:

- `Id` Guid v7 at construct
- `Name`, `Slug` (unique), `IsActive`, branding
- `ExternalProduct` + `ExternalOrgId` unique filtered index — *“One workspace per (product, external org)”* in the **Hub** sense of “One module,” not Lazuar One
- Memberships in `TenantMemberships` with Hub staff roles
- Entitlements in `TenantAppEntitlements` (`OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN`)
- System tenant folklore `00000000-…-0001` (012/06 §10.6) — new Pay has **no** system org in the money DB

Lazuar One `Tenant`:

- `Id` Guid v7 in `TenantService.CreateAsync`
- `Slug`, `Name`, `Status` (`provisioning|active|failed|suspended|deleted`)
- `ZitadelOrgId` — One’s map to **Zitadel**, which Pay must never hold
- Memberships in One `memberships` (snake_case table in DB `lazuar`)
- No Hub entitlement flags. Pay is one product.

You cannot “sync slug acme.” Hub demo slug `acme` and a One tenant slug `acme` can exist on the same laptop in **different databases** and refer to **different humans**.

### 5.4 If a future migration paper must connect a Hub org to One

The operator action is **human and One-native**, not a SQL join:

1. Identify the Hub org (`one.Organizations.Id`, slug, legal name from `TenantBillingProfiles` if present).
2. Ensure the merchant **owner** exists as a One human (they sign in; you do **not** hash-import `GlobalUsers`).
3. Create or pick a One tenant via `POST /tenants` / `GET /me` **as that human**. Do **not** `POST /platform/tenants`.
4. Record the pair **outside Pay SQL** for the duration of the ETL (runbook, signed sheet): `hub_organization_id → one_tenant_id`. If you must put it in Postgres, 012/06 §7.4 last-resort shape is `org_id uuid PRIMARY KEY` **still One’s id**, plus maybe `legacy_hub_org_id uuid UNIQUE` on a **one-shot staging table you drop**. A surrogate `pay_org_id serial` is the full anti-pattern.
5. Rewrite every imported money row’s isolation key to **One’s** uuid. Do not keep Hub’s uuid as `org_id` and “map at read time.”
6. Buyers: insert `payers` from `crm.ClientProfiles` where email is not anonymized (`deleted_{guid}@localhost`). Set `GlobalUserId` **null** in the new row (do not copy the column). Do not `POST` Zitadel humans.
7. Staff: do not import `TenantMemberships`. Invite via One copy-link.
8. Document numbers: §4.3.3.
9. Keys: merchant re-pastes. Do not import `DeveloperApiKeys` or Hub `ApiCredentials`. New machine access is One `lzr_sk_` (012/08).
10. Drop the staging map table when the cut is done so NP-XX-014 cannot rot into dual SoT.

Until steps 1–3 have a **named** tenant, this sequence is hypothetical. It is written so nobody “helpfully” ships `pay.org_map` as a permanent product table.

### 5.5 Header, path, Hub `X-Tenant-Id` — three different mistakes

| Incoming | Meaning |
|----------|---------|
| Pay path `{tenantId}` / `{orgId}` | One tenant uuid. Authz SoT. |
| `X-Lazuar-Tenant-Id` | Hint to One `/me` only. Never Pay SQL. |
| Hub `X-Tenant-Id` / `X-Tenant-Slug` | **Dead.** Do not accept as Pay org. Ambient `Items["TenantId"]` is how empty-tenant workers happened. |

A migration that leaves Hub uuids in Pay paths will 403 against One forever, or — worse — someone will add a map and authorize One id A while reading Hub id B (012/06 §7.2.6).

---

## 6. What NEVER to migrate

Even if §5’s hypothetical ETL is funded, these do not cross the wire.

### 6.1 LHDN XML (and the rest of homemade MyInvois)

`lhdn.TaxDocuments.RawXmlContent` plus hash, IRBM uuid, submission uid, poll state, `IsTestMode`. `lhdn.TenantConfigs` PFX ciphertext and genesis seed row. `TinValidateCaches`. MSIC/country/tax **code catalogs**. Consolidation fields on `billing.LedgerEntries`. NP-XX-001: homemade LHDN / XML / UBL / consolidation job = refuse. Sandbox VALID was **never captured** (`docs/honesty/lhdn-sandbox-valid.md`). Pay’s commercial document is `RCPT-`, not UBL. A tax **provider** later receives amount + buyer; Pay never owns types 03–14 or XAdES (NP-LAT-001).

Migrating XML would smuggle a second ledger (document numbers versus UUIDs, park status with no collector) into the kernel 00-why-leave left.

### 6.2 Parked events and the nine boxes

Do not dump `*.OutboxMessages` / `*.InboxMessages`. They are the in-process bus: parked publishers, unused subscribers, dead letters, `InvoiceIssued` (subscribed, never constructed), `ManualPaymentRecorded` (contract-only), `ApiCreditPurchased` (subscribed, never published), `RevenueRecognitionJob` (not in DI). Replaying them into new Pay recreates MediatR cathedral as a data problem.

Pay may later have **one** outbox for **outbound** HTTP (One webhooks you send, mail). That is a new table, empty.

### 6.3 Hub passwords and Hub humans

`one.GlobalUsers.PasswordHash`, verification and reset hashes, `SecurityStamp`. Cookie `lazuar_auth` / `lazuar_admin_auth`. JWT role `CLIENT` vs membership `ADMIN` (00-why-leave: register said `ADMIN` in JSON and stamped `CLIENT` on the cookie). NP-XX-007, NP-XX-008.

Buyers are not in this table. Staff are One/Zitadel. There is nothing to import that Pay is allowed to store.

### 6.4 Dual API keys

`lhdn.DeveloperApiKeys` **and** `one.ApiCredentials` (`sk_test_` / `sk_live_`, SHA-256, no pepper, product scopes `payments.checkouts:*`). Dual-read was an explicit maintenance program (004 phase 03, 005 R01–R06). Residual LHDN-only keys 401 after One-only middleware. R02 **could not count prod rows**.

New Pay does not mint `sk_*` (collision with Stripe secrets — 012/08). Machine access to Pay is One `lzr_sk_` forwarded as Bearer. Migrating Hub key hashes would: (a) keep the wrong prefix, (b) keep the wrong hash, (c) bind them to Hub org ids, (d) invite dual-read “for cutover.” Forbidden.

### 6.5 WhatsApp (and the credits wallet that billed it)

`Messaging:WhatsAppEnabled` defaults **false** in Hub prod `env.example`. Console transport. Dunning steps with `WhatsAppBody`. `Credits:Costs:WhatsAppSend`. `messaging.MessageDeliveryLogs` channel `WHATSAPP`. NP-XX-004: WhatsApp dunning = refuse. 011/01: leave WhatsApp out until a provider has a reason.

Do not migrate delivery logs, suppression-for-WA, or `TenantCreditBalances` / `CreditLedgers` / `CreditHolds`. Those credits were prepaid **Hub** units for LHDN.submit and WhatsApp, not GMV.

### 6.6 Everything else that is a second product or a replica

| Never | Why |
|-------|-----|
| `one.Organizations` / `TenantMemberships` / `WorkspaceInvitations` / `TenantAppEntitlements` | Identity + entitlements. One is SoT. NP-XX-014. |
| `messaging.TenantReplicas` | Org copy. Dual SoT. |
| `ops.Conversations` / `Messages` | LLM ops console. Not Pay. |
| `communications.Broadcasts` / Hub `MessageTemplates` as a CMS | Vitamin. Transactional copy can be code. |
| `billing.WorkspaceSaasSubscriptions` | Hub billing **itself**. |
| `billing.DeferredRevenueSchedules` | Job parked; no shipping claim. |
| `lhdn.WebhookSubscriptions` | Tax-document callbacks. |
| System tenant `…0001` / genesis LHDN config seed | 012/06 §10.6. |
| R2 objects whose only job is LHDN XML or Hub SaaS PDF of a Guid | Commercial `RCPT-` PDFs, if any, are **regenerable** from Pay rows. |
| Hub `Jwt__Secret` / cookie sessions | Wrong IdP. |

### 6.7 Judgment you **do** steal (not as rows)

From 011/00 and 011/01, restated so “never migrate” is not read as “never learn”:

- Exclusive SST on the unit, then × seats.
- Fail closed when SST registration is unknown.
- Document number is never a UUID; missing is `PENDING`.
- VALID means a tax system said VALID.
- One write path for cash; wrap-rails; `unknown` fee ≠ 0.
- Webhook idempotency on `(tenant, provider, event_id)`.
- Two planes: merchant staff in One; payer in Pay.

---

## 7. Dual-write is forbidden as a long-term strategy; short dual-run of processes is paper 02

### 7.1 Two different ideas

| Idea | Meaning | Allowed? |
|------|---------|----------|
| **Dual-run of processes** | For a bounded window, Hub still listens (8080 / `hub.lazuar.com`) **and** focused Pay listens (8081 / future Pay origin). Different databases. Traffic split is a **product/ops** choice (paper 02: who is sent where, kill criteria, when Hub goes dark). | **Short.** Paper 02. |
| **Dual-write of rows** | The same economic event inserts (or updates) Hub `commerce`/`billing`/`payments` **and** Pay `charges`/`journal_*`/`receipts`. Usually via outbox, “sync job,” or Pay calling Hub HTTP on success. | **Forbidden** as a strategy. Not a cutover tactic that “we’ll turn off later” without a written kill date **and** a single SoT for cash. |

011/07: every extra split is write locally → outbox → at-least-once consume → idempotency, or you double-grant / double-journal. That is what Hub already paid **inside one process** with nine inboxes. Doing it **across** Hub and Pay for the same charge is the cathedral plus a network.

### 7.2 Why dual-write of money cannot be the bridge

1. **Two ledgers.** Webhook retries, “Pay succeeded / Hub inbox lagged,” parked Hub handlers, and `IgnoreQueryFilters` workers will disagree. Support will not know which `RCPT-` is real. NP-GW-006 (retry no-ops) cannot hold across two databases without a distributed lock you will not build correctly.
2. **Two org ids.** Hub write keys `OrganizationId` (Hub uuid). Pay write keys `org_id` (One uuid). Dual-write **forces** the mapping table NP-XX-014 exists to kill.
3. **Two buyer planes.** Hub may attach `ClientProfile.GlobalUserId`. Pay must not. The sync job will “helpfully” create One users for cardholders (NP-XX-013).
4. **Two key rings.** Hub `sk_live_` vs One `lzr_sk_` vs Stripe `sk_live_`. Dual-write of credentials is how you leak Hub KMS into Pay.
5. **No kill switch.** Dual-read of API keys needed an explicit program (004/005) with dates and Q8 residual counts. Dual-write of **cash** has no equivalent honesty file that will be more fun. Teams keep the bridge because “one more tenant hasn’t cut over.”
6. **NP-FUL-001.** First successful pay writes subscription + ledger in the **same handler**. A second handler in Hub is a second writer.

### 7.3 What a short dual-run may do (pointers, not a design)

Paper 02 owns this. Constraints from **this** paper so 02 cannot “need” dual-write:

- Hub keeps `lazuar_mvp`. Pay keeps `lazuar_pay` (new). One keeps `lazuar` on its cluster (**5435** on the whoami laptop; whatever staging publishes).
- A merchant is **either** live on Hub **or** live on Pay, not both for the same charge. If Ada dogfoods Pay, she is an One tenant that Hub never had. If a Hub merchant is not named for migration, they stay on Hub until Hub is killed or they **re-onboard** greenfield.
- Do not point `lazuar-ops` at 8081 (013 index). Do not dual-write so old UI can show new charges.
- Gateway webhooks: one public URL per merchant at a time. Two URLs both journaling is dual-write by another name.

### 7.4 Dual-write that already failed inside Hub (do not export)

API key dual-read, Messaging replica of Organization, `TaxInvoiceId` vs `CustomerDocumentNumber`, Commerce checkout vs Payments integrator checkout, `TenantAppEntitlement` vs actual rows, `TenantUpdatedIntegrationEvent` that Messaging subscribed and One-the-module never published (012/06). Exporting any of these as “Pay will subscribe to Hub outbox” is repeating 2026.

---

## 8. Backup / restore for the new Pay DB

### 8.1 What the asset is

A **new** PostgreSQL database (proposed name `lazuar_pay`) containing only §3 tables (and later V1 adds). It is not:

- a schema inside `lazuar_mvp`
- a schema inside One `lazuar`
- a Neon branch of Hub prod
- a logical replica of Hub

Hub prod today: `deploy/prod/README.md` + `env.example` — dedicated VPS, Caddy, **Neon** Npgsql strings for `ConnectionStrings__Default` / `TenantConnection` / `MessagingConnection` (same DB, nine schemas). Paper 03 decides whether new Pay prod is also Neon, a second Neon project, or compose Postgres. This paper requires: **separate database (preferably separate cluster/project)** from both Hub and One.

One already splits planes: product `lazuar` vs `zitadel` vs `openfga`, and says SQL restore alone is not full recovery (pepper, masterkey, PAT). Pay should copy that **honesty**, not those databases.

### 8.2 What must be in the backup set

| Asset | In `pg_dump` of `lazuar_pay`? | Notes |
|-------|-------------------------------|-------|
| Money tables (§3) | Yes | Source of truth for charges, journal, receipts, payers |
| `gateway_credentials` ciphertext | Yes, but **useless** without Pay’s encryption key | Vault the KMS/master key separately (Hub used `Kms__MasterKey`; do not reuse Hub’s key) |
| One tenant uuids on `org_id` | Yes as columns | Restore does **not** recreate One tenants. If One wipe tombstoned a tenant, Pay rows are **history**, not a live merchant (012/06 §8.3). Do not CASCADE delete charges because One wiped. |
| Pay process config | No | DSN, `One:BaseUrl`, OIDC `client_id` — vault / env |
| One HMAC / `lzr_sk_` Pay holds | No | 011/02 secrets table |
| Hub `lazuar_mvp` dump | **Never restore into Pay** | Wrong schemas, wrong org ids, passwords, XML |
| One `lazuar` dump | **Never restore into Pay** | Memberships are not charges; PG 17 vs whatever Pay runs; NP-XX-014 by accident |

### 8.3 How to take and prove a backup (operator shape)

Steal the **shape** of One `deploy/backups/` (inventory + `pg_dump` gzip + manifest + scratch restore guard), not the three-DB list.

1. Backup-only role: `CONNECT` + `SELECT` on `lazuar_pay` (not superuser if avoidable).
2. Artifact: `$BACKUP_DIR/lazuar_pay-YYYYMMDDTHHMMSSZ.sql.gz` + manifest (db name, bytes, SHA). Copy **off-host**.
3. Restore drill: only onto a name containing `scratch` (One’s script guard is the right instinct). `CONFIRM_SCRATCH=yes`. Never restore over live primary without a written freeze.
4. After restore: Pay process against scratch DSN; `GET /v1/health` does **not** need One; a whoami still calls **live** One (identity was not in the dump). A charge list is `WHERE org_id = <One uuid>` — if that tenant still exists in One, staff can open it; if not, rows remain for forensics.
5. Pre-migration gate (Pay’s own EF migrations, not Hub’s): backup taken, off-box, rollback = previous Pay binary, not auto-down-migrate.

RPO/RTO: One’s backup README is honest — **24h** unless continuous/snapshot is actually wired; do not claim 1h PITR because Neon marketing says so until a drill exists. Paper 03 / 10 may pick numbers; this paper forbids **lying** about Hub dumps as Pay PITR.

### 8.4 Hub backups are not Pay backups

`TODO.md` §2 “Migration (Pay DB / Neon)” is **Hub EF** adding `commerce.Subscriptions.MetadataJson`. It is not this program. Do not run Hub migrators against `lazuar_pay`. Do not run Pay migrators against `lazuar_mvp`. Do not “restore Hub Neon snapshot into Pay Neon folder” as a shortcut to dogfood.

If Hub Neon is ever decommissioned, keep a **cold** Hub dump for legal/forensics **outside** the Pay cluster. That dump is not a Pay restore source.

### 8.5 Local DX

Whoami: One Postgres **5435** / db `lazuar`; Pay **no db**. S1 local: start a **third** database (compose service name must not be `lazuar-db` if Hub compose still exists; do not steal 5432 from One when One is on 5435 — pick a free host port, e.g. 5436, documented in paper 03/05). Database name `lazuar_pay`. Do not `CREATE SCHEMA commerce`.

---

## 9. Anti-goals

| If you… | You have failed this paper |
|---------|----------------------------|
| `pg_dump lazuar_mvp \| psql lazuar_pay` (or Neon branch-from-Hub) | §4 greenfield; nine schemas are the disease |
| `CREATE TABLE organizations` / `tenants` / `workspaces` / `org_map` as product SoT | NP-XX-014, 012/06 |
| Generate a Pay uuid and store One’s id beside it | 012/06 §7 |
| Set `org_id` = Hub `Organizations.Id` because “it’s already a uuid” | §5.1 |
| Call One `POST /platform/tenants` to “provision Pay orgs in bulk” | NP-XX-023 |
| Import `GlobalUsers` / password hashes / `lazuar_auth` | NP-XX-007/008 |
| Create Zitadel humans from `crm.ClientProfiles` | NP-XX-013; buyers are `payers` |
| Keep `ClientProfiles.GlobalUserId` on Pay payers | Mixed planes |
| Import `lhdn.TaxDocuments.RawXmlContent` / UBL / consolidation | NP-XX-001 |
| Print Hub `TaxInvoiceId` UUIDs as `RCPT-` | NP-DOC-002 |
| Dual-write Hub ledger and Pay journal for the same webhook | §7 |
| Dual-read Hub `sk_*` and One `lzr_sk_` in Pay | §6.4, 012/08 |
| Point Pay’s DSN at One `Database=lazuar` (5435 or otherwise) | Merge identity and money; NP-XX-014 by accident |
| Point Pay’s DSN at `lazuar_mvp` and “only use commerce + billing” | Still Hub org ids; still nine boxes |
| Nine schemas / nine outboxes in new Pay | NP-XX-009 |
| Seed products on `tenant.created` | 012/06 §6.3 |
| WhatsApp templates, credits wallet, Hub SaaS subscription rows | NP-XX-004, §6.5–6.6 |
| Ambient `IgnoreQueryFilters` tenancy copied from `PlatformDbContext` | 00-why-leave; explicit `WHERE org_id` |
| Retarget `lazuar-ops` / `lazuar-portal` at 8081 so you “don’t need a new DB” | 013 index; UI is papers 04–05, new apps |
| Block Pay on counting Hub prod tenants | §10 unknown is allowed; greenfield does not wait |
| Design Stripe/CHIP adapters in this file | Paper 06 |
| Design merchant Vite / checkout Vite in this file | Papers 04–05 |

---

## 10. Open questions

### 10.1 Is there any production Hub tenant that must move?

**Unknown.** Evidence searched; no census found.

| Source | What it shows | What it does not show |
|--------|----------------|------------------------|
| `deploy/prod/README.md`, `deploy/prod/docker-compose.yml`, `env.example` | A **path** to run Hub on a VPS at `https://hub.lazuar.com` with **Neon** and GHCR images `lazuar-hub-api` / ops / portal / superadmin / developers. Single API replica because workers live in-process. | Whether that VPS is serving **paying** merchants today; how many `one.Organizations` rows exist; whether any org is Ada-class vs Aura-integrator vs empty seed. |
| `TODO.md` | Operator checklist: wait for GHCR deploy, health 200, then **Hub** EF column `MetadataJson`. Public host `hub.lazuar.com`. | Tenant list. |
| 005 `r02-notes.md` (2026-08-09) | Staging SQL **blocked**. Prod SQL **blocked**. Local optional inventory ran against `lazuar_mvp` on host **5433** (a remap of **Hub** Postgres, not One’s `lazuar`). *Do not invent staging/prod counts.* | Prod `Organizations` / `Subscriptions` / `LedgerEntries` counts. |
| `docs/payments-integration-quickstart.md` | Aura is **one example integrator**, not a prerequisite. `ExternalProduct` + `external_org_id`. | Aura production org id, or a promise Aura must keep Hub history inside new Pay. |
| Hub `DemoTenant` in `appsettings.Development.json` | Local `founder@acme.test` / slug `acme`. | Not production. |
| One whoami path | Ada as One human; tenants in **`lazuar` on 5435**. | No Hub org. |

Until ops runs a read-only count on Hub Neon (`SELECT COUNT(*) FROM one."Organizations";` plus live `commerce."Subscriptions"` by status) **or** names a merchant, this paper’s working assumption is: **nobody named must be ETL’d.** Greenfield stands. If someone later names a tenant, they fund §5; they do not silently add `org_map`.

### 10.2 Other unknowns (do not block greenfield)

| Question | Why it can wait |
|----------|-----------------|
| Exact production DSN vendor for `lazuar_pay` (Neon vs VPS Postgres vs One’s PG 17) | Paper 03. Constraint here: separate from `lazuar_mvp` and from One `lazuar`. |
| Host port for local Pay Postgres when One holds 5435 and Hub wants 5432 | Paper 03/05. |
| Whether S1 requires `tax_profiles` before first CHIP dogfood | Paper 07 + NP-MON-004 (V1). S1 can fail-closed by refusing SST-unknown merchants or by explicit product SST fields. |
| Whether `receipts` is a table or a numbered journal header | Paper 07. Either way: no UUID, no Tax Invoice title, no LHDN XML. |
| Whether Hub Neon will be retained as cold archive after Hub process kill | Paper 02/10. This paper: cold archive ≠ Pay restore source. |
| One staging proof still NOT PASSED | Identity honesty is One’s. Pay still greenfields against One HTTP. |

### 10.3 What is **not** an open question

- One tenant id is Pay `org_id`.
- No second org table.
- No `POST /platform/tenants`.
- Buyers are Pay payers, not Zitadel humans.
- Hub DB ≠ One DB; do not merge.
- Nine module schemas are not the new shape.
- Dual-write of cash is not a cutover strategy.
- LHDN XML, parked boxes, Hub passwords, dual API keys, WhatsApp — never migrate.

---

## 11. Fail modes for implementers (checklist form)

| PR smell | Failure |
|----------|---------|
| `HasDefaultSchema("commerce")` in `apps/lazuar-pay` | Nine-schema disease imported |
| `ProjectReference` to `Modules.*` “for the entities” | Cathedral as a library |
| Migrator host job `SqlHubToPayStore` | §4 without a named tenant paper |
| `org_id` column type `text` storing slugs | 012/06 §3.2 |
| `REFERENCES one.tenants` across process DBs | Cannot FK another product’s database; copy the uuid |
| Checkout fixture kept as sole store in “production-ready” | Not a ledger; paper 07 cannot journal |
| Restoring Hub dump “to have something to show in ops” | Wrong ids, passwords, XML |

---

## 12. What this paper does not decide

- Gateway adapter code, webhook URL layout, wrap-rails matrix (paper 06).
- Merchant Vite OIDC chrome or checkout page (papers 04–05).
- Pay host `Program.cs` DSN plumbing, health-vs-ready, secrets files (paper 03).
- Dual-run calendar, Hub kill switch, DNS cut (paper 02).
- Exact journal column list and SST rounding (paper 07) — only that those rows live in **one** new DB keyed by One’s uuid.
- One HMAC consumer / `tenant.suspended` (paper 08) — only that suspend is **not** a Pay org row flip.
- Go vs C# kernel (011/05). If Pay is rewritten in Go, this paper’s **data** locks still hold.
- Whether Pay BFF caches `GET /tenants/{id}` name onto the receipt at print time (allowed denormalization) vs live GET.

---

## 13. Evidence index

| Claim | Evidence |
|-------|----------|
| Pay HEAD / One HEAD | `git rev-parse`: Pay `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` on `feat/012-connect-one`; One `0f79fe4f6503847881286ead2e7e57b7c7dc1808` on `main` |
| Nine schemas | `HasDefaultSchema` in nine `*DbContext.cs`; 005 r16 `ModuleSchemas` list; 011/09 shape table |
| `Directory.Build.props` has no schema names | `apps/lazuar-api/Directory.Build.props` |
| Hub DSN = `lazuar_mvp` three times | `apps/lazuar-api/src/Lazuar.Api/appsettings.json` `ConnectionStrings`; `docker-compose.yml` `POSTGRES_DB: lazuar_mvp`, Postgres 16 |
| Focused Pay has no DB | `apps/lazuar-pay/.../appsettings.json` (no ConnectionStrings); `Lazuar.Pay.csproj` no Npgsql; `CheckoutStore.cs` in-memory |
| One DBs `lazuar`/`zitadel`/`openfga` | `lazuar-one/deploy/dev/postgres/init/01-databases.sql`; `LazuarDbContext` remarks |
| Whoami Postgres **5435** | `lazuar-one/.env` `POSTGRES_PUBLISHED_PORT=5435`; 012/05 default collision on 5432; Pay whoami README: Hub off, One on 8080 |
| CRM buyers + `GlobalUserId` | `Modules/CRM/Domain/ClientProfileEntity.cs`; table `crm.ClientProfiles` |
| Subscriptions / checkout / ledger names | `CommerceDbContext`, `BillingDbContext`, `PaymentsDbContext` `ToTable` |
| Guid document numbers / PENDING | `DocumentSeries.LooksLikeGuid` / `CustomerFacingNumber`; `LedgerEntry.TaxInvoiceId` remarks; 011/00 Guid PDF |
| Parked events | `Modules/Billing/README.md` parked list; `ParkedBillingWritersTests` |
| Dual keys | 005 r02/r17; `lhdn.DeveloperApiKeys` vs `one.ApiCredentials` |
| WhatsApp off | `deploy/prod/env.example` `Messaging__WhatsAppEnabled=false`; NP-XX-004 |
| IgnoreQueryFilters / empty tenant | `PlatformDbContext.ConfigureGlobalFilter`; 011/00 |
| NP-XX-014 / no platform tenants POST | 011/11 refuse rows; 011/02 tenancy table |
| Prod tenant census | **Absent**; R02 prod blocked; `deploy/prod` is Hub runbook not a roster |
| Tenant replica dual SoT | `messaging.TenantReplicas` |
| LHDN XML column | `TaxDocument.RawXmlContent` |
| One tenant shape | `lazuar-one/.../Domain/Tenants/Tenant.cs`; snapshot `ToTable("tenants")` |

---

## 14. One-paragraph restatement

Replacing Hub in production means standing up a **new** money database for focused Pay, not pouring `lazuar_mvp` into it. The customer of this rewrite is Ada on One; her tenant uuid **is** `org_id`; whoami already proved that without Postgres on Pay’s side. One’s product DB is `lazuar` (on this laptop, published at **5435**); Hub’s is `lazuar_mvp` (Postgres 16, nine schemas); they must never merge. Buyers become Pay `payers`, never Zitadel humans. There is no second org table, no `POST /platform/tenants`, no dual-write of cash, and no import of LHDN XML, parked inboxes, Hub password hashes, dual API keys, or WhatsApp. If a named Hub merchant must keep history, that is a later project with an explicit Hub-org→One-tenant map — and as of this write, whether any such merchant exists is unknown. Steal SST and `RCPT-` judgment; leave the nine schemas behind.

---

## 15. Suggested first Pay migration (informative, not a patch)

When paper 03 adds a DSN and paper 07 agrees names, the first migration is boring on purpose:

```text
CREATE DATABASE lazuar_pay;          -- not lazuar, not lazuar_mvp
-- public schema only

-- every money table: org_id uuid NOT NULL
-- no organizations table
-- no users table
-- indexes: (org_id, …) for list; unique (org_id, idempotency_key) on checkouts
-- unique (org_id, provider, event_id) on webhook_events
-- unique (org_id, prefix) on document_sequences
```

Empty. Ada’s first `POST /v1/{tenantId}/products` inserts row one. That is production-ready data. Everything else is a museum.
