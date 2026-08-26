---
number: "280"
id: B07-I40
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 280 — B07-I40 — `UserRegisteredDomainEvent` is orphaned; verify never starts at register

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I40 — P2 — `UserRegisteredDomainEvent` is orphaned; verify never starts at register

**Where.** `GlobalUser.cs:44`; grep of handlers.

**What.** Completes B07-I02’s verify half. Resend is the only mint.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`GlobalUser` still raises `UserRegisteredDomainEvent` in its constructor, but the One module has no `INotificationHandler<UserRegisteredDomainEvent>` (handlers exist for password-reset, verify-requested, invite, org created/updated, and profile-updated — not register). `RegisterPublicUserCommandHandler` constructs the user, persists, issues the merchant cookie, and returns `Is_email_verified = false`. It never calls `SetEmailVerificationToken`, so `EmailVerificationRequestedDomainEvent` never fires and no verify mail is staged. The only mint is `POST /one/auth/resend-verification` → `ResendVerificationEmailCommandHandler`. Login (`POST /one/auth/login`) does not look at `IsEmailVerified`. 112 (`fix/112-reset-verify-404`) added Ops `/verify-email` and made verify accept an `email` query, so the *click* half of B07-I02 is no longer 404 — but register still never starts the flow. A new engineer can treat this as “wire register to the existing resend mint, do not invent a second token scheme.”

### Still present?
**STILL BROKEN**

`UserRegisteredDomainEvent` is still only the record + ctor. Grep under `apps/` finds no handler type.

```44:44:apps/lazuar-api/Modules/One/Domain/GlobalUser.cs
        AddDomainEvent(new UserRegisteredDomainEvent(Id, Email, Name));
```

```5:8:apps/lazuar-api/Modules/One/Domain/Events/UserRegisteredDomainEvent.cs
public record UserRegisteredDomainEvent(Guid UserId, string Email, string Name) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
```

`NotificationDispatchDomainEventHandlers` implements reset / verify-requested / invite only:

```12:16:apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs
public class NotificationDispatchDomainEventHandlers :
    INotificationHandler<PasswordResetRequestedDomainEvent>,
    INotificationHandler<EmailVerificationRequestedDomainEvent>,
    INotificationHandler<WorkspaceInvitationCreatedDomainEvent>
```

Register never mints:

```66:87:apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs
        var user = new GlobalUser(email, name, passwordHash, isSystemAdmin: false);
        _repository.AddGlobalUser(user);
        // ... memberships / entitlements ...
        await _repository.SaveChangesAsync(ct);

        return user.Id;
```

Resend is still the only mint (`SetEmailVerificationToken` at `ResendVerificationEmailCommand.cs:34`). Login at `AuthEndpoints.cs:95–116` checks active + password only. Register immediately `IssueCookie`s (`AuthEndpoints.cs:69`).

Likely related resolved work: **112** (`fix/112-reset-verify-404`) — Ops `VerifyEmailPage.tsx` + `/verify-email` route exist; verify endpoint now takes `email` query (`AuthEndpoints.cs:148–184`) and is not session-bound. That does **not** start verify at register.

### Related files
- `apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` — ctor raises the orphan event; `SetEmailVerificationToken` is the existing mint.
- `apps/lazuar-api/Modules/One/Domain/Events/UserRegisteredDomainEvent.cs` — unused payload (`UserId`, `Email`, `Name`).
- `apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` — production register path; no token.
- `apps/lazuar-api/Modules/One/Application/Commands/ResendVerificationEmailCommand.cs` — only live mint.
- `apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` — already builds the verify mail from `EmailVerificationRequestedDomainEvent` (Ops URL after 112).
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` — register, login, resend, verify HTTP.
- `apps/lazuar-ops/src/pages/VerifyEmailPage.tsx` — click target after 112; unused until a token exists.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/RegisterPublicUserCommandHandlerTests.cs` — locks `IsEmailVerified == false`, does not assert a token.

### Tests
- Existing tests that touch this path: `RegisterPublicUserCommandHandlerTests.HappyPath_Creates_User_Workspace_Admin_And_Core_Entitlements` (asserts `IsEmailVerified` is false, line 94); `Empty_Workspace_Creates_User_Only`; no test named for `UserRegisteredDomainEvent` or `EmailVerificationTokenHash` on register. `ResendVerificationEmailCommand` has no dedicated test file under `apps/lazuar-api/tests/`. `PlatformAdminAuthQueryTests` only asserts a found admin’s `IsEmailVerified` is true.
- None of those would fail if the bug is still there. `HappyPath_Creates_User_…` would stay green even if a handler were added *unless* register also flipped `IsEmailVerified`.
- First regression test: after `RegisterPublicUserCommand`, assert `EmailVerificationTokenHash` and `EmailVerificationExpiresAt` are set (or that `EmailVerificationRequestedDomainEvent` was raised / `DispatchMessageIntegrationEvent` was published to the system tenant). Assert login still succeeds unverified unless product later requires verify (do not sneak that in). Assert register does not require a second “resend” to produce a token.

### Reproduction today
Arrange: empty One DB (or unique email). Act: `POST /api/v1/one/public/register` with `accepted_terms`, email, password, workspace name/slug. Assert: 200 + cookie; `is_email_verified` is false; Communications/Messaging outbox has **no** “Verify your email address” dispatch; `one.GlobalUsers.EmailVerificationTokenHash` is null. Then `POST /one/auth/login` with the same password succeeds. Then `POST /one/auth/resend-verification` with that email; now the hash is set and (if system Resend is configured) the Ops `/verify-email?email=&token=` mail is the first verify the user ever sees.

### Blast radius
Every new merchant on `POST /one/public/register`. No money path. PII is just the signup email (already stored). Ops impact: support will hear “I never got a verify email” and tell them to hit Resend; many never will. Frequency: every signup. Severity stays **P2** now that 112 closed the 404 — this is “verify never starts,” not “verify URL 404s.” Combined with I02 it was ranked P1 in the audit; I02 is resolved.

### Suggested fix
Smallest correct change: in `RegisterPublicUserCommandHandler` (or a new `INotificationHandler<UserRegisteredDomainEvent>` next to the existing dispatch handlers) call the same `ITokenGeneratorService` + `user.SetEmailVerificationToken(...)` that resend uses, then `SaveChanges` so `EmailVerificationRequestedDomainEvent` rides the existing mail path to Ops `/verify-email`. Do not require verify on login in this ticket (that is a product gate, not this bug). Do not invent a second HMAC. Do not regenerate TypeSpec. Do not touch Stripe Billing `subscription.updated`, LP-059, Wave 5, WhatsApp, Xero, or e-mandate.

### Evaluation notes
Completes **112 / B07-I02** (resolved on `fix/112-reset-verify-404`). Do not re-open 112 unless the Ops page regresses. No overlap with 281–291. Not blocked. Still P2. Source section lives in `plans/009-bugs/07-one-identity-invites-keys.md` (not a file named `07-identity-tenancy-security.md`).

