---
number: "260"
id: B07-I16
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 260 — B07-I16 — Slug uniqueness is check-then-act; update skips the check

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I16 — P2 — Slug uniqueness is check-then-act; update skips the check

**Where.** `RegisterPublicUserCommand.cs:45–49`; `CreateWorkspaceCommand.cs:42–47`; `UpdateWorkspaceCommand.cs:43` (no `IsSlugUniqueAsync`); `OneDbContext.cs:49`.

**What.** Concurrent create → 500 + leaked unique-violation (B07-I19). Update collision → same. Not an IDOR.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Workspace slugs are globally unique (`one.Organizations.Slug`). Create/register ask `IsSlugUniqueAsync` and then insert. Two concurrent creates of `acme` can both see unique=true and both insert; the second hits the unique index. Update never calls `IsSlugUniqueAsync` at all: `UpdateWorkspaceCommand` loads the org and `UpdateDetails(name, slug)`. A collision is the same unique-index 500. This is not IDOR — you cannot read another tenant by guessing a slug here — it is a check-then-act plus a missing update guard.

### Still present?
**STILL BROKEN**

Register/create still check-then-act:

```47:53:apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs
        if (createWorkspace)
        {
            var isSlugUnique = await _repository.IsSlugUniqueAsync(slug, ct);
            if (!isSlugUnique)
            {
                throw new InvalidOperationException("The requested workspace slug is already taken. Please choose another.");
            }
```

```42:47:apps/lazuar-api/Modules/One/Application/Commands/CreateWorkspaceCommand.cs
        var slug = request.Slug.Trim().ToLowerInvariant();
        var isSlugUnique = await _repository.IsSlugUniqueAsync(slug, ct);
        if (!isSlugUnique)
        {
            throw new InvalidOperationException("The requested workspace slug is already taken. Please choose another.");
        }
```

Update still has no uniqueness call — it only authorizes and writes:

```40:52:apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs
        var organization = await _repository.GetOrganizationByIdAsync(request.OrganizationId, ct);
        if (organization == null || !organization.IsActive)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        organization.UpdateDetails(request.Name, request.Slug);
        // ...
        await _repository.SaveChangesAsync(ct);
```

`IsSlugUniqueAsync` is a plain `AnyAsync` (`OneRepository.cs:39–41`). Unique index remains (`OneDbContext.cs:49`). Ops General Settings will PUT a new slug (`GeneralSettingsPage.tsx:81–88`). Residual after **122**: the 500 no longer leaks `23505` / constraint names (`GlobalExceptionHandler.cs:52–62`, `Unhandled_Exception_Is_500_Without_Provider_Text`). The client still gets a generic 500 instead of “slug taken.”

### Related files
- `apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` — create-time check.
- `apps/lazuar-api/Modules/One/Application/Commands/CreateWorkspaceCommand.cs` — same.
- `apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs` — no check.
- `apps/lazuar-api/Modules/One/Domain/Organization.cs:47–61` — `UpdateDetails` validates format only.
- `apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs:49` — unique index (the real guard).
- `apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs:39–41` — non-transactional exists.
- `apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` — merchant slug change UI.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` — 500 text after **122**.
- `apps/lazuar-api/Modules/One/Application/Commands/ProvisionAuraWorkspaceCommandHandler.Tenant.cs` — also check-then-act (sibling).

### Tests
- Existing: `Taken_Slug_Throws_And_Writes_Nothing` (register), `Duplicate_Slug_Throws_And_Writes_Nothing` (create), `UpdateWorkspaceCommandHandlerTests.SuperAdmin_Membership_Can_Update` / `Member_Cannot_Update` (no slug collision).
- Those tests stub `IsSlugUniqueAsync` as a list scan; they **pass** under a race and they **never** assert update-vs-existing-slug. `Unhandled_Exception_Is_500_Without_Provider_Text` only proves the leak is gone.
- First regression: (1) `UpdateWorkspace` to another org’s slug throws `InvalidOperationException` “already taken” **before** SaveChanges; (2) a test that simulates `SaveChanges` throwing `DbUpdateException` on slug unique is mapped to that same 400 (not 500). Concurrent create is hard in unit tests; an integration test with a unique-violation interceptor is enough.

### Reproduction today
Arrange: org A slug `acme`, org B slug `other`. Act as B’s ADMIN: General Settings → change slug to `acme` → Save. Assert: HTTP 500 `An unexpected error occurred.` (not 400 already taken). Arrange two parallel `POST /one/public/register` (or `POST /one/workspaces`) with the same free slug. Assert: one 200, one 500 generic — not a 400. Sequential second create still correctly 400 “already taken.”

### Blast radius
Merchants renaming or signing up for a popular slug. Not IDOR, not PII. After **122** they see a dead 500 instead of a Postgres dump. Frequency: rare on create (race window); certain on update whenever two live slugs collide. Checkout/portal URLs keyed by slug break if an update somehow succeeded — it will not succeed, it 500s, so the worse case is a failed rebrand.

### Suggested fix
1. `UpdateWorkspaceCommand`: if the cleaned slug differs, `IsSlugUniqueAsync` (or “unique ignoring this org id”) and throw the same `InvalidOperationException` as create. 2. Catch `DbUpdateException` unique-on-slug in the three write handlers (or a small helper) and rethrow as that same InvalidOperation so **122**’s generic 500 is not the user-visible path. Do not drop the unique index. No TypeSpec regen.

### Evaluation notes
Still P2. **122** removed the leak the audit paired with this (B07-I19); the race and the update hole remain. Provision slug loop is a sibling, not a fix. Not blocked. Not 161–200 fail-closed.

