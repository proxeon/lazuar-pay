# 004 — Transactional Import Protocol (Bypassing Side-Effects)

When migrating historical datasets into a production environment, you must ensure that the seeding process is "silent." This document defines the strategies used to prevent unintended side effects (such as automated emails, webhook alerts, and outbox publications) during imports.

---

## 1. The Migration Hazard: Accidental Side-Effects
If you migrate data by invoking standard application Commands (such as `RecordSubscriptionPaymentCommand`), the domain aggregates will raise Domain Events (e.g., `SubscriptionActivatedDomainEvent`). 

These events are designed to trigger automated processes:
1. They populate `OutboxMessages`.
2. Background workers publish them to the event bus.
3. The `Messaging` module receives them and physically sends welcome, success, or payment receipt emails to real customers whose data is being migrated.

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
1. **Bypass DbContext:** Do not use `CommunityDbContext` or `PaymentsDbContext` to run imports unless you explicitly disable domain event dispatching on them.
2. **Direct Schema Writes:** Write directly to the tables using SQL scripts or Dapper connections.
3. **No Outbox Entries:** Never insert rows into `OutboxMessages` or `InboxMessages` tables during historical data imports.

---

## 3. Strict Import Sequencing
Because modular monolithic schemas are separated cleanly and enforce local relational referential integrity, imports must follow a strict, sequential ordering. 

You cannot import a subscription until its tenant, plan, and client profile exist.

```
  Step 1: Organization/Tenant Seeding (tenant.Organizations)
    │
    ▼
  Step 2: CRM Client Profile Seeding (crm.ClientProfiles)
    │
    ▼
  Step 3: Message Template Seeding (messaging.MessageTemplates)
    │
    ▼
  Step 4: Community Plan Seeding (community.Plans)
    │
    ▼
  Step 5: Community Subscription Seeding (community.Subscriptions)
    │
    ▼
  Step 6: Community Payment Record Seeding (community.PaymentRecords)
```

### Step 1: Organization/Tenant Seeding
* **Target Table:** `tenant."Organizations"`
* **Action:** Import historical business entities first. This generates the unique `OrganizationId` required by all downstream multi-tenant records.

### Step 2: CRM Client Profile Seeding
* **Target Table:** `crm."ClientProfiles"`
* **Action:** Seed your client directory. Ensure each record is mapped to its matching `OrganizationId`.

### Step 3: Message Template Seeding
* **Target Table:** `messaging."MessageTemplates"`
* **Action:** Seeding messaging templates is required before plans can establish reminder configurations.

### Step 4: Community Plan Seeding
* **Target Table:** `community."Plans"`
* **Action:** Map and seed your tier/plan structures. The unique plan ID will be referenced by active subscriptions.

### Step 5: Community Subscription Seeding
* **Target Table:** `community."Subscriptions"`
* **Action:** Map legacy users to their plans. Since `ClientProfileId` is stored as a raw `Guid` with no database-level foreign key, you must verify inside your migration script that the referenced `ClientProfileId` exists in the `crm.ClientProfiles` table.

### Step 6: Community Payment Record Seeding
* **Target Table:** `community."PaymentRecords"`
* **Action:** Seed historical transaction ledgers linked to the imported `SubscriptionId` to maintain billing integrity.
