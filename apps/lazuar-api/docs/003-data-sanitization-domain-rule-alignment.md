# 003 — Data Sanitization & Domain Rule Alignment Playbook

> **OBSOLETE / HISTORICAL (phase 02 maintenance):** This playbook was written against the removed **Community** module aggregates (`CommunityPlan`, `CommunitySubscription`) and legacy subscription migration. Community/Vault schemas were dropped (ADR 022; `DropLegacySchemas` migration). Do **not** treat `community.*` or Community type names as live targets.
>
> **Current equivalent:** Commerce product/subscription aggregates and domain invariants under `Modules/Commerce`. Rewrite any active ETL against Commerce + CRM + Payments before use.

This playbook outlines the strategies, cleaning rules, and mapping matrices required to migrate legacy database records into the modular monolith without violating aggregate invariants or breaking business rules.

---

## 1. Why Data Sanitization is Mandatory
Legacy databases often suffer from "data rot" (orphaned records, inconsistent statuses, or invalid parameters). The legacy system tolerated these anomalies because its validation checks were loose or non-existent.

In the modular monolith, domain rules are hardcoded as structural invariants inside aggregate constructors (historically Community plan/subscription; today **Commerce** product/subscription equivalents). If you try to load, seed, or map corrupt legacy data directly into these domain objects, the system will throw a `BusinessRuleValidationException`, halting execution.

---

## 2. Invariant Rules and Pre-Migration SQL Audits

Before initiating a migration run, you must execute the following database sanitization audits on your legacy SQL database to discover and correct invariants.

### Rule A: Grace Period Must Not Be Negative (`GracePeriodMustBePositiveRule`)
* **Legacy Anomaly:** Some legacy plans might have `-1` or NULL values to indicate infinity, or corrupt negative integer values.
* **Sanitization Script:** Run this query to locate and correct invalid grace periods before importing:
  ```sql
  -- Identify corrupt plans (legacy source DB — not a live community.* schema)
  SELECT id, slug, grace_period_days FROM legacy_plans WHERE grace_period_days < 0;

  -- Fix them by converting negative/invalid values to a safe default (e.g., 0 days)
  UPDATE legacy_plans SET grace_period_days = 0 WHERE grace_period_days < 0;
  ```

### Rule B: Invalid Subscription State Transitions
* **Domain Rule:** A subscription designated as `IsReminderOnly = true` must never transition to the `EXPIRED` or `SUSPENDED` states; it must remain `PAST_DUE` indefinitely.
* **Sanitization Script:** Run this query to locate legacy subscriptions that violate this rule and flag them appropriately before migration:
  ```sql
  -- Identify reminder-only subscribers that are in an expired state
  SELECT id, email, status FROM legacy_subscriptions 
  WHERE is_reminder_only = true AND status IN ('expired', 'suspended');

  -- Fix: Force them to remain in 'past_due' status
  UPDATE legacy_subscriptions SET status = 'past_due' 
  WHERE is_reminder_only = true AND status IN ('expired', 'suspended');
  ```

---

## 3. Legacy Status Mapping Matrix

Legacy subscription statuses must be mapped cleanly to the strict state machine defined by the **target** subscription aggregate (today: Commerce subscription domain, not the deleted Community module).

```
                  ┌─────────────────────────────────────┐
                  │          Legacy Statuses            │
                  │ (trialing, unpaid, canceled, etc.)  │
                  └──────────────────┬──────────────────┘
                                     │
                                     ▼
                  ┌─────────────────────────────────────┐
                  │            Domain Status            │
                  │ (PENDING, ACTIVE, PAST_DUE, etc.)   │
                  └─────────────────────────────────────┘
```

Use the following mapping matrix during your ETL (Extract, Transform, Load) transformation stage:

| Legacy Status | Condition / Business Context | Target Domain Status |
| :--- | :--- | :--- |
| `incomplete` | Checkout session created but no payment recorded. | `PENDING` |
| `trialing` | Active period with no balance due. | `ACTIVE` |
| `active` | Normal, fully paid billing cycle. | `ACTIVE` |
| `unpaid` / `past_due` | Subscription is past its renewal date but remains within the plan's grace period. | `PAST_DUE` |
| `past_due` | Subscription is past its renewal date AND grace period has elapsed. | `EXPIRED` |
| `canceled` | Subscription cancelled by customer or admin (can still access until period ends). | `CANCELLED` |
| `unpaid` | Grace period has elapsed and no payment is forthcoming. | `EXPIRED` |

---

## 4. Run-Time Sanitization Strategy
When mapping legacy fields to the API's commands, you must sanitize raw inputs inline to ensure safety. For example, strip whitespaces and normalize emails:
```csharp
var command = new CreatePlanCommand(
    OrganizationId: legacyOrgId,
    Slug: legacySlug.Trim().ToLowerInvariant(), // Normalize Slug format
    Name: legacyName.Trim(),
    Price: Math.Max(0, legacyPrice), // Enforce non-negative price
    Interval: legacyInterval == "yearly" ? "yr" : "mo", // Normalize code mapping
    // ...
);
```
