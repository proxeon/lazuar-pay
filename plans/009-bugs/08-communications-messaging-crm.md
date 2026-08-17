# 08 — Communications, Messaging, CRM

**Program:** 009-bugs  
**Slice:** Communications (email templates, Resend, suppressions, unsubscribe GET+POST), Messaging (WhatsApp stub, notify, delivery logs), CRM (client profiles, resolve, anonymize)  
**Date:** 17 August 2026  
**Branch / HEAD:** `feat/007-waves-1-4-implement` (`297ba98`)  
**Does not implement.** Does not commit. Does not condense. Does not treat `plans/007-feats` tracker cells or `plans/008-evals/06-communications-email-whatsapp.md` as truth unless this file re-reads the live tree.

Parent index: [README.md](./README.md). Historical (pre-009) comms eval: `plans/008-evals/06-communications-email-whatsapp.md`. Feature archaeology: `plans/007-feats/16-communications-whatsapp-email.md`.

Out of scope for this file (do not treat absence here as a clean bill): dunning campaign builder (03), One invites (07), frontend copy except as evidence of a backend lie.

Honesty lock this file must not contradict: WhatsApp is `ConsoleMessagingService` and `Messaging:WhatsAppEnabled` defaults false. **Do not file “WhatsApp not implemented” as a P0.** The job of WhatsApp utility dunning is frozen under `plans/004-maintenance/decisions.md` §00.4. That is a product decision, not a defect.

---

## 1. Method

Question this file answers: after the P0/P1 landings on this branch (`911d358` … `297ba98`), including Gross/SST wiring in Commerce, what bugs and errors are still in the Communications / Messaging / CRM tree — and which 008 findings are closed, still open, or now worse.

How the work was done:

1. Re-read every live `.cs` file under:
   - `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/`
   - `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/`
   - `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/`
2. Followed producers that this slice consumes: `DunningStepDispatcher`, `InvoiceReminderJob`, `SubscriberQueryService.GetSubscriptionMailContextAsync`, `ICommerceDocumentLookup.GetSubscriptionCommsContextAsync`, `DocumentPublishedIntegrationEvent`, `OrderCompletedIntegrationEvent`, `AnonymizeSubscriberCommandHandler`, `InitiateCheckoutCommandHandler`, `CreateCustomCheckoutCommandHandler`.
3. Followed consumers this slice produces: `DispatchMessageIntegrationEventHandler`, `ResendEmailService`, `SuppressionService`, `LhdnBuyerMapper` (IdValue / Tin from CRM).
4. Re-read tests under `apps/lazuar-api/tests/Lazuar.ModuleTests/{Communications,Messaging,CRM}/` plus the Commerce tests that lock (or fail to lock) amounts, resolve args, and anonymize fan-out.
5. Cross-checked Resend/Svix signature rules against https://docs.svix.com/receiving/verifying-payloads/how-manual (HMAC key = base64-decode of the part after `whsec_`).
6. Marks come from **code paths that execute**, not from seed copy, ops tabs, or 008 prose.

Severity used below:

| Mark | Meaning in this file |
|------|----------------------|
| **P0** | Wrong money/tax identity on a live buyer path, or a compliance pipe that is dead while the product claims it works (bounce/complaint suppression). |
| **P1** | Buyer-visible lie, PDPA wipe hole, deliverability hole, or checkout/mail gate that lets a paid session through and then fails the receipt. |
| **P2** | Hygiene, latent landmine, missing test lock, vitamin-path (broadcasts), or honesty gap that does not fire on the default catalog body. |

WhatsApp-not-shipping is **not** in this table.

---

## 2. Files this slice actually executes

Absolute paths. These are the ground-truth files quoted later. Tests are listed in §9.

| Concern | Path |
|---------|------|
| Shared hydrator + link builder | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs` |
| Seeded catalog | `.../Communications/Domain/DefaultMessageTemplates.cs` |
| Template / suppression / broadcast / BYOK aggregates | `.../Communications/Domain/Aggregates/{MessageTemplate,SuppressionEntry,Broadcast,TenantEmailConfiguration}.cs` |
| Dunning / invoice hydrate | `.../Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` |
| Immediate fail mail | `.../Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` |
| Cancel mail | `.../Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` |
| Receipt / quotation mail | `.../Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| Digital-delivery mail | `.../Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs` |
| Portal Access mail | `.../Communications/Infrastructure/EventHandlers/PortalAccessEmailHandlers.cs` |
| GDPR suppress | `.../Communications/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` |
| Unsubscribe + Resend webhook | `.../Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` |
| Resend payload parser | `.../Communications/Infrastructure/ResendWebhookParser.cs` |
| Suppression lanes | `.../Communications/Infrastructure/Services/SuppressionService.cs` |
| Checkout gate + wiki | `.../Communications/Infrastructure/Services/CommunicationsQueryService.cs` |
| Save BYOK | `.../Communications/Application/Commands/SaveEmailConfigCommand.cs` |
| Template create/update/test | `.../Communications/Application/Commands/MessageTemplateCommandHandlers.cs` |
| Broadcast enqueue | `.../Communications/Application/Commands/BroadcastCommandHandlers.cs` |
| Broadcast fan-out | `.../Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` |
| Admin HTTP | `.../Communications/Infrastructure/Endpoints.cs`, `.../Endpoints/{Template,Broadcast}Endpoints.cs` |
| Markdown → HTML | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Application/MarkdownParser.cs` |
| Document HMAC helper | `.../BuildingBlocks/Infrastructure/DocumentLinkSigner.cs` |
| Dispatch + credits + log | `.../Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` |
| Resend HTTP | `.../Messaging/Infrastructure/Email/ResendEmailService.cs` |
| Brand HTML shell | `.../Messaging/Infrastructure/Email/EmailTemplateBuilder.cs` |
| WhatsApp stub | `.../Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` |
| Notify + delivery-log HTTP | `.../Messaging/Infrastructure/Endpoints.cs` |
| Notify command | `.../Messaging/Application/SendTenantNotificationCommandHandler.cs` |
| Delivery log entity | `.../Messaging/Domain/MessageDeliveryLog.cs` |
| Resend options (no webhook secret) | `.../Messaging/Infrastructure/Configuration/ResendOptions.cs` |
| CRM resolve | `.../CRM/Infrastructure/ResolveClientProfileCommandHandler.cs` |
| CRM create (latent) | `.../CRM/Infrastructure/CreateClientProfileCommandHandler.cs` |
| CRM anonymize | `.../CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` |
| CRM entity | `.../CRM/Domain/ClientProfileEntity.cs` |
| Unique index (org, email, phone) | `.../CRM/Infrastructure/Configurations/ClientProfileConfiguration.cs` |
| CRM reads | `.../CRM/Infrastructure/CrmQueryService.cs` |
| Global name/email sync | `.../CRM/Infrastructure/EventHandlers/GlobalUserProfileUpdatedIntegrationEventHandler.cs` |
| Gross helper (Commerce; dunning now calls it) | `.../Commerce/Application/SubscriptionBillingAmount.cs` |
| Dunning payload amounts | `.../Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` |
| Cancel mail money | `.../Commerce/Infrastructure/Services/SubscriberQueryService.cs` |
| Fail-mail context | `.../Commerce/Contracts/ICommerceDocumentLookup.cs`, `.../Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| Quote reminder amounts | `.../Commerce/Infrastructure/Workers/InvoiceReminderJob.cs` |
| Checkout gate callers | `.../Commerce/Application/Commands/{InitiateCheckout,CreateProduct,UpdateProduct}CommandHandler.cs` |
| Custom-quote B2B resolve (positional) | `.../Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` |
| Commerce anonymize entry | `.../Commerce/Application/Commands/AnonymizeSubscriberCommandHandler.cs` |
| Commerce anonymize consumer | `.../Commerce/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` |
| LHDN buyer IdValue | `.../Lhdn/Infrastructure/Services/LhdnBuyerMapper.cs` |
| Billing PDF + DocumentPublished | `.../Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` |
| Appsettings (Jwt empty, Resend webhook empty, WA flag) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json` |

There is still **no** `Communications/README.md`. Messaging README remains the only module-level freeze text. CRM README still claims a “strict, isolated directory” and a complete GDPR fan-out.

---

## 3. Mechanics the tree still implements

```
Domain event / admin API
  → Communications (policy, templates, suppressions, BYOK store, variable fill)
  → communications.OutboxMessages → in-process bus
  → Messaging inbox
  → DispatchMessageIntegrationEventHandler
       → IEmailService = ResendEmailService          (LIVE if tenant BYOK)
       → IMessagingService = ConsoleMessagingService (STUB; also gated off)
