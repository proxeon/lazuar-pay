# 006 — Payment Webhook Idempotency & Log Backfilling

This document outlines the cut-over procedures for transferring payment gateways (Stripe and Billplz) from the legacy platform to the new Lazuar API. It ensures zero data loss, prevents double-accounting, and avoids duplicate transaction processing.

---

## 1. Webhook Transition Risks

When you update your webhook endpoint URLs inside your Stripe Dashboard or Billplz Account to point to the new Lazuar API:
* **The "In-Flight" Webhook Hazard:** Transactions initiated on the legacy system right before the switch may have their payment success webhooks delivered to the new API.
* **Duplicate Retries:** If a network blip occurs during endpoint registration, Stripe or Billplz may retry webhook deliveries, sending the same event to both the old and new systems.

If the new API processes a duplicate webhook, it will trigger a duplicate command, resulting in incorrect accounting entries or extending a user's subscription period twice for a single payment.

---

## 2. Playbook: Pre-Populating the Webhook Log

To guarantee idempotency at the database level, the Payments module uses the `payments.PaymentWebhookLogs` table. It enforces a unique index on `(Provider, EventId)`.

Before changing the webhook URLs at the payment gateway level, you must extract all processed event IDs from the legacy database for the last **30 days** and insert them into the new table.

```
┌─────────────────────────────────┐
│     Legacy Webhook Log (30d)    │
└────────────────┬────────────────┘
                 │
                 │ 1. Extract Event IDs & Providers
                 ▼
┌─────────────────────────────────┐
│     payments.PaymentWebhookLogs │
│      (Seed and Lock Index)      │
└─────────────────────────────────┘
```

### PostgreSQL Backfill Script:
Execute this migration query on the new database before switching over webhook configurations:
```sql
-- Assuming a temporary table 'legacy_webhook_events' contains raw legacy logs
INSERT INTO payments."PaymentWebhookLogs" ("Id", "EventId", "Provider", "ProcessedAt")
SELECT 
    gen_random_uuid(), -- Generate safe C# UUIDs
    l.event_id,        -- Stripe Event ID (e.g., evt_1N...) or Billplz Bill ID
    UPPER(l.gateway),  -- 'STRIPE' or 'BILLPLZ'
    l.processed_at     -- Preserves original timestamp
FROM legacy_webhook_events l
WHERE l.processed_at >= NOW() - INTERVAL '30 days'
ON CONFLICT ("Provider", "EventId") DO NOTHING; -- Enforces absolute safety
```

---

## 3. Required Webhook Metadata Schema

For the automated cross-module integration handler (`GatewayPaymentCompletedIntegrationEventHandler.cs`) to successfully match a gateway payment to an active subscription, the checkout session must be generated with strict metadata properties.

When migrating or generating checkouts, the following metadata schema is mandatory:

### A. Stripe Metadata Requirements
When calling the Stripe API, the `SessionCreateOptions` must contain these metadata keys:
```json
{
  "metadata": {
    "type": "community_subscription",
    "subscription_id": "00000000-0000-0000-0000-000000000000", // Target Subscription ID
    "tenant_id": "00000000-0000-0000-0000-000000000000"       // Organization ID
  }
}
```

### B. Billplz Reference Mapping Requirements
Because Billplz does not support arbitrary JSON metadata payloads, we map our identifiers to Billplz's native reference fields inside `BillplzGatewayAdapter.cs`:
* **`reference_1`:** Must contain the target `SubscriptionId` Guid string.
* **`reference_2`:** Must contain the hardcoded string `"community_subscription"`.

The webhook parser will reconstruct these fields back into standard metadata key-values upon receiving callbacks.
```

---

### File Path: `apps/lazuar-api/docs/007-reminder-schedule-catch-up-guard.md`

```markdown
# 007 — Reminder Schedule Catch-Up Guard

This document describes how to prevent the background reminder engine (`CommunityLifecycleJob.cs`) from spamming imported subscribers with immediate overdue notifications on day one of launching the new platform.

---

## 1. The Hazard: The "Catch-Up" Notification Storm

The background worker `CommunityLifecycleJob` runs automatically inside the `Community` module. It evaluates active subscriptions and compares their `NextRenewalDate` to the current system time (`DateTime.UtcNow`). 

If a subscription is past due, the worker loops through the active `ReminderSchedules` and raises a `SubscriptionRenewalDueDomainEvent`.

When migrating historical subscribers:
* Many active subscribers might be imported with a `NextRenewalDate` that is in the past (e.g., if they are in a grace period).
* If the database does not contain a log indicating that their billing reminders were already sent for that billing period, the background scheduler will assume no notifications were dispatched.
* **The storm occurs:** The background job will immediately execute all active schedules sequentially, sending several overdue emails or messages to the customer on the same day.

---

## 2. The Solution: The `ReminderDispatchLog` Table

The database schema provides an idempotency table to block this behavior: `community.ReminderDispatchLogs`. 

This table enforces a strict database-level unique constraint on:
```sql
UNIQUE ("SubscriptionId", "ScheduleId", "TargetRenewalDate")
```

When a reminder is dispatched, a record is locked in this table. To make the new scheduler "silent" for past periods, you must backfill this table during the data migration process.

```
                         NextRenewalDate < NOW() ?
                                    │
                                    ├──► YES: Has Dispatch Log?
                                    │           ├──► YES: Do nothing (Blocked)
                                    │           └──► NO: Trigger Email (Storm!)
```

---

## 3. Playbook: Seeding the Dispatch Logs

When importing subscription records that are past due or currently active in their billing cycles, you must generate "placeholder" dispatch logs. This tells the scheduler that all notifications for the *current* and *past* periods have already been processed.

### PostgreSQL Backfill Script:
Execute this query as the final stage of your data migration ETL pipeline:
```sql
-- For every imported subscription that has a past or current NextRenewalDate,
-- we insert placeholder logs for all active reminder schedules.
INSERT INTO community."ReminderDispatchLogs" ("Id", "SubscriptionId", "ScheduleId", "TargetRenewalDate", "DispatchedAt")
SELECT 
    gen_random_uuid() as "Id",
    s."Id" as "SubscriptionId",
    sch."Id" as "ScheduleId",
    s."NextRenewalDate" as "TargetRenewalDate",
    NOW() as "DispatchedAt"
FROM community."Subscriptions" s
CROSS JOIN community."ReminderSchedules" sch
WHERE s."Status" IN ('ACTIVE', 'PAST_DUE')
  AND s."NextRenewalDate" <= NOW()
  AND sch."IsEnabled" = true
ON CONFLICT ("SubscriptionId", "ScheduleId", "TargetRenewalDate") DO NOTHING;
```

By populating these rows, you ensure that when `CommunityLifecycleJob` boots, it will skip sending renewal reminders for the current active cycle and only trigger dispatches when the *next* renewal period arrives.
