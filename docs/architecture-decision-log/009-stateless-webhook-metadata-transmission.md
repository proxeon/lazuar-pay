
# ADR 009: Stateless Webhook Metadata Transmission via Query Strings

**Status:** Accepted  
**Date:** June 2026  

## Context

The `Payments` module is designed to be completely **stateless** regarding checkout sessions. It acts strictly as an infrastructure gateway orchestrator. It does not store "Pending Orders" or "Pending Checkouts" in the database. 

Instead, the domain context (e.g., `type=community_subscription`, `subscription_id=XYZ`) must be passed to the payment gateway during checkout generation, and the payment gateway must return that exact context in its Webhook/Callback upon a successful payment.

While modern gateways like Stripe natively support a `metadata` dictionary that persists throughout the transaction lifecycle, regional gateways (such as Billplz) do not. They strip out custom references (like `reference_1` and `reference_2`) from their Server-to-Server (S2S) webhook payloads.

If a webhook arrives without this metadata, the `Payments` module publishes a context-less `GatewayPaymentCompletedIntegrationEvent`, which downstream domain modules (like `Community`) will silently drop, resulting in unfulfilled orders.

## Decision

To maintain the stateless nature of the `Payments` module without relying on the gateway's native metadata capabilities, **we will encode critical context directly into the Webhook Callback URL as Query String parameters.**

## Implementation Rules

When implementing or modifying a Payment Gateway Adapter (`IPaymentGatewayAdapter`), developers must adhere to the following pipeline:

### 1. URL Appending (Checkout Generation Phase)
When constructing the payload to create a checkout session, the adapter must extract necessary metadata and append it to the `callback_url` as URL-encoded query strings.

```csharp
// Example from BillplzGatewayAdapter.cs
var webhookUrl = $"{apiBaseUrl}/webhooks/payments/billplz/{tenantId}";
webhookUrl = $"{webhookUrl}?type={Uri.EscapeDataString(typeValue)}&subscription_id={Uri.EscapeDataString(subId)}";
```

### 2. Header Injection (Webhook Ingestion Phase)
The global Minimal API webhook endpoint (`Endpoints.cs` in the Payments module) must be configured to read `context.Request.Query`. Because CQRS commands shouldn't deal with raw HTTP contexts, the endpoint maps these query parameters into the `Headers` dictionary using a `Query-` prefix.

```csharp
// Inside Endpoints.cs
foreach (var query in context.Request.Query)
{
    headers[$"Query-{query.Key}"] = query.Value.ToString();
}
```

### 3. Metadata Reconstruction (Adapter Parsing Phase)
Inside the `ParseWebhookAsync` method, the adapter must actively look for these `Query-` prefixed headers and reconstruct the `Metadata` dictionary before passing the payload to the Event Bus.

```csharp
// Example from BillplzGatewayAdapter.cs
var reference1 = formData.GetValueOrDefault("reference_1", "");
if (string.IsNullOrEmpty(reference1) && headers.TryGetValue("Query-subscription_id", out var qsSubId))
{
    reference1 = qsSubId;
}

var metadata = new Dictionary<string, string>();
if (!string.IsNullOrEmpty(reference1)) metadata["subscription_id"] = reference1;
```

## Security Consequences

Because query strings are visible in standard web server access logs (e.g., Caddy, Nginx):
1. **Never pass PII or sensitive data** (Emails, Names, Passwords, API Keys) in the webhook query string. 
2. Only pass non-sensitive System Identifiers (e.g., `Guid` representing a Subscription ID) and routing types (e.g., `community_subscription`).