```

Communications owns content and policy. Messaging is the dumb pipe (R34). CRM is the PII registry with no HTTP surface; merchants wipe via Commerce `POST /admin/commerce/subscribers/{id}/anonymize`.

Gross wiring (new since 008’s “amount = product.Price” write-up) landed in Commerce:

- `SubscriptionBillingAmount.Gross` = `(UnitAmount > 0 ? UnitAmount : product.Price) * max(1, Quantity)` plus SST when the merchant has an SST registration and the product is typed `02`.
- `DunningStepDispatcher.DispatchCommunicationStepAsync` now awaits that Gross and puts the result in `amount` / `total_price`.
- `DunningEngineJob` passes `IBillingQueryService` into the dispatcher.
- Cancel mail (`GetSubscriptionMailContextAsync`) and immediate-fail mail (`GetSubscriptionCommsContextAsync`) were **not** moved onto Gross.
- The template wiki still describes `{{amount}}` as “Product list price”.

That split is the money-honesty story of this slice after Gross. Dunning day-0 copy prints `{{amount}}`. Cancel default copy does not. Immediate-fail catalog does not. Invoice reminders are quote line-item sums, not subscriptions.

---

## 4. Quoted walk

This section is the evidence. Bugs in §6 point back here. Do not skip the quotes.

### 4.1 Dunning amounts after Gross

`DunningStepDispatcher` no longer writes `product.Price` into the payload. It writes Gross:

```70:86:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs
        var amount = product == null
            ? 0m
            : await SubscriptionBillingAmount.Gross(sub, product, billing);

        var payloadObj = new
        {
            subscription_id = sub.Id.ToString(),
            client_profile_id = sub.ClientProfileId.ToString(),
            product_id = sub.ProductId.ToString(),
            action_type = effectiveActionType,
            subject = step.Subject,
            email_body = step.EmailBody,
            whatsapp_body = effectiveActionType == "EMAIL" ? string.Empty : step.WhatsAppBody,
            plan_name = product?.Name ?? string.Empty,
            amount,
            total_price = amount,
            currency = product?.Currency ?? string.Empty,
```

`Gross` is seats × snapshot unit, plus SST when Billing says the merchant is registered:

```18:32:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static decimal Unit(Subscription sub, Product product)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return sub.UnitAmount > 0 ? sub.UnitAmount : product.Price;
    }

    public static int Seats(Subscription sub)
    {
        ArgumentNullException.ThrowIfNull(sub);
        return Math.Max(1, sub.Quantity);
    }

    public static decimal Line(Subscription sub, Product product) =>
        Unit(sub, product) * Seats(sub);
```

```54:63:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static decimal Gross(
        decimal unitNet,
        int seats,
        string? sstTaxType,
        decimal sstRatePercent,
        bool merchantHasSst) =>
        GrossBreakdown(unitNet, seats, sstTaxType, sstRatePercent, merchantHasSst).Gross;

    public static decimal Gross(Subscription sub, Product product, bool merchantHasSst) =>
        GrossBreakdown(sub, product, merchantHasSst).Gross;
```

The Communications hydrator then formats whatever number the producer sent. It does not recompute seats. It does not know what Gross is.

```116:120:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
        var planName = root.TryGetProperty("plan_name", out var planProp) ? planProp.GetString() ?? "" : "";
        var amount = MessageTemplateHydrator.FormatMoney(ReadNumericString(root, "amount"));
        var totalPrice = root.TryGetProperty("total_price", out _)
            ? MessageTemplateHydrator.FormatMoney(ReadNumericString(root, "total_price"))
            : amount;
```

`{{amount}}` and `{{total_price}}` are still aliases of the same payload field. The wiki still pretends they are not Gross:

```91:94:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs
                    new TemplateVariableDto { Tag = "{{plan_name}}", Description = "The subscription name (e.g. Premium Tier)." },
                    new TemplateVariableDto { Tag = "{{amount}}", Description = "Product list price, formatted 0.00." },
                    new TemplateVariableDto { Tag = "{{total_price}}", Description = "Same as amount until invoice totals exist." },
                    new TemplateVariableDto { Tag = "{{currency}}", Description = "ISO currency code (e.g. MYR)." },
```

That wiki sentence is now **false** for the only mail that prints money on the default new-org campaign (day-0 dunning). It is still **true** for cancel, if a merchant adds `{{amount}}` there.

### 4.2 Cancel mail is still catalog list price

```96:103:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs
        return new SubscriptionMailContext(
            sub.Id,
            sub.ProductId,
            product?.Name ?? "",
            product?.Price ?? 0m,
            product?.Currency ?? "",
            sub.NextBillingDate,
            sub.Status);
```

The port comment still says so out loud:

```20:24:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/ISubscriberQueryService.cs
    /// <summary>
    /// Commerce-schema snapshot for dunning / lifecycle mail (plan, list price, next bill).
    /// Null when the subscription is missing or belongs to another org.
    /// </summary>
```

`LifecycleEventHandlers` formats that list price into both `Amount` and `TotalPrice`:

```63:73:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs
        var amount = mail == null ? "" : MessageTemplateHydrator.FormatMoney(mail.Price);

        var ctx = new MessageTemplateContext(
            CustomerName: string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name,
            CustomerEmail: toEmail,
            CustomerPhone: profile.Phone ?? "",
            BusinessName: string.IsNullOrWhiteSpace(workspace?.Name) ? "Lazuar Merchant" : workspace.Name,
            PlanName: mail?.PlanName ?? "",
            Amount: amount,
            TotalPrice: amount,
```

Default cancel catalog does not mention `{{amount}}`. A merchant who pastes the tag after reading the wiki (“list price”) will print list price even though dunning now prints Gross. Two mails, two money truths, one tag name.

Missing mail context still sends. Subject becomes `"Your  membership has ended"` (double space). Tests lock that ugly string (`LifecycleEventHandlersTests.cs` 99–102).

### 4.3 Immediate Payment Failed amount is still empty

`GetSubscriptionCommsContextAsync` returns only profile id, status, and product name. No money fields exist on the record:

```63:66:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs
public record CommerceSubscriptionCommsContext(
    Guid ClientProfileId,
    string Status,
    string? ProductName);
```

The handler writes empty strings:

```82:95:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs
        var ctx = new MessageTemplateContext(
            CustomerName: string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name,
            CustomerEmail: toEmail,
            CustomerPhone: profile.Phone ?? "",
            BusinessName: string.IsNullOrWhiteSpace(workspace?.Name) ? "Lazuar Merchant" : workspace.Name,
            PlanName: context.ProductName ?? "",
            Amount: "",
            TotalPrice: "",
            Currency: "",
            DaysOverdue: "",
            CurrentPeriodEnd: "",
            RenewalLink: links.RenewalLink,
            PortalMagicLink: links.PortalMagicLink,
            UpdatePaymentLink: links.UpdatePaymentLink);
```

Catalog “Payment Failed” does not use `{{amount}}`. A merchant who adds it after reading the wiki gets a blank, not list price, not Gross. `ToPhone` is forced `null` (line 102). Channel is whatever the template says (`ALL`). WhatsApp body is hydrated and then skipped at the edge. That skip is the freeze, not a bug.

### 4.4 Invoice reminder is still quote subtotal, hardcoded MYR

```108:120:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
            var total = session.AdHocLineItems.Sum(i => i.Quantity * i.UnitPrice);
            var payloadObj = new
            {
                client_profile_id = session.ClientProfileId.ToString(),
                session_id = session.Id.ToString(),
                document_number = session.DocumentNumber ?? string.Empty,
                checkout_url = payUrl,
                due_at = session.DueAt.Value.ToString("yyyy-MM-dd"),
                amount = total,
                total_price = total,
                currency = "MYR",
                day_offset = dayOffset,
                plan_name = session.DocumentNumber ?? "Quote"
            };
```

`AdHocLineItem` has no SST field. Custom hop-1 (`InitiateCheckoutCommandHandler` 101) charges the same sum and also hardcodes `"MYR"` on `GenerateCheckoutSessionQuery`. Reminder money matches the quote charge. It does not match subscription Gross. Catalog body prints `{{amount}} {{currency}}`. Tests (`InvoiceReminderJobTests.cs` 78–81) assert `checkout_url` only.

Missing “Invoice Reminder” template is a **warn and return**, not the dunning throw:

```147:154:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
            var template = await _repository.GetTemplateByNameAsync(@event.OrganizationId, "Invoice Reminder");
            if (template == null)
            {
                _logger.LogWarning(
                    "Invoice reminder skipped: template missing. OrganizationId={OrganizationId} SessionId={SessionId}",
                    @event.OrganizationId, subIdStr);
                return;
            }
```

The job still writes `InvoiceReminderDispatchLog` **before** Communications hydrates. If the template is missing, the day is burned and never retried.

### 4.5 Unsubscribe GET+POST both suppress

Hunt item “unsubscribe POST not suppressing” is **closed** on this tree. Both verbs call `SuppressAsync` with reason `UNSUBSCRIBE`.

```57:87:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs
            await suppression.SuppressAsync(orgId, email, "UNSUBSCRIBE", "unsubscribe_link");
            logger.LogInformation("Tenant {OrganizationId}: {Email} unsubscribed via link.", orgId, email);
            return Results.Content(UnsubscribeHtml, "text/html", Encoding.UTF8);
        });

        // RFC 8058 one-click POST to the same List-Unsubscribe URL.
        group.MapPost("/unsubscribe", async (
            ...
            await suppression.SuppressAsync(orgId, email, "UNSUBSCRIBE", "list_unsubscribe_one_click");
            logger.LogInformation("Tenant {OrganizationId}: {Email} unsubscribed via one-click POST.", orgId, email);
            return Results.Ok();
```

Lanes still hold: `UNSUBSCRIBE` blocks Marketing only. Dispatch uses Transactional (`DispatchMessageIntegrationEventHandler.cs` 78). Broadcast uses Marketing (`BroadcastFanoutJob.cs` 162). `SuppressionLaneTests` locks that.

HMAC is a different bug (empty `Jwt:Secret`, `??` vs `IsNullOrWhiteSpace`, `FixedTimeEquals` length). The POST itself writes the row.

`BuildUnsubscribeUrl` is used by broadcasts only. Transactional/dunning omit `UnsubscribeUrl`. Correct.

### 4.6 Resend webhook verify uses the wrong HMAC key

Signed content construction matches Svix (`{svix-id}.{svix-timestamp}.{rawBody}`). Timestamp skew > 300s is rejected. Empty secret fail-closes outside Development. All of that is fine.

The key is not:

```126:135:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs
                var signed = $"{svixId}.{svixTimestamp}.{rawBody}";
                var expected = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed)));
                var received = svixSignature.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(p => p.StartsWith("v1="))?["v1=".Length..];
                if (received == null || !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received)))
                {
                    return Results.BadRequest("Invalid webhook signature.");
                }
