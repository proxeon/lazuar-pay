---
number: "281"
id: B08-M11
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 281 — B08-M11 — CreateClientProfile `email OR phone` matches empty phones

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M11 — P2 — CreateClientProfile `email OR phone` matches empty phones

**Where:** `CreateClientProfileCommandHandler.cs` 25–28.

**What:** Latent. No production `new CreateClientProfileCommand`. Handler is live in the container. Empty phone ≡ every empty-phone row.

**Why it matters:** The next person who “just exposes CRM create” inherits a P0 merge. File it so they do not.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`CreateClientProfileCommandHandler` is still a live MediatR handler (Infrastructure assembly, auto-registered) that looks up an existing row with `OrganizationId == org && (Email == normalized || Phone == normalized)`. `NormalizePhone` maps blank/whitespace to `""`. The unique key on `crm.ClientProfiles` is `(OrganizationId, Email, Phone)`, and checkout/enroll write empty phone as `""`. So `Phone == ""` matches **every** empty-phone profile in the org. The first such row is returned as a “match,” optionally linking `GlobalUserId`, and no new row is inserted. The audit called this latent because no production `new CreateClientProfileCommand(...)` exists — checkout, custom quotes, and manual enroll all send `ResolveClientProfileCommand` (which, after **126**, matches email **and** phone). The next person who exposes “CRM create” over HTTP or calls Create from a linker will inherit a cross-buyer merge that looks like a P0 identity bug.

### Still present?
**STILL BROKEN**

Predicate is unchanged:

```25:38:apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs
        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId
                && (p.Email == emailNormalized || p.Phone == phoneNormalized), cancellationToken);

        if (existingProfile != null)
        {
            if (existingProfile.GlobalUserId == null && request.GlobalUserId.HasValue)
            {
                existingProfile.GlobalUserId = request.GlobalUserId.Value;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return existingProfile.Id;
        }
```

```76:78:apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
```

Grep of `new CreateClientProfileCommand` under `apps/` hits only `ClientProfileAnonymizedEventTests.cs:81` (constructs the record to assert `ConsentedToMarketing` defaults false). Handler + contract still exist; no CRM HTTP `Endpoints.cs`. Contrast resolve, which now matches the unique key (`ResolveClientProfileCommandHandler.cs:25–33`).

### Related files
- `apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs` — the OR match and empty-phone collapse.
- `apps/lazuar-api/Modules/CRM/Contracts/CreateClientProfileCommand.cs` — still in the container; default consent false.
- `apps/lazuar-api/Modules/CRM/Infrastructure/Configurations/ClientProfileConfiguration.cs` — unique `(OrganizationId, Email, Phone)` at line 15.
- `apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs` — the path production actually uses; do not “fix” Create by copying the old email-only resolve.
- `apps/lazuar-api/Modules/CRM/Infrastructure/DependencyInjection.cs` — no extra registration needed; MediatR picks the handler up.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/CRM/ClientProfileAnonymizedEventTests.cs` — only test that constructs Create.

### Tests
- Existing: `ClientProfileAnonymizedEventTests.CreateAndResolveCommands_DefaultConsentFalse` (constructs Create, does not call `Handle`). `ClientProfileCompanyNameTests` exercise **Resolve** only (`Resolve_SameEmail_DifferentPhone_KeepsTaxIdentitySeparate` is 126’s lock). There is no `CreateClientProfileCommandHandlerTests`.
- No current test would fail if the OR-phone bug is still there. The consent test would stay green.
- First regression test: seed two empty-phone profiles in one org (`a@x.com`/`""` and `b@x.com`/`""`); `Handle(Create(… email=c@x.com, phone=""))` must insert a **third** row, not return `a` or `b`. Second case: same email + different phone must not merge (mirror 126). Third: blank phone must not match a row that only shares a phone of `""`.

### Reproduction today
Arrange: in-memory `CrmDbContext`, org O, two `ClientProfileEntity` rows `(O, ada@example.com, "")` and `(O, ben@example.com, "")`. Act: `new CreateClientProfileCommandHandler(db).Handle(new CreateClientProfileCommand(O, "Cara", "cara@example.com", "", …))`. Assert: returned id is one of the existing ids (today: first empty-phone row via `FirstOrDefault`), row count stays 2. Repeat with a future HTTP surface (`POST` CRM create) if one is added — same merge.

### Blast radius
Not on the live checkout path (Resolve is). Hurt party is whoever next “just exposes CRM create” plus every empty-phone buyer in that tenant (most hop-1 guests: `InitiateCheckoutCommand.Phone` is optional). Merge writes `GlobalUserId` onto the wrong row if the command carries one — then **283** will overwrite that stranger’s name/email from One. PII / tax identity, not money capture. Frequency: zero in production until Create is called; then every Create with empty phone. Keep **P2** as filed (latent); treat as P0 **if** you add an endpoint without fixing the predicate.

### Suggested fix
Change Create’s lookup to the same `(OrganizationId, Email, Phone)` predicate Resolve uses (lines 29–33). Do not keep OR. Treat empty phone as part of the unique key, not a wildcard. Optionally refuse Create when both email and phone are empty. Do not delete the handler “because unused” without an architecture-test lock — it is still in the container. Do not re-merge by email (that is 126, resolved). No TypeSpec, no WhatsApp, no Wave 5.

### Evaluation notes
Overlaps **126 / B08-M03** (resolved `fix/126-crm-email-merge`) and **282 / B08-M12** (this range). 126 fixed Resolve, not Create. 282 is the unique-index vs resolve-by-email tension; after 126 Resolve matches the unique key, so 281 is the remaining “wrong matcher.” **292 / 165** scoped `GetClientProfileAsync` to org — unrelated to this predicate. Still P2 because latent. Not blocked.

