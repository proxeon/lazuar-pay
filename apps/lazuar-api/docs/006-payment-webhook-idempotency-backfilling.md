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
    "type": "commerce_subscription",
    "subscription_id": "00000000-0000-0000-0000-000000000000", // Target Subscription ID
    "tenant_id": "00000000-0000-0000-0000-000000000000"       // Organization ID
  }
}
```

### B. Billplz Reference Mapping Requirements
Because Billplz does not support arbitrary JSON metadata payloads, we map our identifiers to Billplz's native reference fields inside `BillplzGatewayAdapter.cs`:
* **`reference_1`:** Must contain the target `SubscriptionId` Guid string.
* **`reference_2`:** Must contain the hardcoded string `"commerce_subscription"`.

The webhook parser will reconstruct these fields back into standard metadata key-values upon receiving callbacks.

---

## 4. Reminder catch-up guard — OBSOLETE

> **OBSOLETE (phase 02 maintenance):** The following section documented `CommunityLifecycleJob` and `community.ReminderDispatchLogs` / `community.Subscriptions` / `community.ReminderSchedules`. The Community module and `community` schema were removed (ADR 022; `DropLegacySchemas`). **Do not execute any `community.*` SQL.**
>
> Commerce dunning / reminder behavior is owned by **Commerce** + **Communications** today. If a catch-up storm guard is still needed for imports, rewrite against the live Commerce reminder/dunning tables and jobs — do not resurrect Community SQL.

### Historical hazard (for archive readers only)

The deleted `CommunityLifecycleJob` evaluated active subscriptions against `NextRenewalDate` and could spam imported subscribers if dispatch logs for the current cycle were missing. The old mitigation was to backfill `community."ReminderDispatchLogs"` before enabling the job.

### Example of what NOT to run

```sql
-- DO NOT RUN — community schema dropped (ADR 022)
-- INSERT INTO community."ReminderDispatchLogs" (...)
-- FROM community."Subscriptions" s
-- CROSS JOIN community."ReminderSchedules" sch
-- ...
```