```

Svix (Resend’s signer), manual verification page, 6 August 2026:

> So to calculate the expected signature, you should HMAC the `signed_content` from above using the **base64 portion of your signing secret (this is the part after the `whsec_` prefix)** as the key. For example, given the secret `whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw` you will want to use `MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw`.
>
> `const secretBytes = Buffer.from(secret.split('_')[1], "base64");`

Lazuar HMAC-SHA256s the **UTF-8 bytes of the entire configured string**, prefix included, and never base64-decodes. A merchant who pastes the Resend “Signing Secret” (`whsec_…`) into `Resend:WebhookSecret` will see every bounce and complaint return `400 Invalid webhook signature`. Nothing is suppressed.

`ResendOptions` still has no `WebhookSecret` field. The endpoint reads `IConfiguration["Resend:WebhookSecret"]` ad hoc. `appsettings.json` 35–38 default is `""`. Empty + non-Development = **503 fail-closed** (105–110). So production today is one of:

| Config | What happens |
|--------|----------------|
| Secret empty (default) | 503. No suppressions. Resend retries a dead endpoint. |
| Secret = `whsec_…` (the value Resend shows) | 400. No suppressions. |
| Secret = raw decoded bytes typed as UTF-8 | Might verify. Nobody documents this. Tests do not lock it. |

Parser unit tests only cover JSON shape (`ResendWebhookParserTests`). There is **no** endpoint test that feeds a Svix-signed body.

Parser `to` handling requires an array. A string `data.to` is ignored. `TryParseSuppression` returns `true` even when type/recipient/org are all null. The endpoint then `MapReason`s, sees null, returns 200. Soft vs hard bounce is not distinguished; both become `BOUNCE`.

No-org-tag still warns and does **not** suppress (155–158). Attribution still depends on outbound tag `org` (`ResendEmailService.OrgTagName`). That part is correct.

Exceptions inside the parse/suppress try are logged and **still return 200** (160–165). Resend will not retry. A transient DB failure on `SuppressAsync` is lost forever.

### 4.7 First reason wins — bounce after unsub never inserts

```50:61:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Services/SuppressionService.cs
    public async Task SuppressAsync(Guid organizationId, string email, string reason, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var normalized = email.Trim().ToLowerInvariant();

        var exists = await _dbContext.SuppressionEntries
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == organizationId && s.Email == normalized);
        if (exists) return;

        _dbContext.SuppressionEntries.Add(new SuppressionEntry(organizationId, normalized, reason, source));
        await _dbContext.SaveChangesAsync();
    }
