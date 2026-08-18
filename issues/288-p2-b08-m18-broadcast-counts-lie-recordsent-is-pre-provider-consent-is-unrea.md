---
number: "288"
id: B08-M18
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 288 — B08-M18 — Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M18 — P2 — Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout

**Where:** `SendBroadcastCommandHandler` 35; `GetActiveSubscriberCountAsync` vs `GetActiveSubscriberRecipientsAsync`; `BroadcastFanoutJob` 174–185; `InitiateCheckoutCommand` (no consent field); `Resolve` (does not write consent).

**What:** Preview count ≥ people who will receive. `ConsentedToMarketing` defaults false and checkout never sets it, so fan-out of a “successful” broadcast to a fresh tenant is **zero consenting recipients** after a non-zero preview. `RecordSent` before Resend. `FailedCount` stays 0. ADR 021: do not productize. Still a lying API.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Three stacked lies on the marketing-broadcast API (not a “build Chargebee broadcasts” ticket). Preview and `SendBroadcastCommandHandler` set `TotalRecipients` from `GetActiveSubscriberCountAsync` (every `ACTIVE`/`PAST_DUE` subscription). Fan-out then calls `GetActiveSubscriberRecipientsAsync`, which **drops** anyone without `Consented_to_marketing`. Checkout (`InitiateCheckoutCommand`) has no consent field; Resolve defaults `ConsentedToMarketing = false`; entity/DB default false. A fresh tenant with paying subscribers previews N and sends 0. `BroadcastFanoutJob` `RecordSent()`s after `PublishAsync` to the Communications outbox, not after Resend 200; `RecordFailed()` is never called in the per-recipient loop, so `FailedCount` stays 0 when the provider later fails. TypeSpec copy still says fan-out is “ACTIVE/PAST_DUE with marketing consent” — half true. ADR 021: do not productize this surface; still do not ship a lying count.

### Still present?
**STILL BROKEN**

```35:40:apps/lazuar-api/Modules/Communications/Application/Commands/BroadcastCommandHandlers.cs
        var recipientCount = await _subscriberQueryService.GetActiveSubscriberCountAsync(request.OrganizationId);
        if (recipientCount == 0)
            throw new BusinessRuleValidationException(new GenericBusinessRule("No active subscribers to broadcast to."));

        var broadcast = new Broadcast(request.OrganizationId, request.Subject, request.EmailBody);
        broadcast.Queue(recipientCount);
```

Preview uses the same count (`BroadcastEndpoints.cs:51–57`). Recipients filter consent:

```76:82:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs
            // Marketing broadcasts require explicit marketing consent (PDPA).
            if (profiles.TryGetValue(row.ClientProfileId, out var profile)
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Consented_to_marketing)
            {
                result.Add(new SubscriberRecipient(row.Id, profile.Email, profile.Phone, profile.Full_name));
```

Checkout still has no consent (`InitiateCheckoutCommand.cs:7–30`). Resolve default false (`ResolveClientProfileCommand.cs:16`). Fan-out records sent pre-provider (`BroadcastFanoutJob.cs:178–189`); `RecordFailed` exists on the aggregate (`Broadcast.cs:65`) but is not used in the loop. `Failed_count` is exposed (`BroadcastEndpoints.cs:80`).

### Related files
- `apps/lazuar-api/Modules/Communications/Application/Commands/BroadcastCommandHandlers.cs` — inflated `Queue(count)`.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs` — preview + status DTOs.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs` — count vs consenting recipients.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` — RecordSent before Resend.
- `apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs` — no consent field.
- `apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs` / `ClientProfileEntity.cs` — default false (correct PDPA).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/BroadcastTests.cs` — aggregate only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/BroadcastClaimTests.cs` — SKIP LOCKED claim only.

### Tests
- Existing: `BroadcastTests.Queue_SetsRecipients`, `RecordSent_AccumulatesSentCount`, `MarkFailed_SetsReason`; `BroadcastClaimTests.ClaimQueued_MarksSending_AndSkipsAlreadySending`; `ClientProfileAnonymizedEventTests.CreateAndResolveCommands_DefaultConsentFalse`. No test that preview count equals consenting recipients. No test that provider failure increments `FailedCount`.
- None would fail if preview is still N and fan-out is 0, or if RecordSent stays pre-Resend. `DefaultConsentFalse` would fail if someone “fixed” this by forcing consent true — do not do that (007 gap is closed on purpose).
- First regression: N ACTIVE subscribers, 0 consenting → preview `recipient_count = 0` (or a separate `consenting_count`) and Send refuses with the same truth; if you keep N, status must show `skipped_no_consent`. Second: stub Resend throw after outbox publish → `FailedCount >= 1`, `SentCount` not incremented (or incremented only on provider 200).

### Reproduction today
Arrange: tenant with 3 ACTIVE subscriptions from hop-1 (no consent checkbox). Act: `GET /api/v1/admin/communications/broadcasts/preview` → `recipient_count = 3`. `POST /admin/communications/broadcasts` succeeds and queues 3. Wait for `BroadcastFanoutJob`. Assert: `GET /broadcasts/{id}` shows `sent_count = 0`, `failed_count = 0`, `total_recipients = 3`, status COMPLETED. Optionally break Resend: still `sent_count` ticks as outbox rows enqueue, `failed_count` stays 0.

### Blast radius
Merchants who trust preview/status to mean “we emailed N buyers.” Fresh tenants: successful broadcast, zero mail (PDPA-correct, API-false). `RecordSent` pre-provider: support sees Sent while delivery log is FAILED. Marketing lane only (receipts/dunning unaffected). Frequency: every broadcast on a tenant that never wrote consent. Still **P2**. Do not flip consent default to true.

### Suggested fix
Point preview and `Queue` at the same consenting query fan-out uses (or return both counts). Move `RecordSent` to the Messaging dispatch success path (or increment failed when `DispatchMessage` / Resend throws before save). Do **not** add a checkout consent checkbox in this ticket unless product asks — that is **296 / B08-M26** and a PDPA copy change. Do not productize WhatsApp broadcasts (Wave 5 / Decision 00.4). No TypeSpec regen required if you only correct existing DTO field meanings; if you add `skipped_no_consent`, that is a contract change — prefer reusing `total_recipients` as the consenting count.

### Evaluation notes
**296 / B08-M26** is the checkout-consent half (open, next range). **128** is HasValidEmailConfig, not this. ADR 021: keep this an honest internal API, not a marketing product. Still P2. Not blocked by 165/292.

