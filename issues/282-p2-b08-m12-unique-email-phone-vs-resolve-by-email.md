---
number: "282"
id: B08-M12
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 282 — B08-M12 — Unique `(Email, Phone)` vs resolve-by-email

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M12 — P2 — Unique `(Email, Phone)` vs resolve-by-email

**Where:** `ClientProfileConfiguration.cs` 15; `ResolveClientProfileCommandHandler.cs` 26–28.

**What:** Two rows with the same email and different phones can exist (Create path, or a future writer). Resolve picks one without `OrderBy`. Concurrent first inserts of `(org, email, "")` race the unique index and 500.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
The audit filed a three-part tension: unique index is `(OrganizationId, Email, Phone)`, Resolve used to match **email only** and `FirstOrDefault` without `OrderBy` (so two same-email/different-phone rows silently collapsed to whichever row EF returned), and two concurrent first inserts of `(org, email, "")` race that unique index into an unhandled 500. **126** (`fix/126-crm-email-merge`, commit `2eec0b9e`) changed Resolve to match the unique key (`email AND phone`) and added `Resolve_SameEmail_DifferentPhone_KeepsTaxIdentitySeparate`. Same inbox + different phone is now **two buyers** by design. The “picks one without OrderBy” half is gone on the Resolve path. What remains: (1) the schema still allows same-email/different-phone pairs, and **281**’s Create OR-matcher plus `GetClientProfileByEmailAsync` (`FirstOrDefault` by email only, no `OrderBy`) will still pick an arbitrary sibling; (2) Resolve still has no `DbUpdateException` handling on insert, so two hop-1s of the same email with empty phone still 500 on Postgres.

### Still present?
**PARTIAL**

Resolve now matches the unique key (126). Comment in-tree:

```25:33:apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs
        // Match the unique key (org, email, phone). Same inbox + different phone is
        // a different buyer — do not merge tax identity onto the first TIN.
        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.OrganizationId == request.OrganizationId
                     && p.Email == emailNormalized
                     && p.Phone == phoneNormalized,
                cancellationToken);
```

Unique index unchanged:

```15:15:apps/lazuar-api/Modules/CRM/Infrastructure/Configurations/ClientProfileConfiguration.cs
        builder.HasIndex(x => new { x.OrganizationId, x.Email, x.Phone }).IsUnique();
```

Insert still has no race handling (`ResolveClientProfileCommandHandler.cs:122–123` `AddAsync` + `SaveChangesAsync`). `GetClientProfileByEmailAsync` is still email-only `FirstOrDefault` (`CrmQueryService.cs:82–85`) — LHDN refund / B2B invoice / portal magic-link callers can still attach the wrong sibling when two phones share an inbox.

Likely fix for the resolve-by-email half: **126** / `fix/126-crm-email-merge`.

### Related files
- `apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs` — current unique-key match; unguarded insert.
- `apps/lazuar-api/Modules/CRM/Infrastructure/Configurations/ClientProfileConfiguration.cs` — unique `(org, email, phone)`.
- `apps/lazuar-api/Modules/CRM/Infrastructure/CreateClientProfileCommandHandler.cs` — still OR (281); can still write a second same-email row.
- `apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs` — `GetClientProfileByEmailAsync` still email-only.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` — production Resolve caller (empty phone common).
- `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs` / `CreateManualSubscriberCommandHandler.cs` — same.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/CRM/ClientProfileCompanyNameTests.cs` — `Resolve_SameEmail_DifferentPhone_KeepsTaxIdentitySeparate` locks 126, not the 500.

### Tests
- Existing: `ClientProfileCompanyNameTests.Resolve_SameEmail_DifferentPhone_KeepsTaxIdentitySeparate` (two rows, tax identity separate); `Resolve_StoresCompanyNameAndTin_LeavesIdValueNull`; `Resolve_Enrich_FillsBlankTinAndCompany_DoesNotOverwriteExistingTin`; `CheckoutB2bIdentityTests` stubs mediator and never hits the unique index. No test that two concurrent Resolves of `(org, email, "")` surface a 409/business rule instead of `DbUpdateException`.
- `Resolve_SameEmail_DifferentPhone_…` would **fail** if someone reverted Resolve to email-only merge. It would **not** fail if the race 500 is still there. It would not fail if `GetClientProfileByEmailAsync` still picks the wrong sibling.
- First remaining regression: two parallel `Resolve(org, email, "")` against empty table — second must not 500 (retry-on-unique or serialize). Second: `GetClientProfileByEmailAsync` with two same-email rows — document whether it is allowed to be ambiguous, or take phone / fail closed.

### Reproduction today
Arrange: empty CRM org. Act A: `Resolve(email=shared@x.com, phone=60111111111)` then `Resolve(email=shared@x.com, phone=60122222222)` — assert two rows (this **passes** after 126). Act B: two simultaneous hop-1 `POST /public/commerce/checkout` (or two `Resolve`) with the same email and omitted phone — on Postgres expect unique violation 500 from `SaveChangesAsync`. Act C: call `GetClientProfileByEmailAsync(org, "shared@x.com")` after Act A — you get whichever row `FirstOrDefault` returns, no `OrderBy`.

### Blast radius
Act A is now correct (no TIN bleed). Act B hits every pair of guests who share an inbox and skip phone — support sees a 500 on hop-1, one buyer may already have a session. Act C can put the wrong NRIC on an LHDN document or magic-link the wrong subscription (`RequestPortalMagicLinkCommandHandler`, `B2bTaxInvoiceRequestedIntegrationEventHandler`, `GatewayRefundCompletedIntegrationEventHandler`). Money: failed checkout, not double-charge. Frequency: Act B is rare (same-second same email); Act C becomes real as soon as 126’s two-row world exists. Remaining work is still **P2**; the P1 merge is 126 and is resolved.

### Suggested fix
Do not revert 126. For the race: catch unique violation on Resolve insert and re-read the winning row (same pattern as `InvoiceReminderJob`’s `DbUpdateException` swallow). For email-only readers: either require phone, or fail closed when more than one row matches the email. Align **281** Create with the unique-key predicate so it cannot invent a second same-email row via OR. Do not add a unique-on-email-only index (that would undo 126). No TypeSpec.

### Evaluation notes
**126 / B08-M03** already scoped the “strangers by email” P1. This ticket’s resolve-by-email pick-one is that fix. Do not re-do 126 here. **281** is the Create OR leftover. **292** is `GetClientProfileAsync` global-by-id (165 already scoped that to org — next range). Still P2 for the race + email-only readers. Not blocked.

