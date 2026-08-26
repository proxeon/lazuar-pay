---
number: "285"
id: B08-M15
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 285 — B08-M15 — Immediate fail amount is empty; context port cannot carry Gross

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M15 — P2 — Immediate fail amount is empty; context port cannot carry Gross

**Where:** `CommerceSubscriptionCommsContext` (three fields); `GatewayPaymentFailedIntegrationEventHandler` 88–91.

**What:** Catalog does not print amount. Custom templates get `""`. Port would have to grow (or fail-mail should call the same Gross helper cancel should call).

Tests lock the update-payment URL and “no `{{` leftovers” (`GatewayPaymentFailedEmailHandlerTests` 60–76). They do not assert amount. Empty replace leaves no `{{amount}}` if the catalog omits the tag — the test cannot see the hole.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Immediate Payment Failed mail (`Communications` `GatewayPaymentFailedIntegrationEventHandler`) builds `MessageTemplateContext` with `Amount`, `TotalPrice`, and `Currency` all `""`. The catalog “Payment Failed” body does not print money (update-payment link + plan name only), so default tenants never notice. A merchant who adds `{{amount}}` from the wiki / cancel / dunning docs gets a blank. The Commerce port `CommerceSubscriptionCommsContext` is still three fields (`ClientProfileId`, `Status`, `ProductName`) — it cannot carry Gross. Sibling **133 / B08-M10** taught cancel/lifecycle to use `SubscriberQueryService.GetSubscriptionMailContextAsync`, which **does** call `SubscriptionBillingAmount.Gross`. Fail-mail never calls that helper. Tests still assert the HMAC update-payment URL and `Contains("{{") == false`; empty replace of a tag the catalog does not include leaves no leftover, so the suite cannot see the hole.

### Still present?
**STILL BROKEN**

Port is still three fields:

```63:66:apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs
public record CommerceSubscriptionCommsContext(
    Guid ClientProfileId,
    string Status,
    string? ProductName);
```

```118:135:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
    public async Task<CommerceSubscriptionCommsContext?> GetSubscriptionCommsContextAsync(...)
    {
        // ...
        return new CommerceSubscriptionCommsContext(sub.ClientProfileId, sub.Status, product?.Name);
    }
```

Fail handler still blanks money:

```82:95:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs
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

Catalog omits amount (`DefaultMessageTemplates.cs:26–32`). Tests (`GatewayPaymentFailedEmailHandlerTests.cs:60–76`) do not mention amount. Contrast Gross already used for subscription mail context (`SubscriberQueryService.cs:101–112`).

133 is resolved (`fix/133-cancel-mail-gross`) — that is cancel, not this handler.

### Related files
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` — empty amount.
- `apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs` — port too small.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` — implements the three-field snapshot.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs` — `GetSubscriptionMailContextAsync` already has Gross; fail-mail should reuse it or grow the port.
- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — default body hides the hole.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/GatewayPaymentFailedEmailHandlerTests.cs` — URL / leftover lock.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/LifecycleEventHandlersTests.cs` — cancel sibling (133).

### Tests
- Existing: `GatewayPaymentFailedEmailHandlerTests.GatewayPaymentFailed_DispatchesPaymentFailed_WithUpdatePaymentUrl` (lines 27–77); `GatewayPaymentFailed_CanceledSub_NoDispatch`; `GatewayPaymentFailed_NoSubscriptionMetadata_NoDispatch`. Commerce `GatewayPaymentFailedIntegrationEventHandlerTests` cover PAST_DUE / campaign, not mail amount.
- None fail if amount stays `""`. `Contains("{{") == false` **locks the hole** for the catalog body.
- First regression: seed a custom “Payment Failed” body `Pay {{amount}} {{currency}}` (or assert the context). After a failed renewal of 5×99 + SST, mail must contain the same Gross string dunning/cancel use (e.g. `534.60` / `MYR`), not `""` and not list `99.00`. Keep the update-payment URL assertions.

### Reproduction today
Arrange: ACTIVE subscription, vaulted card, tenant “Payment Failed” template edited to include `{{amount}} {{currency}}`. Act: decline an off-session renewal (`GatewayPaymentFailedIntegrationEvent` with `subscription_id`). Assert: dispatched HTML has empty amount/currency where the tags were (or the tags survive only if someone stops replacing them — today they become `""`). Default catalog tenants: mail looks fine, no money printed.

### Blast radius
Buyers on a declined renewal whose merchant customized Payment Failed using the wiki’s amount tag. Default catalog: no user-visible lie. Support/wiki: merchants “fix” dunning/cancel and copy tags here. No money mis-capture. Frequency: every live decline that sends this mail. Still **P2** (catalog hides it). **133** was P1 because cancel/wiki actively printed the wrong number.

### Suggested fix
Smallest: grow `CommerceSubscriptionCommsContext` with Gross + currency (call the same `SubscriptionBillingAmount.Gross` cancel uses), or have the fail handler call `ISubscriberQueryService.GetSubscriptionMailContextAsync` and map those fields. Fill `Amount`/`TotalPrice`/`Currency` via `MessageTemplateHydrator.FormatMoney`. Do not print list price. Do not subscribe to Stripe `subscription.updated`. LP-059 stays next-renewal-only. No TypeSpec. No WhatsApp product work.

### Evaluation notes
Third sibling of **133 / B08-M10** (resolved). Audit text already named it. Do not reopen 133 unless cancel regresses. Dual-CMS / Payment Failed vs dunning step copy is **297 / B08-M27** (next range). Still P2. Not blocked.

