---
number: "283"
id: B08-M13
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 283 — B08-M13 — GlobalUserProfileUpdated overwrites every linked CRM email

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M13 — P2 — GlobalUserProfileUpdated overwrites every linked CRM email

**Where:** `GlobalUserProfileUpdatedIntegrationEventHandler.cs` 20–33.

**What:** All `GlobalUserId == user` rows, every tenant, get `FullName` and `Email` from One. No uniqueness pre-check. Can collide with `(org, newEmail, phone)`. Can change the email anonymize will later scrub logs against (B08-M07).

Guest checkout does not set `GlobalUserId`. Resolve does not either. This fires for Create-linked or subsequently linked profiles.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
When One publishes `GlobalUserProfileUpdatedIntegrationEvent`, CRM loads **every** `ClientProfileEntity` with that `GlobalUserId` (`IgnoreQueryFilters`, no `OrganizationId` predicate) and writes `FullName` and `Email` from the event onto each row. There is no uniqueness pre-check against `(OrganizationId, Email, Phone)`. `UpdateProfile` today only accepts a new **name** (`UpdateProfileCommand` / `PUT /one/me/profile`); `GlobalUser` email is not mutated by that path, so the Email assignment is currently a same-value write. The live harm is: (1) a merchant’s launchpad name overwrites every linked buyer-facing `FullName` in every tenant (receipts, dunning greeting, LHDN display); (2) if anyone later adds an email-change path, the same loop will collide with another profile’s unique `(org, newEmail, phone)` or desync the address **130 / B08-M07** anonymize later scrubs logs against. Guest checkout and Resolve still do not set `GlobalUserId`; Create can. CRM README still documents this fan-out as intentional “data consistency.”

### Still present?
**STILL BROKEN**

```20:33:apps/lazuar-api/Modules/CRM/Infrastructure/EventHandlers/GlobalUserProfileUpdatedIntegrationEventHandler.cs
        var profiles = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .Where(p => p.GlobalUserId == @event.UserId)
            .ToListAsync();

        if (!profiles.Any()) return;

        foreach (var profile in profiles)
        {
            profile.FullName = @event.Name;
            profile.Email = @event.Email;
        }

        await _dbContext.SaveChangesAsync();
```

Producer is name-only but still ships current email:

```47:52:apps/lazuar-api/Modules/One/Domain/GlobalUser.cs
    public void UpdateProfile(string name)
    {
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new GlobalUserProfileUpdatedDomainEvent(Id, Email, Name));
```

```20:26:apps/lazuar-api/Modules/One/Application/EventHandlers/GlobalUserProfileUpdatedDomainEventHandler.cs
    public async Task Handle(GlobalUserProfileUpdatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new GlobalUserProfileUpdatedIntegrationEvent(
            notification.UserId,
            notification.Email,
            notification.Name
        ));
```

Resolve create block (`ResolveClientProfileCommandHandler.cs:107–120`) still does not set `GlobalUserId`. No test file references `GlobalUserProfileUpdated`.

### Related files
- `apps/lazuar-api/Modules/CRM/Infrastructure/EventHandlers/GlobalUserProfileUpdatedIntegrationEventHandler.cs` — the fan-out write.
- `apps/lazuar-api/Modules/CRM/Infrastructure/DependencyInjection.cs` — handler subscribed on `CrmEventBus` (lines 32, 43).
- `apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` / `UpdateProfileCommand.cs` / `ProfileEndpoints.cs` — name-only producer.
- `apps/lazuar-api/Modules/One/Contracts/GlobalUserProfileUpdatedIntegrationEvent.cs` — payload still includes Email.
- `apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs` — only production-shaped writer that can set `GlobalUserId` (latent; 281).
- `apps/lazuar-api/Modules/CRM/README.md` lines 29–30 — documents cross-tenant overwrite as a feature.
- `apps/lazuar-api/Modules/CRM/Infrastructure/Configurations/ClientProfileConfiguration.cs` — unique `(org, email, phone)` the email write can collide with.

### Tests
- Existing: **none** under `apps/lazuar-api/tests/` mention `GlobalUserProfileUpdated`. `CrmQueryServiceTenantIsolationTests` is 165 (get-by-id + org). `ClientProfileAnonymizedEventTests` covers wipe, not this sync.
- No test would fail if the bug is still there.
- First regression: two orgs, two profiles linked to the same `GlobalUserId` with distinct `FullName`/`Email`; publish the event; assert only the intended tenant row moves, **or** (if product wants name sync) that Email is not written and unique-key collision is refused. Second: linked profile email `old@x.com` + sibling `(org, new@x.com, samePhone)` + event email `new@x.com` must not 500 / must not clobber the sibling.

### Reproduction today
Arrange: CRM row with `GlobalUserId = U`, `FullName = "Buyer Ada"`, `Email = ada@buyer.com` in tenant A; same `U` linked in tenant B if you can (Create). Act: `PUT /api/v1/one/me/profile` `{ "name": "Ada Merchant" }` as user U. Assert: both CRM `FullName`s become `"Ada Merchant"`; `Email` rewritten to One’s email (same string today). No HTTP 409 if a unique collision is later possible — `SaveChanges` throws.

### Blast radius
Only profiles with `GlobalUserId` set (not hop-1 guests). Hurt: buyer-facing greetings and any downstream that reads CRM name/email (dunning, receipts, portal). If email change is added later: unique-index 500 or wrong-inbox mail; anonymize log scrub (**130 / B08-M07**) uses the new email and leaves old-address rows. Frequency: every `PUT /me/profile`. No money capture. Still **P2** — live name clobber is real but email collision is latent while One email is immutable.

### Suggested fix
Stop writing `Email` unless One actually gains a verified email-change command (and then only after a uniqueness check per org). Scope the update to a single tenant if the event is not meant to be a global buyer rename — merchant launchpad name ≠ buyer CRM name. If product insists on name sync, update `FullName` only and skip rows that look like distinct buyers (different email than One). Catch unique violations instead of 500. Do not set `GlobalUserId` from Resolve as a “fix.” No TypeSpec.

### Evaluation notes
Touches **130 / B08-M07** (anonymize email used for log scrub). **281** is how `GlobalUserId` would get set if Create is exposed. **165 / 292**: `GetClientProfileAsync` is already org-scoped (`CrmQueryService.cs:54–59`); this handler is a different global walk. Still P2. Not blocked.

