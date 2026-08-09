# 004 — Transactional Import Protocol (Bypassing Side-Effects)

> **OBSOLETE / HISTORICAL (phase 02 maintenance):** Seed steps below that target `community.*` tables describe a **deleted** module (ADR 022; schemas dropped via One `DropLegacySchemas`). Do **not** run Community plan/subscription/payment SQL against production.
>
> **Still valid principles:** silent imports (bypass MediatR/outbox), sequential dependency order, direct SQL/COPY for historical data.
>
> **Current schema targets (illustrative):** `one` (orgs/users), `crm` (profiles), `communications` (templates — not `messaging.MessageTemplates` for catalog ownership), `commerce` (products/subscriptions/payment logs), `payments` (webhook logs / tenant payment config), `billing` (ledger). Confirm table names in the live EF migrations before any ETL.

When migrating historical datasets into a production environment, you must ensure that the seeding process is "silent." This document defines the strategies used to prevent unintended side effects (such as automated emails, webhook alerts, and outbox publications) during imports.

---

## 1. The Migration Hazard: Accidental Side-Effects
If you migrate data by invoking standard application Commands (such as `RecordSubscriptionPaymentCommand`), the domain aggregates will raise Domain Events (e.g., `SubscriptionActivatedDomainEvent`). 

These events are designed to trigger automated processes:
1. They populate `OutboxMessages`.
2. Background workers publish them to the event bus.
3. Downstream modules (e.g. `Communications` / `Messaging`) may physically send welcome, success, or payment receipt emails to real customers whose data is being migrated.

**To avoid spamming historical users, you must bypass the outbox/inbox pipeline entirely during imports.**

---

## 2. Bypassing Strategies: Direct Database Seeding
To achieve complete silence during migration, you must bypass EF Core's change tracker and MediatR pipeline. The most reliable method is to perform raw bulk inserts directly to the database using Dapper or PostgreSQL COPY commands.

```
                  ┌─────────────────────────────────────┐
                  │        Migration Script (ETL)       │
                  └──────────────────┬──────────────────┘
                                     │
                        Bypasses EF Core & MediatR
                                     │
                                     ▼
                  ┌─────────────────────────────────────┐
                  │         Database Schemas            │
                  │    (Direct SQL / COPY / Dapper)     │
                  └─────────────────────────────────────┘
```

### Protocol Guidelines:
1. **Bypass DbContext:** Do not use `CommerceDbContext`, `PaymentsDbContext`, or other module contexts to run imports unless you explicitly disable domain event dispatching on them.
2. **Direct Schema Writes:** Write directly to the tables using SQL scripts or Dapper connections.
3. **No Outbox Entries:** Never insert rows into `OutboxMessages` or `InboxMessages` tables during historical data imports.

---

## 3. Strict Import Sequencing
Because modular monolithic schemas are separated cleanly and enforce local relational referential integrity, imports must follow a strict, sequential ordering. 

You cannot import a subscription until its tenant, product, and client profile exist.

```
  Step 1: Organization/Tenant Seeding (one.Organizations)
    │
    ▼
  Step 2: CRM Client Profile Seeding (crm.ClientProfiles)
    │
    ▼
  Step 3: Message Template Seeding (communications templates — catalog ownership)
    │
    ▼
  Step 4: Commerce Product Seeding (commerce products / plans)
    │
    ▼
  Step 5: Commerce Subscription Seeding (commerce subscriptions)
    │
    ▼
  Step 6: Commerce / Billing Payment Record Seeding (commerce payment logs + billing ledger as needed)
```

### Step 1: Organization/Tenant Seeding
* **Target schema:** `one` (e.g. `one."Organizations"`)
* **Action:** Import historical business entities first. This generates the unique `OrganizationId` required by all downstream multi-tenant records.

### Step 2: CRM Client Profile Seeding
* **Target Table:** `crm."ClientProfiles"`
* **Action:** Seed your client directory. Ensure each record is mapped to its matching `OrganizationId`.

### Step 3: Message Template Seeding
* **Target:** Communications-owned templates (not the deleted Community catalog; Messaging is dispatch-only).
* **Action:** Seed default templates before any job that assumes they exist.

### Step 4: Commerce Product Seeding
* **Target schema:** `commerce` (products / plan-equivalent entities — **not** `community."Plans"`)
* **Action:** Map and seed product/plan structures. Product IDs are referenced by subscriptions.
* ~~**Deleted:** `community."Plans"`~~ — do not use.

### Step 5: Commerce Subscription Seeding
* **Target schema:** `commerce` subscriptions — **not** `community."Subscriptions"`
* **Action:** Map legacy users to their products. Since `ClientProfileId` is stored as a raw `Guid` with no database-level foreign key, verify in the migration script that the referenced `ClientProfileId` exists in `crm.ClientProfiles`.
* ~~**Deleted:** `community."Subscriptions"`~~ — do not use.

### Step 6: Payment / Ledger Seeding
* **Target schemas:** Commerce payment history tables and/or `billing` ledger entries as required by current domain design — **not** `community."PaymentRecords"`
* **Action:** Seed historical transaction ledgers linked to the imported subscription IDs to maintain billing integrity.
* ~~**Deleted:** `community."PaymentRecords"`~~ — do not use.
