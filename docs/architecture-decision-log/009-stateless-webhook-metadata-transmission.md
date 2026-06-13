
# ADR 009: Stateless Webhook Metadata Transmission via Query Strings

**Status:** Accepted  
**Date:** June 2026  

## Context

### The Context: The "Silent Drop"
When you completed the test payment, the transaction succeeded on Billplz, and your server successfully received the webhook (as verified by the `200 OK` in your ngrok logs). However, the subscription remained inactive and no emails were sent. 

This happened because the `Payments` module published the internal success event without any metadata. When the `Community` module received this event, it checked for a `subscription_id` and `type` to verify ownership. Finding none, the module assumed the payment belonged to a different part of the system (like Vault or Funnel) and silently ignored it, halting the activation and email dispatch process.

### Why is it working now?
In a server-to-server (S2S) payment flow, there are two distinct steps:
1. **You ask the gateway for a checkout link.** (You send the amount, product name, and metadata).
2. **The gateway tells you the customer paid.** (It sends an HTTP POST to your webhook endpoint).

**The Problem:** Stripe is "smart"; it takes the `metadata` dictionary you give it in Step 1 and includes it in the JSON body it sends back in Step 2. Billplz is "dumb"; it completely strips out `reference_1`, `reference_2`, and any custom data from its S2S webhook body. It only sends back its own Bill ID and the amount paid. Because your backend is strictly decoupled, it had no way to know *which* subscriber that Billplz ID belonged to.

**The Solution:** We exploited the one thing Billplz *cannot* strip out: **The URL itself**. 
By appending `?type=community_subscription&subscription_id=...` to the `callback_url` during Step 1, we forced Billplz to send its POST request to that exact URL in Step 2. We then updated your ASP.NET Minimal API endpoint to scrape the Query String from the URL, inject it into the headers, and pass it into the pipeline. Now, your system gets its context back statelessly.

---

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
