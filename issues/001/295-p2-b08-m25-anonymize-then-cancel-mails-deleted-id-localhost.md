---
number: "295"
id: B08-M25
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 295 — B08-M25 — Anonymize then cancel mails `deleted_{id}@localhost`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M25 — P2 — Anonymize then cancel mails `deleted_{id}@localhost`

**Where:** order in §4.14 step 6; `LifecycleEventHandlers` 46–48.

**What:** Not a PII leak (dummy + real address suppressed). It is a wasted Resend call and a FAILED delivery-log row that support will read as “we emailed the deleted user.”

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Subscriber Anonymize wipes CRM to `deleted_{profileId}@localhost`, suppresses the **pre-wipe** address as `ANONYMIZED`, then Commerce cancels every non-canceled subscription and publishes `SubscriptionCanceledIntegrationEvent`. `LifecycleEventHandlers` then loads the profile **after** the wipe and uses `profile.Email` as `ToEmail`. The dummy is non-empty, so cancel catalog mail is dispatched. Dispatch does not treat `deleted_*@localhost` as suppressed (suppression is on the real inbox). Resend is called with a fake recipient; the delivery log is FAILED or a bounce. Support reading Developer/delivery rows will think Lazuar emailed the deleted user. The real inbox is not contacted. `ClientProfileEntity.IsAnonymizedEmail` already exists and is unused on this path.

### Still present?
**STILL BROKEN**

Wipe + dummy address:

```27:31:apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs
    public void Anonymize()
    {
        FullName = "Anonymized User";
        Email = $"deleted_{Id}@localhost";
```

Commerce still cancels and publishes after anonymize (`ClientProfileAnonymizedIntegrationEventHandler.cs:63–73`). Cancel mail still sends to whatever CRM now has:

```46:48:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs
        var profile = await _crmQueryService.GetClientProfileAsync(@event.OrganizationId, @event.ClientProfileId);
        var toEmail = profile?.Email;
        if (profile == null || string.IsNullOrEmpty(toEmail)) return;
```

The helper that should short-circuit is unused here:

```20:25:apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs
    public bool IsAnonymized() => IsAnonymizedEmail(Email);

    public static bool IsAnonymizedEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.StartsWith("deleted_", StringComparison.OrdinalIgnoreCase)
        && email.EndsWith("@localhost", StringComparison.OrdinalIgnoreCase);
```

Communications anonymize consumer correctly skips dummy emails when inserting suppressions (`ClientProfileAnonymizedIntegrationEventHandler.cs:36–39`). That does **not** stop cancel dispatch.

### Related files
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` — cancel mail after wipe.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` — cancel fan-out that publishes the event.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/AnonymizeSubscriberCommandHandler.cs` — HTTP entry (CRM wipe only; cancel is the consumer).
- `apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs` — dummy address + unused `IsAnonymizedEmail`.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` — will Resend the dummy and write FAILED.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/LifecycleEventHandlersTests.cs` — cancel mail with a live address only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/AnonymizeSubscriberCommandHandlerTests.cs` — cancel publish, no mail assertion.

### Tests
- Existing: `LifecycleEventHandlersTests.Cancel_PopulatesSubjectAndNames`, `Cancel_MissingProfile_DoesNotDispatch`, `Cancel_MissingMailContext_StillDispatchesWithLinks`. CRM: `ClientProfileAnonymizedEventTests.Anonymize_WipesPiiAndConsent`, `IsAnonymizedEmail_MatchesDummyOnly`. Communications: `ClientProfileAnonymizedSuppressionTests.HandleAsync_DummyEmail_DoesNotSuppress`. Commerce: `AnonymizeSubscriberCommandHandlerTests` cancel-publish test (~line 173).
- Would any test fail if the bug is still there? No. Missing-profile is the only skip. Dummy email is not covered.
- First regression: after anonymize, `LifecycleEventHandlers.HandleAsync` must **not** publish `DispatchMessageIntegrationEvent` when `IsAnonymizedEmail(profile.Email)`.

### Reproduction today
Arrange an ACTIVE subscriber with a real email and a “Subscription Cancelled” template. Act: `POST /admin/commerce/subscribers/{id}/anonymize`. Assert: CRM email is `deleted_{id}@localhost`; real address has an `ANONYMIZED` suppression; a `DispatchMessageIntegrationEvent` (or Messaging delivery log) exists for `deleted_{id}@localhost` with FAILED/bounce. The real inbox must not receive cancel mail.

### Blast radius
Ops/support noise and a wasted Resend request per anonymized subscription (often one, sometimes many if the profile had several subs). Not a PDPA leak of the live address. Frequency = every Anonymize click. Sibling of 287 (PDFs / LHDN / delivery-log PII that stay).

### Suggested fix
In `LifecycleEventHandlers`, after loading the profile, return if `ClientProfileEntity.IsAnonymizedEmail(toEmail)` (or if `Full_name` is the wipe sentinel). Optionally skip publishing `SubscriptionCanceled` mail from the anonymize consumer with a flag — the handler-side skip is smaller. Do not email the pre-wipe address (that is the point of suppress). Do not enable WhatsApp.

### Evaluation notes
Still P2. Not fixed by 165 (org scope) or by dummy-email suppression skip. `IsAnonymizedEmail` was added for this class of bug and never wired into cancel mail. Do not mark resolved.