```

Unique index is `(OrganizationId, Email)` (`CommunicationsDbContext.cs` 89). The interface comment says “Idempotent on (org, email).” That is the bug, written as a feature.

Sequence: broadcast List-Unsubscribe inserts `UNSUBSCRIBE`. Later `email.bounced` finds the row and returns. `IsSuppressedAsync(..., Transactional)` only trips on `BOUNCE` / `COMPLAINT` / `ANONYMIZED`. Receipts and dunning keep going to a mailbox Resend already rejected.

`IsSuppressedAsync` with a blank email returns `false` (line 24). Dispatch will not send a blank To either (`wantsEmail` requires a non-empty address), so that particular hole is closed at the edge.

System tenants skip the suppression check entirely (`DispatchMessageIntegrationEventHandler.cs` 57–58, 78). One platform mail is out of this slice.

### 4.8 HasValidEmailConfig is a row-shape check, not a working key

```117:132:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs
    public async Task<bool> HasValidEmailConfigAsync(Guid tenantId)
    {
        ...
        const string sql = @"
            SELECT 1 
            FROM communications.""TenantEmailConfigurations"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""IsActive"" = true 
              AND ""ApiKey"" IS NOT NULL AND ""ApiKey"" != ''
              AND ""SenderEmail"" IS NOT NULL AND ""SenderEmail"" != ''
            LIMIT 1";
```

It does not decrypt. It does not call Resend. A row with garbage ciphertext, a rotated-and-revoked key, or `receipts@gmail.com` counts as valid.

Save-path live check is `GET https://api.resend.com/domains` (SaveEmailConfigCommand.cs 73–81). Success means “this key can list domains.” It does **not** assert `SenderEmail`’s host is one of those domains. Ops copy still claims you cannot use Gmail. The server does not enforce it.

Callers:

| Caller | Effect |
|--------|--------|
| `InitiateCheckoutCommandHandler` 54–58 | Throws: checkout disabled. |
| `CreateProductCommandHandler` 43–48 | Product is created, then `Archive()`. |
| `UpdateProductCommandHandler` 34–40 | Activating without a row → business rule. |
| `CreateCustomCheckoutCommandHandler` | **Does not call it.** Quotes mint without Resend. |

So the gate is hop-1 catalog checkout + product activate. It is not quote create. It is not “the key still works.” A merchant who saved a key, then revoked it in Resend, still opens checkout. The buyer pays. Receipt dispatch throws “No platform fallback allowed for tenant emails” (`ResendEmailService.cs` 66–69). That is the incorrect gate the hunt asked about.

Create-product archive-without-telling-the-command-caller is a second, smaller lie: the handler returns a Guid as if the product were live.

### 4.9 ClientUrl vs OpsUrl on this slice

Every Communications buyer link uses `App:ClientUrl`:

- `FulfillmentRequestedIntegrationEventHandler` 101
- `PortalAccessEmailHandlers` 76
- `OrderCompletedDigitalDeliveryHandler` 68
- `GatewayPaymentFailedIntegrationEventHandler` 78
- `LifecycleEventHandlers` 60
- `InvoiceReminderJob` 85

Document emails use `App:ApiBaseUrl` for the signed PDF (`DocumentPublishedIntegrationEventHandler` 67–69). That is the public API host, not Ops. Correct.

There is **no** `App:OpsUrl` read under Communications, Messaging, or CRM. One invite mail uses OpsUrl via `OneLinkService`. That is slice 07.

Hunt item “email sent with ClientUrl vs OpsUrl wrongly” is **closed** for this slice. Preview mocks still say `https://portal.lazuar.com/acme/...` (`MessageTemplateHydrator.Preview`). That is preview-only.

### 4.10 HTML injection

Hydrator is raw string replace. No `HtmlEncoder`. No markdown escape.

```69:83:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs
        return text
            .Replace("{{customer_name}}", ctx.CustomerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{customer_email}}", ctx.CustomerEmail, StringComparison.OrdinalIgnoreCase)
            ...
```

`customer_name` is whatever checkout typed. Then:

```7:19:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Application/MarkdownParser.cs
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static string ToHtml(string markdown)
    {
        ...
        return Markdown.ToHtml(markdown, Pipeline);
    }
```

Markdig’s default pipeline **allows raw HTML**. `UseAdvancedExtensions()` does not call `DisableHtml()`. A buyer named `Jane <img src=x onerror=alert(1)>` or a name that closes a markdown link (`](https://evil.example)`) is interpolated into merchant copy and emitted as HTML. Email clients differ; this is still untrusted input in an HTML email.

Merchant-authored templates can include `<script>`, `javascript:` links, and tracking pixels. Create validates `{{tags}}` against required∪optional. **Update does not** (`UpdateMessageTemplateCommandHandler` 99–108). Paste `{{garbage}}` or raw HTML; it ships.

Broadcasts skip Markdown entirely and publish `broadcast.EmailBody` as HTML (`BroadcastFanoutJob` 174–183). The brand wrapper interpolates `unsubscribeUrl` into an href with no encoding (`EmailTemplateBuilder` 18–22). `BuildUnsubscribeUrl` is a controlled URL (guid + escaped email + hex sig), so that particular href is safe. Buyer names are not.

`EmailTemplateBuilder.WrapWithBrandHtml` still `Replace("\n", "<br/>")` on already-parsed HTML (line 16). Tests lock the `<br/>` (`DispatchMessageIntegrationEventHandlerTests` 120–126, `EmailTemplateBuilderTests` 22). Noisy `<br/>` inside `<p>` tags is unchanged.

### 4.11 CRM resolve — email-only identity, first write wins

Checkout, custom quotes, and manual enroll all go through `ResolveClientProfileCommand`, not `CreateClientProfileCommand`. (Create is constructed in tests only.)

```22:28:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var phoneNormalized = NormalizePhone(request.Phone);

        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId && p.Email == emailNormalized, cancellationToken);
```

If a row exists:

- `FullName` is **never** updated. First checkout’s typo or the previous stranger’s name sticks on every later mail.
- `Phone`, `CompanyName`, `Tin`, `IdType`, `IdValue`, `Address` fill **only when blank**. A second person sharing the email cannot overwrite the first person’s NRIC / TIN / address.
- `ConsentedToMarketing` is **never** updated. `InitiateCheckoutCommand` does not even have a consent field.
- There is no `OrderBy`. If two rows share an email (the unique index is `(OrganizationId, Email, Phone)`, not email alone — `ClientProfileConfiguration.cs` 15), `FirstOrDefault` is non-deterministic.

`LhdnBuyerMapper` reads that same row:

```44:61:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/LhdnBuyerMapper.cs
        var tin = FirstNonEmpty(profile?.Tin, display?.Tin);
        ...
        buyerName = FirstNonEmpty(profile?.Company_name, display?.CompanyName, profile?.Full_name, display?.Name, "Customer");
        ...
        idValue = FirstNonEmpty(profile?.Id_value, display?.IdValue);
        if (string.IsNullOrWhiteSpace(idValue) || string.Equals(idValue, "NA", StringComparison.OrdinalIgnoreCase))
            return false;
```

Two colleagues sharing `accounts@company.com` become one MyInvois buyer. The first NRIC/BRN wins. The second checkout’s tax identity is discarded. That is “merging strangers.”

### 4.12 Custom-quote B2B resolve puts CompanyName into IdValue

Product hop-1 uses named arguments and is correct (`InitiateCheckoutCommandHandler` 198–208). The **custom session** B2B branch uses positional arguments against `ResolveClientProfileCommand`’s parameter list:

```
OrganizationId, FullName, Email, Phone, Tin, IdType, IdValue, BillingAddress, ConsentedToMarketing, CompanyName
```

```134:142:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
                await _mediator.Send(new ResolveClientProfileCommand(
                    tenantId.Value,
                    request.Name,
                    request.Email,
                    request.Phone ?? "",
                    request.TaxId,
                    null,
                    request.CompanyName,
                    customBillingAddress), ct);
```

Seventh positional = `IdValue` = `request.CompanyName`. Eighth = `BillingAddress`. `CompanyName` on the CRM row is never set. `IdType` is forced null (mapper then defaults BRN).

`InitiateCheckoutCommand` itself stores CompanyName as its seventh field (`InitiateCheckoutCommand.cs` 14). The test at `CreateCustomCheckoutAndInitiateSessionTests.cs` 132–141 passes `"Buyer Sdn Bhd"` as that seventh **command** argument (so the DTO is right) and then only asserts gateway metadata. It never inspects the `ResolveClientProfileCommand` that the handler builds. `CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` does the same. `ClientProfileCompanyNameTests` uses named params and therefore cannot see this path.

The return value of Resolve is **discarded**. `CheckoutSession.ClientProfileId` stays the quote-time profile (`CreateCustomCheckoutCommandHandler` 30–35, phone `""`). If the payer email equals the quote email — the normal case — Resolve finds that row and writes CompanyName into `IdValue`. LHDN then submits BRN = `"Buyer Sdn Bhd"`.

If the payer email differs, Resolve creates or updates a **ghost** profile and the session still points at the quote customer. Documents follow the quote. The ghost row keeps the polluted IdValue.

### 4.13 CreateClientProfile is a latent OR-phone merge

No production caller constructs `CreateClientProfileCommand` (grep hits the handler, the record, generated DTOs, and one test). The handler is still registered via MediatR on the Infrastructure assembly.

```25:28:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs
        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId
                && (p.Email == emailNormalized || p.Phone == phoneNormalized), cancellationToken);
```

`NormalizePhone("")` returns `""`. `p.Phone == ""` matches **every** empty-phone profile in the org. FirstOrDefault then “creates” by returning a stranger. Do not wire an HTTP or LLM tool to this handler without rewriting the predicate.

### 4.14 Anonymize wipes CRM and commerce logs. It does not wipe Billing, LHDN, or delivery logs.

CRM wipe is real:

```27:39:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs
    public void Anonymize()
    {
        FullName = "Anonymized User";
        Email = $"deleted_{Id}@localhost";
        Phone = "";
        CompanyName = null;
        Tin = null;
        IdType = null;
        IdValue = null;
        Address = null;
        ConsentedToMarketing = false;
        GlobalUserId = null;
    }
```

The only HTTP path is Commerce `AnonymizeSubscriberCommandHandler`:

1. Scrub `CommerceTransactionLog` rows whose **current** CRM email matches (`GetTransactionLogsByCustomerEmailAsync`). Name → `Anonymized User`, email → `deleted_{profileId}@localhost`. Amounts stay. `RecordedByName` stays.
2. Send `AnonymizeClientProfileCommand`.
3. CRM publishes `ClientProfileAnonymizedIntegrationEvent` with the **pre-wipe** email/phone into the CRM outbox, then `SaveChanges` (atomic with the wipe).
4. Communications consumer inserts `ANONYMIZED` (both lanes).
5. Commerce consumer cancels every non-`CANCELED` subscription on that profile and publishes `SubscriptionCanceledIntegrationEvent`.
6. `LifecycleEventHandlers` loads the profile **after** the wipe and mails `deleted_{id}@localhost`. The real inbox is suppressed. The dummy address bounces if anyone ever configured a catch-all, or just fails.

There is **zero** `Anonymiz` hit under `Modules/Billing/**/*.cs`. `GenerateAndStoreDocumentCommandHandler` already uploaded `vault/{org}/documents/{ledgerEntryId}.pdf` with `CustomerName`, `CustomerEmail`, `CustomerTin`, `CustomerAddress` baked into the PDF (`InvoiceDocumentFactory` 37–41). Those objects are not deleted. `DocumentPublishedIntegrationEvent` already carried the live email into Communications outbox history.

LHDN: submitted UBL has `BuyerTin` / `BuyerIdValue` / `BuyerName`. `TinValidateCache` keeps an `IdValueHash`. No consumer clears them.

Messaging: `MessageDeliveryLog.Recipient` is the live address (`MessageDeliveryLog.cs` 16, 38). `GET /messaging/delivery-logs` returns it to any OrgAdmin. No anonymize hook.

If `GlobalUserProfileUpdatedIntegrationEvent` changed CRM email before wipe, log scrub uses the **new** email and leaves rows under the old one (`CommerceRepository.cs` 131–146). Guest checkout profiles have `GlobalUserId == null`, so that particular race is One-linked profiles only.

`GetClientProfileAsync` is id-only with `IgnoreQueryFilters` and **no organization predicate** (`CrmQueryService.cs` 54–61). Callers pass ids they already own. It is still a tenancy hole in the port.

### 4.15 Digital delivery is “every completed one-time order”

`OrderCompletedIntegrationEvent` is published only on the non-subscription branch of hop-2 (`GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` 118–138), plus zero-amount and offline mark-paid. Subscriptions get `SubscriptionActivated` instead. Good — we do not double-mail “download ready” on renewals.

The handler still does not look at `FulfillmentTargets` or product type:

```45:85:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs
    public async Task HandleAsync(OrderCompletedIntegrationEvent @event)
    {
        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.OrganizationId == @event.OrganizationId && t.Name == "Digital Product Delivery");
        ...
        // No dedicated digital asset URL on products yet — portal is the best available fulfillment surface.
        var fulfillmentUrl = portalLink;
        ...
                .Replace("{{plan_name}}", "your purchase", StringComparison.OrdinalIgnoreCase)
                .Replace("{{fulfillment_url}}", fulfillmentUrl, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", portalLink, StringComparison.OrdinalIgnoreCase);
```

Every one-time SKU with the seeded template gets “Your download is ready.” `fulfillment_url` is `{ClientUrl}/{slug}/portal` with **no token**. Wiki still says “Cloudflare R2 Download Link” (`CommunicationsQueryService.cs` 109). Shared hydrator does not even replace `{{fulfillment_url}}` — only `PopulatePreview` does. There is **no** `OrderCompletedDigitalDeliveryHandler` test file.

### 4.16 Dispatch, WhatsApp stub, notify, delivery logs

Flag default false (`appsettings.json` 103–105). Handler short-circuits before the port (Dispatch 60–76). Adapter is a logger, `IsBillable => false`. Registered as the singleton `IMessagingService`. Cost config `WhatsAppSend: 0`. Deduct only if `actualCost > 0`. This is the freeze. **Not a bug.**

`POST /messaging/notify` requires OrgAdmin (locked by `MessagingEndpointsAuthorizationTests`). The body is `SendTenantNotificationCommand(TenantId, Message)`. `TenantId` comes from JSON, **not** `IExecutionContextAccessor`. The handler loads that id from `TenantReplica` and console-logs `[System Alert for {name}]`. Cross-tenant notify is possible; the sink is a log line. P2.

`GET /messaging/delivery-logs` filters `OrganizationId == ctx.TenantId`, clamp 1–200. `MessageDeliveryLog` does **not** implement `IMustHaveTenant`; the explicit Where is the only tenant wall. Status vocabulary SENT / FAILED / SKIPPED. Bounce does not update the row. No TypeSpec. Ops “Developer → Logs” is still outbound webhooks (frontend slice 09).

Broadcast `CreditHoldId = broadcast.Id` still skips deduct (Dispatch 89). `RecordSent()` runs after `PublishAsync` to the **outbox**, not after Resend 200 (`BroadcastFanoutJob` 174–185). A later provider throw increments Sent and never calls `RecordFailed()`. Preview `Recipient_count` is `GetActiveSubscriberCountAsync` (all ACTIVE/PAST_DUE). Fan-out then drops anyone without `Consented_to_marketing`. Checkout never sets that flag. TypeSpec still says “fans out to all ACTIVE/PAST_DUE subscribers with marketing consent.” Half true.

Test reminder always targets `admin@lazuars.io` + `+60123456789` (`SendTestReminderCommandHandler` 168–176) via the **tenant** BYOK. The tenant’s Resend account emails Lazuar staff.

### 4.17 Unsubscribe HMAC vs empty Jwt:Secret

```49:52:/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs
            var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
            var expected = ComputeSig(secret, $"{orgId}:{email}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig.ToLowerInvariant())))
```

`appsettings.json` 23–28 sets `"Jwt": { "Secret": "" }`. `??` does **not** replace empty string. HMAC key is `""`. Anyone who knows `orgId` and email can forge GET/POST unsubscribe.

`DocumentLinkSigner.ResolveSecret` uses `IsNullOrWhiteSpace` and **does** fall back (lines 57–60). Same default secret, two policies. Broadcast signing uses the same `??` (`BroadcastFanoutJob` 152), so minted links and the verifier agree when both see `""`. Forgery is easy; verification is consistent.

`FixedTimeEquals` throws if the byte lengths differ. A short `sig` is a 500, not a 400. Same pattern on the Svix compare (132–133) when `received` is a different-length base64 string.

---

## 5. Hunt items — closed / open / moved

| Hunt | Verdict on `297ba98` |
|------|----------------------|
| Template vars wrong (amount ignores seats — re-check after Gross) | **Moved.** Dunning payload is Gross (seats × snapshot + SST). Cancel is still `product.Price`. Immediate fail is still `""`. Wiki still says “list price.” Invoice reminder is quote subtotal / `MYR`. |
| Unsubscribe POST not suppressing | **Closed.** POST writes `UNSUBSCRIBE` / `list_unsubscribe_one_click`. Lanes keep receipts alive. |
| Resend webhook parse | **Open, worse than parse.** Parser handles array `to` + both tag shapes. Verify HMAC key is not Svix’s key. Production suppressions from Resend are dead. |
| Suppression bypass | **Open.** First-reason-wins (unsub hides later bounce). Empty-secret / wrong-secret webhook. Development skip-verify is intentional. System tenant skip is out of slice. |
| CRM resolve merging strangers | **Open.** Email-only, first TIN/NRIC/name wins. Custom B2B path writes CompanyName into IdValue. |
| Anonymize leaving PII in commerce/billing | **Open for Billing + LHDN + delivery logs.** Commerce transaction-log name/email is scrubbed on the subscriber path. CRM row is wiped. PDFs and MyInvois submissions are not. |
| Email sent with ClientUrl vs OpsUrl wrongly | **Closed** on this slice. Buyer links = `App:ClientUrl`. PDFs = `App:ApiBaseUrl`. OpsUrl is One invites. |
| HasValidEmailConfig gating checkout incorrectly | **Open.** Row-shape true-positive. Quotes ungated. Revoked/garbage key still opens hop-1. Create-product silent archive. |
| HTML injection in templates | **Open.** Unescaped replace + Markdig raw HTML + update-path no tag check + broadcast raw HTML. |
| WhatsApp not implemented | **Not a bug.** Flag off, console stub, cost 0. |

---

## 6. Bug catalog

### B08-M01 — P0 — Resend bounce/complaint webhook never verifies a real `whsec_` secret

**Where:** `PublicComplianceEndpoints.cs` 126–135; `ResendOptions.cs` 1–8; `appsettings.json` 35–38.

**What:** Manual Svix verification requires HMAC-SHA256 with the **base64-decoded** bytes after `whsec_`. Lazuar HMAC-SHA256s `Encoding.UTF8.GetBytes(secret)` of the whole string. Resend’s dashboard value will never match.

**Why it matters:** The product claims bounce/complaint suppression is live (008 §4, lanes tests, README-adjacent honesty). In production the inbound pipe is either 503 (empty secret) or 400 (correctly pasted secret). Mail keeps going to hard-bounced and complained addresses. That burns the tenant’s Resend domain and ignores a legal complaint.

**Not fixed by:** parser tests. Those never touch HMAC.

**Fix direction (do not implement here):** strip `whsec_`, `Convert.FromBase64String`, HMAC that key; put `WebhookSecret` on `ResendOptions`; add a signed-body integration test using the Svix sample (`secret = 'whsec_plJ3nmyCDGBKInavdOK15jsl'` …).

---

### B08-M02 — P0 — Custom-quote B2B resolve stores CompanyName as LHDN IdValue

**Where:** `InitiateCheckoutCommandHandler.cs` 134–142 vs `ResolveClientProfileCommand.cs` 7–17 vs `LhdnBuyerMapper.cs` 51–61.

**What:** Positional argument 7 is `IdValue`, not `CompanyName`. Quote pay with `IsB2bRequired` writes `"Acme Sdn Bhd"` into `ClientProfileEntity.IdValue`. Mapper treats that as BRN (IdType is null → default BRN) and will submit it if TIN is also present.

**Why it matters:** Lazuar is Compliance CaaS. A MyInvois buyer identification number that is a company **name** is a rejected or worse, accepted-wrong, tax document. The session’s `ClientProfileId` is not updated from Resolve’s return, so this always mutates the quote-time profile when emails match.

**Tests that should have caught it and did not:** `CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata`; `CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin`. Both fire Resolve. Neither reads `IdValue` / `CompanyName` on the command.

Product hop-1 is fine (named args, `CheckoutB2bIdentityTests` 49–55).

---

### B08-M03 — P1 — Resolve merges strangers by email and freezes the first tax identity

**Where:** `ResolveClientProfileCommandHandler.cs` 26–79; unique index `ClientProfileConfiguration.cs` 15; `LhdnBuyerMapper.cs` 44–61.

**What:** Identity key is normalized email only. Enrichment is blank-fill-only. FullName never moves. Two people, one shared inbox (`accounts@`, a family Gmail, a typo) share TIN, NRIC/BRN, address, and every dunning/receipt mail greeting.

**Why it matters:** PDPA / LHDN. The first buyer’s NRIC on the second buyer’s invoice is not “CRM convenience.”

**Not the same as:** Stripe-style customer-by-email when the product is a single-buyer SaaS seat. This product sells B2B tax invoices off the same row.

---

### B08-M04 — P1 — Unsubscribe row blocks later BOUNCE/COMPLAINT insert

**Where:** `SuppressionService.SuppressAsync` 50–58; unique `(OrganizationId, Email)`; `IsSuppressedAsync` 34–45.

**What:** First reason wins. Marketing unsub first → transactional lane stays open → Resend bounce cannot upgrade the row.

**Why it matters:** The 008 P0 “unsub kills receipts” was correctly inverted into lanes. The leftover race undoes bounce protection for anyone who unsubscribed first. That is the common List-Unsubscribe-then-mailbox-gone sequence.

**Tests:** `SuppressionLaneTests` locks the lane matrix on a **pre-inserted** reason. Nothing inserts unsub then bounce.

---

### B08-M05 — P1 — HasValidEmailConfig is a false “valid”; quotes skip it

**Where:** `CommunicationsQueryService.HasValidEmailConfigAsync` 117–132; `InitiateCheckoutCommandHandler` 54–58; `CreateCustomCheckoutCommandHandler` (no call); `CreateProductCommandHandler` 43–48; `UpdateProductCommandHandler` 34–40; `ResendEmailService` 66–69.

**What:** Any active row with non-empty ciphertext + sender opens catalog checkout. Create-quote never asks. A revoked or undecryptable key still returns true. Hop-2 / receipt / dunning then throw no-fallback.

**Why it matters:** The gate’s job is “do not take money you cannot receipt.” It gates the presence of a row, not a working sender. Buyer pays; Official Receipt mail dies in Messaging; outbox retries; delivery log FAILED.

Silent `product.Archive()` on create without email config is a smaller sibling (returned id looks live).

---

### B08-M06 — P1 — Untrusted names and merchant HTML are emitted as email HTML

**Where:** `MessageTemplateHydrator.Populate`; `MarkdownParser` pipeline; `UpdateMessageTemplateCommandHandler` 99–108; `DocumentPublishedIntegrationEventHandler` 74–77; `BroadcastFanoutJob` 174–179.

**What:** No HTML encode. Markdig raw HTML on. Update skips tag validation. Document handler interpolates `CustomerName` then Markdown-parses. Broadcasts are raw HTML.

**Why it matters:** Checkout `Name` is attacker-controlled. Stored XSS in the buyer’s (and the merchant’s preview) mailbox. Phishing links via `javascript:` or extra `<a>` tags. This is not “merchants can brand their mail.” This is buyer input in a privileged HTML context.

---

### B08-M07 — P1 — Anonymize does not reach Billing PDFs, LHDN submissions, or delivery logs

**Where:** `AnonymizeSubscriberCommandHandler` (commerce logs + CRM command only); `GenerateAndStoreDocumentCommandHandler` 107–120; `LhdnBuyerMapper` / submitted UBL; `MessageDeliveryLog`; no Billing anonymize consumer.

**What:** After Subscribers → Anonymize, CRM is dummy, commerce log name/email are dummy, mail is suppressed, subscriptions cancel. The official receipt PDF in R2 still has the live name, email, TIN, and address. MyInvois already has the buyer. `GET /messaging/delivery-logs` still lists the live inbox. Outbox rows for `ClientProfileAnonymized` keep the pre-wipe email until processed (necessary) and remain readable after.

**Why it matters:** Ops UI copy says “This cannot be undone. Subscriptions cancel. Emails stop.” It does not say “your filed tax invoices and receipt PDFs still have the NRIC.” CRM README §5 claims a GDPR fan-out. The fan-out is Communications + Commerce cancel only.

**Commerce log scrub is real** (`AnonymizeSubscriberCommandHandlerTests` 65–69). Do not re-file that as missing.

---

### B08-M08 — P1 — Digital Product Delivery fires for every one-time order and lies about the file

**Where:** `OrderCompletedDigitalDeliveryHandler.cs` 15–85; wiki `CommunicationsQueryService.cs` 109; catalog `DefaultMessageTemplates.cs` 43–50.

**What:** No digital-asset check. `plan_name` is the literal `"your purchase"`. `fulfillment_url` and `portal_magic_link` are the same portal home URL, no 24h token. Wiki: “Cloudflare R2 Download Link” and “24-hour auto-login.”

**Why it matters:** A one-time consulting SKU emails “Your download is ready.” The button is a logged-out portal. There is no test file.

Subscription activations do **not** take this path (they use Portal Access). Do not file a renewal double-mail that does not exist.

---

### B08-M09 — P1 — Empty `Jwt:Secret` is a working HMAC key on unsubscribe

**Where:** `PublicComplianceEndpoints.cs` 49, 77; `BroadcastFanoutJob.cs` 152; `appsettings.json` 23–24; contrast `DocumentLinkSigner.ResolveSecret` 57–60.

**What:** `??` leaves `""` in place. Forged `sig = hex(HMAC-SHA256("", "{org}:{email}"))` unsubscribes anyone. Document links on the same process fall back to the 32-char dev string.

**Why it matters:** Marketing-lane only (receipts survive). Still a one-click unsub of a competitor’s list if org ids leak (they are in every unsubscribe URL and every Resend `org` tag).

`FixedTimeEquals` length mismatch → 500 is B08-M20.

---

### B08-M10 — P1 — Cancel (and wiki) still speak list price after Gross

**Where:** `SubscriberQueryService.cs` 96–103; `LifecycleEventHandlers.cs` 63–72; wiki lines 93–94; `ISubscriberQueryService` comment “list price.”

**What:** 5 seats × RM 99 snapshot, SST on, AUTO_CHARGE RM 534.60. Day-0 dunning now prints `534.60`. Cancel custom `{{amount}}` prints `99.00`. Wiki tells the merchant both tags are list price.

**Why it matters:** The Gross fix was applied to the producer that 008 named and not to the other two subscription mail producers, and the wiki was not updated. Merchants will “fix” dunning by editing the template toward the wiki and make it wrong again.

Immediate-fail empty amount is the third sibling (B08-M15). Default cancel body does not print money, so default tenants only see the wiki lie until they customize.

---

### B08-M11 — P2 — CreateClientProfile `email OR phone` matches empty phones

**Where:** `CreateClientProfileCommandHandler.cs` 25–28.

**What:** Latent. No production `new CreateClientProfileCommand`. Handler is live in the container. Empty phone ≡ every empty-phone row.

**Why it matters:** The next person who “just exposes CRM create” inherits a P0 merge. File it so they do not.

---

### B08-M12 — P2 — Unique `(Email, Phone)` vs resolve-by-email

**Where:** `ClientProfileConfiguration.cs` 15; `ResolveClientProfileCommandHandler.cs` 26–28.

**What:** Two rows with the same email and different phones can exist (Create path, or a future writer). Resolve picks one without `OrderBy`. Concurrent first inserts of `(org, email, "")` race the unique index and 500.

---

### B08-M13 — P2 — GlobalUserProfileUpdated overwrites every linked CRM email

**Where:** `GlobalUserProfileUpdatedIntegrationEventHandler.cs` 20–33.

**What:** All `GlobalUserId == user` rows, every tenant, get `FullName` and `Email` from One. No uniqueness pre-check. Can collide with `(org, newEmail, phone)`. Can change the email anonymize will later scrub logs against (B08-M07).

Guest checkout does not set `GlobalUserId`. Resolve does not either. This fires for Create-linked or subsequently linked profiles.

---

### B08-M14 — P2 — Invoice reminder currency/SST and missing-template burn

**Where:** `InvoiceReminderJob.cs` 108–118; hydrate 147–154; job writes the dispatch log in the same loop after publish (133).

**What:** `currency = "MYR"` always. No SST field on ad-hoc lines (matches hop-1 custom charge — consistent, still a lie if they ever add SST to quotes). Missing template: Communications returns; Commerce already recorded the offset. Exact-day only, UTC, no catch-up (pre-existing 008).

No hydrator test for `EventType == "invoice.reminder"`.

---

### B08-M15 — P2 — Immediate fail amount is empty; context port cannot carry Gross

**Where:** `CommerceSubscriptionCommsContext` (three fields); `GatewayPaymentFailedIntegrationEventHandler` 88–91.

**What:** Catalog does not print amount. Custom templates get `""`. Port would have to grow (or fail-mail should call the same Gross helper cancel should call).

Tests lock the update-payment URL and “no `{{` leftovers” (`GatewayPaymentFailedEmailHandlerTests` 60–76). They do not assert amount. Empty replace leaves no `{{amount}}` if the catalog omits the tag — the test cannot see the hole.

---

### B08-M16 — P2 — Tax Invoice / Credit Note email uses Official Receipt copy

**Where:** `DocumentPublishedIntegrationEventHandler.cs` 38–59; catalog has neither name (`DefaultMessageTemplates.cs` 23–87).

**What:** Fallback is intentional in code. Subject is still “Your official receipt from {business}.” W4-LP-100 fixed the **PDF** disclaimer. The email still says receipt. Event has no amount (`DocumentPublishedIntegrationEvent.cs` 10–18).

---

### B08-M17 — P2 — Template update skips variable validation; hydrator leaves unknown tags

**Where:** `UpdateMessageTemplateCommandHandler` 99–108 vs `CreateMessageTemplateCommandHandler` 27, 47–87; `MessageTemplateHydratorTests` 59–63 (locks the leftover).

**What:** Create is strict. Update is a content dump. `{{garbage}}` ships. `{{fulfillment_url}}` and `{{document_link}}` are not in the shared hydrator at all — only in two local replace loops. A dunning step that copies those tags from the wiki’s fulfillment section will send the raw tag.

---

### B08-M18 — P2 — Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout

**Where:** `SendBroadcastCommandHandler` 35; `GetActiveSubscriberCountAsync` vs `GetActiveSubscriberRecipientsAsync`; `BroadcastFanoutJob` 174–185; `InitiateCheckoutCommand` (no consent field); `Resolve` (does not write consent).

**What:** Preview count ≥ people who will receive. `ConsentedToMarketing` defaults false and checkout never sets it, so fan-out of a “successful” broadcast to a fresh tenant is **zero consenting recipients** after a non-zero preview. `RecordSent` before Resend. `FailedCount` stays 0. ADR 021: do not productize. Still a lying API.

---

### B08-M19 — P2 — `POST /messaging/notify` trusts body.TenantId

**Where:** `Endpoints.cs` 23–27; `SendTenantNotificationCommand` / handler 22–29.

**What:** OrgAdmin of tenant A can pass tenant B’s id. Sink is `ConsoleMessagingService` with B’s slug. Authz test only checks the policy name, not the id binding.

---

### B08-M20 — P2 — `FixedTimeEquals` on hex/base64 of unequal length throws 500

**Where:** unsubscribe 51–52; webhook 131–133.

**What:** `CryptographicOperations.FixedTimeEquals` requires equal lengths. A 1-character `sig` or a truncated `v1=` is an unhandled exception, not `400 Invalid unsubscribe link`.

---

### B08-M21 — P2 — SaveEmailConfig does not require SenderEmail ∈ listed domains

**Where:** `SaveEmailConfigCommand.cs` 73–81.

**What:** 008 recorded this. Still true. Key that can `GET /domains` + `from: gmail.com` saves. Checkout gate then goes green.

---

### B08-M22 — P2 — `GetClientProfileAsync` is global-by-id

**Where:** `CrmQueryService.cs` 54–61.

**What:** `IgnoreQueryFilters`, no `OrganizationId`. A leaked UUID is a PII read. Callers today pass ids from their own tenant rows.

---

### B08-M23 — P2 — Parser misses string `to`; webhook 200 on suppress failure

**Where:** `ResendWebhookParser.ReadRecipient` 47–66; endpoint 160–165.

**What:** If Resend ever sends `"to": "user@example.com"` instead of an array, recipient is null, event acknowledged, no suppress. DB exceptions inside the try are acknowledged too.

---

### B08-M24 — P2 — Test reminder always mails `admin@lazuars.io` via tenant BYOK

**Where:** `SendTestReminderCommandHandler.cs` 168–176; `TemplateEndpoints.cs` 114–120.

**What:** Tenant’s domain sends to Lazuar staff. Preview mocks (Ahmad / Founders Mastermind / portal.lazuar.com). WhatsApp test would console-log `+60123456789` if the flag were on.

---

### B08-M25 — P2 — Anonymize then cancel mails `deleted_{id}@localhost`

**Where:** order in §4.14 step 6; `LifecycleEventHandlers` 46–48.

**What:** Not a PII leak (dummy + real address suppressed). It is a wasted Resend call and a FAILED delivery-log row that support will read as “we emailed the deleted user.”

---

### B08-M26 — P2 — Checkout never collects marketing consent

**Where:** `InitiateCheckoutCommand` has no consent; Resolve default `ConsentedToMarketing = false`; entity default false (`ConsentDefaultFalse` migration).

**What:** Correct PDPA default. Combined with B08-M18, broadcasts cannot reach hop-1 buyers without a back-door write. Not a “consent forced true” regression (that 007 gap is still closed).

---

### B08-M27 — P2 — Dual CMS and leftover `reminder.due`

**Where:** `DunningStepDispatcher` sends step copy, not catalog “Payment Failed”; hydrate still implements `reminder.due` + `template_id` (FulfillmentRequested 51, 161–172); no job publishes it.

**What:** Editing Templates → Payment Failed does not change day-0 dunning. 008 called this Chargebee-shaped debt. Still true. Not a functional defect unless someone sells “one template.” `reminder.due` is dead code path.

---

### B08-M28 — P2 — Brand wrapper still injects `<br/>` into HTML

**Where:** `EmailTemplateBuilder.cs` 16; tests assert it.

**What:** Markdown already emitted `<p>`. Extra `<br/>` is ugly, not a security issue. Locked in by tests — a future cleanup will fail them on purpose.

---

## 7. 008 re-verify

`plans/008-evals/06-communications-email-whatsapp.md` was written 16 August 2026 against the pre-Gross tree. This table is the 17 August 2026 reread.

| 008 claim | `297ba98` |
|-----------|-----------|
| Dunning amount = `product.Price`; seats ignored | **Stale.** Dispatcher calls `Gross`. Engine test still asserts `50m` from `CreateProduct(..., 50)` + default `Quantity = 1` + no Billing in the in-memory job — Gross == list price in that fixture, so the test did not move. There is still **no** `Quantity = 5` mail assertion. |
| Cancel amount = list price | **Still true.** |
| Immediate fail amount empty | **Still true.** |
| Invoice reminder = line sum, `MYR`, no SST | **Still true.** |
| Wiki `{{amount}}` = list price | **Now a lie for dunning; still true for cancel.** Wiki tests do not read the description string for amount. |
| RFC 8058 POST exists and suppresses | **Still true.** Hunt “POST not suppressing” is closed. |
| Lanes: unsub ≠ receipts | **Still true.** |
| First-reason-wins bounce-under-unsub | **Still true.** 008 named it; still unfixed. |
| Resend parser handles both tag shapes | **Still true.** Verify HMAC is the new P0 008 did not file this way (008 described fail-closed + parser, not `whsec_` decode). |
| `HasValidEmailConfig` does not decrypt; quotes ungated | **Still true.** |
| Sender ∉ domains | **Still true.** |
| Digital delivery portal + `"your purchase"` + no token | **Still true.** No tests added. |
| ClientUrl on comms handlers | **Still true; not mixed with OpsUrl.** |
| WhatsApp stub / flag / cost 0 / not billed | **Still true. Not a bug.** |
| Privacy policy Meta sub-processor | **Still true** on `apps/lazuar-portal/src/app/legal/privacy/page.tsx`. Frontend slice 09; leftover evidence that the channel freeze is not reflected in buyer legal copy. |
| DeliveryLogsPage = webhooks | **Still true.** Slice 09. |
| No hydrator test for `invoice.reminder` | **Still true.** |
| No webhook → SuppressionService integration test | **Still true.** |
| No seats mail test | **Still true**, and more important now that Gross exists. |
| Broadcast consent vs TotalRecipients | **Still true.** |
| Create-product archives without config | **Still true.** |
| Document event has no money | **Still true.** |
| `ResendOptions` has no WebhookSecret | **Still true.** |

008’s “highest-leverage leftovers” vs this tree:

1. Portal privacy Meta — still there (09).
2. Put `SubscriptionBillingAmount.Line` on dunning + cancel + fail — **dunning done (Gross, which is Line + SST); cancel and fail not.**
3. Stop labeling checkout phone “WhatsApp Number” — frontend 09.
4. Stamp ADR/gap docs historical — not this slice’s code.
5. Invoice reminder tests/currency/SST/throw-on-missing — still open.
6. Receipt email link wrapper — still a link wrapper.
7. Do not flip `Messaging:WhatsAppEnabled` — still do not.

---

## 8. Lying tests

A test lies when it names a behavior it does not lock, or locks a fixture that cannot fail the bug.

| Test | Why it lies or under-locks |
|------|----------------------------|
| `DunningEngineJobTests.PastDue_Day0Email_PublishesReminderDunningAndRecordsLog` (`total_price == 50m`) | Product price is 50, `Activate` default quantity 1, job fixture has no `IBillingQueryService`. Gross == 50. Passes before and after the seats fix. Does not mention seats. |
| `DunningTemplateVariableSubstitutionTests.HandleAsync_Dunning_AmountJsonNumber_FormatsIntoBody` | Injects `amount = 99.00m` in the payload. Proves the hydrator formats a number. Proves nothing about Commerce Gross. |
| `SubscriberQueryServiceMailContextTests.GetSubscriptionMailContext_ReturnsProductAndPeriod` | Locks `Price == 99` from `product.Price`. After a real fix this test **must** change. Today it documents the bug as the spec. |
| `TemplateVariablesWikiTests.GetTemplateVariables_ListsDunningTags_AndOmitsCommunityLeftovers` | Asserts tags exist. Does **not** assert the amount description. Wiki can keep saying “list price” forever. |
| `CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_*` and `CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` | Exercise the positional Resolve. Assert gateway metadata only. The P0 IdValue write is invisible. |
| `ClientProfileCompanyNameTests.Resolve_StoresCompanyNameAndTin_LeavesIdValueNull` | Named arguments. Green while production custom B2B is wrong. |
| `GatewayPaymentFailedEmailHandlerTests` “no `{{` leftovers” | Catalog has no `{{amount}}`. Empty amount cannot fail this test. |
| `InvoiceReminderJobTests.Day0Due_OpenCustom_SendsOnce` | Asserts URL. Does not assert `amount`, `currency`, or `plan_name`. |
| `ResendWebhookParserTests` | Parser only. A green suite here is compatible with a 100% 400 rate on the live webhook. |
| `SuppressionLaneTests` | Pre-seeded reasons. Does not insert twice. First-reason-wins is untested. |
| `MessagingEndpointsAuthorizationTests` | Policy = OrgAdmin. Does not bind `TenantId` to `ctx.TenantId`. |
| `DispatchMessageIntegrationEventHandlerTests` brand `<br/>` | Locks the noisy HTML as required behavior. |
| `AnonymizeSubscriberCommandHandlerTests` / `ClientProfileAnonymizedEventTests` | Honest about CRM + commerce logs + cancel fan-out. They will be quoted as “GDPR is done.” They never open Billing or Messaging tables. |
| `LifecycleEventHandlersTests.Cancel_MissingMailContext_StillDispatchesWithLinks` | Locks `"Your  membership has ended"` as acceptable. That is a product bug frozen as a unit test. |
| **Missing files** | No `OrderCompletedDigitalDeliveryHandler` tests. No public unsubscribe endpoint tests. No Resend webhook HMAC tests. No Resolve-two-strangers-same-email test. No `Quantity = 5` dunning payload test. No `HasValidEmailConfig` decrypt/negative test. |

`DefaultMessageTemplatesTests.PaymentFailed_RequiresUpdatePaymentLink_AndDoesNotHardcodePortalHost` is honest and still useful. Do not “fix” it.

`PortalAccessEmailHandlerTests` honestly lock the token. Digital delivery’s missing token is the contrast.

---

## 9. Tests that exist (inventory)

Communications: `AppEntitlementGrantedIntegrationEventHandlerTests`, `BroadcastClaimTests`, `BroadcastTests` (aggregate only), `ClientProfileAnonymizedSuppressionTests`, `DefaultMessageTemplatesTests`, `DocumentPublishedIntegrationEventHandlerTests`, `DunningTemplateVariableSubstitutionTests`, `GatewayPaymentFailedEmailHandlerTests`, `LifecycleEventHandlersTests`, `MessageTemplateHydratorTests`, `PortalAccessEmailHandlerTests`, `ResendWebhookParserTests`, `SuppressionEntryTests`, `SuppressionLaneTests`, `TemplateVariablesWikiTests`, `TenantEmailConfigurationTests`.

Messaging: `ConsoleMessagingServiceTests`, `DispatchMessageIntegrationEventHandlerTests`, `EmailTemplateBuilderTests`, `MessageDeliveryLogTests`, `MessagingEndpointsAuthorizationTests`, `ResendEmailServiceTests`.

CRM: `ClientProfileAnonymizedEventTests`, `ClientProfileCompanyNameTests`, `CrmOutboxInboxRegistrationTests`.

Commerce-adjacent used as evidence: `AnonymizeSubscriberCommandHandlerTests`, `CheckoutB2bIdentityTests`, `CreateCustomCheckoutAndInitiateSessionTests`, `DunningEngineJobTests`, `InvoiceReminderJobTests`, `SubscriberQueryServiceMailContextTests`, `SubscriptionBillingAmountTests` (Gross math itself is tested; mail is not).

---

## 10. Unread / not verified

These were **not** executed or opened. Do not infer they are clean.

- No test suite was run on this machine for this audit. Failures in CI are unknown.
- No live Resend webhook was posted. B08-M01 is from the HMAC construction plus Svix’s published key rule.
- Production `Jwt:Secret` / `Resend:WebhookSecret` values were not read from a deployed environment. `appsettings.json` defaults are empty. If prod injects a non-`whsec_` key that happens to be the raw decoded bytes as Latin-1, M01 could be accidentally live. There is no code comment claiming that.
- R2 objects were not listed. PDF leftover PII is from the upload path, not from a bucket listing.
- LHDN sandbox was not queried for a post-anonymize document.
- Inbox/outbox retention and dead-letter payloads were not dumped. Processed `ClientProfileAnonymized` rows likely still contain the pre-wipe email in `Data`.
- Frontend ops Templates / Email Settings / Subscribers pages were not re-audited (slice 09) except where they prove a backend contract (privacy Meta sentence; “Anonymize … Emails stop.”).
- TypeSpec `packages/api-spec/modules/communications/` was not line-diffed again. 008’s omitted paths (public unsubscribe, webhook, delivery-logs, notify) are assumed still omitted.
- `CreateClientProfileRequestDto` exists in generated contracts. No Minimal API maps it. Not re-checked in Scalar.
- Markdig version’s exact raw-HTML default was not package-probed; `DisableHtml()` is absent, which is the documented switch.
- Concurrent Resolve unique-index 500 was not reproduced against Postgres (in-memory tests will not see it).

---

## 11. Ranked open bugs

P0 first. WhatsApp-not-shipping is not on this list.

1. **B08-M01** — Resend webhook HMAC key is not Svix’s `whsec_` key. Bounce/complaint suppression is dead in any honest production config.
2. **B08-M02** — Custom-quote B2B Resolve writes CompanyName into IdValue. LHDN buyer BRN becomes a legal name.
3. **B08-M03** — Resolve-by-email freezes the first stranger’s TIN/NRIC/name onto later buyers.
4. **B08-M07** — Anonymize leaves receipt PDFs, MyInvois submissions, and delivery-log recipients.
5. **B08-M04** — First suppression reason wins; unsub hides a later bounce from the transactional lane.
6. **B08-M05** — Checkout gate trusts row shape; quotes skip the gate; revoked keys still take payment.
7. **B08-M06** — Buyer-controlled names (and merchant HTML on update/broadcast) land in email HTML.
8. **B08-M08** — Every one-time order gets a fake download mail.
9. **B08-M09** — Empty Jwt secret forges unsubscribe.
10. **B08-M10** — After Gross, cancel + wiki still say list price; dunning says Gross; same `{{amount}}` tag.

Then P2: M11 latent Create OR-phone, M12 unique vs resolve, M13 global email overwrite, M14 invoice reminder, M15 fail-mail empty amount, M16 tax-invoice copy, M17 update validation / missing hydrator tags, M18 broadcast counts, M19 notify TenantId, M20 FixedTimeEquals 500, M21 sender∉domains, M22 CRM get-by-id, M23 parser/200-on-error, M24 test reminder, M25 dummy cancel mail, M26 consent unreachable, M27 dual CMS, M28 `<br/>`.

**Do not open a P0 for Console WhatsApp.** Flag false, stub, cost 0, new-org +3 is EMAIL, engine demotes WA.

---

## 12. One paragraph

On `297ba98` Lazuar Pay still sends tenant Resend email for checkout-gated receipts, sequenced dunning, immediate declines, portal magic links, cancel, and quote reminders, and it still does not send WhatsApp. Gross wiring fixed **dunning** money (seats × snapshot + SST) and did not touch cancel, fail-mail, or the wiki. Unsubscribe POST does suppress. The Resend inbound signature is not Svix’s signature, so bounce/complaint suppression does not work with a real Signing Secret. CRM resolve is email-only and the custom B2B checkout path writes company name into the LHDN IdValue field. Anonymize wipes CRM and commerce log name/email and does not wipe Billing PDFs, MyInvois, or `MessageDeliveryLog`.

---

*End of uncondensed bug audit. Source of truth is the live tree under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` at `297ba98` on 17 August 2026, not `docs/001-gaps/08-communications-module.md`, not `plans/007-feats/16-communications-whatsapp-email.md`, and not `plans/008-evals/06-communications-email-whatsapp.md` except where this file re-checked a claim.*
