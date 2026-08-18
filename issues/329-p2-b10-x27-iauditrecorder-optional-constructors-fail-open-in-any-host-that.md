---
number: "329"
id: B10-X27
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 329 — B10-X27 — `IAuditRecorder?` optional constructors fail open in any host that forgets the registration

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X27 — P2 — `IAuditRecorder?` optional constructors fail open in any host that forgets the registration

`AddOneModule` does `services.AddScoped<IAuditRecorder, AuditRecorder>()`. Production invite/remove/refund/cancel should audit. The constructors default `= null`. A test host or a future composition that registers the command handlers without One’s DI **silently stops writing** `one.AuditEvents`. That is fail-open for compliance, fail-closed for nothing.

Accept-invite never took the dependency (B10-X20).

## Evaluation (current tree, 2026-08-18)

### What the bug is
Invite, remove, refund, cancel, record-payment, API-key mint/revoke, and (as of 176) accept-invite all take `IAuditRecorder? auditRecorder = null`. If the handler is constructed without One’s DI, `one.AuditEvents` is simply not written. Production `AddOneModule` does register the recorder. A test host, a future extracted worker, or a `new XxxHandler(repo, ...)` that forgets the third argument fails **open** for compliance and closed for nothing.

### Still present?
**STILL BROKEN**

Optional constructors are unchanged. Examples:

```18:26:apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs
    public InviteUserToWorkspaceCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        IAuditRecorder? auditRecorder = null)
```

```20:28:apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs
    public RecordRefundCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        IAuditRecorder? auditRecorder = null)
```

Same `= null` on `RemoveWorkspaceMemberCommandHandler` (23–25), `CancelAdminSubscriptionCommandHandler`, `RecordSubscriberPaymentCommandHandler`, `GenerateApiCredentialCommand`, `RevokeApiCredentialCommand`.

176 (`fix/176-accept-invite-membership`) added the accept-invite membership guard **and** an optional recorder. It did not make the dependency required:

```16:26:apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs
    private readonly IAuditRecorder? _auditRecorder;

    public AcceptWorkspaceInvitationCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        IAuditRecorder? auditRecorder = null)
```

Write is still `if (_auditRecorder != null) await _auditRecorder.RecordAsync(...)` (56–67). Production registration is still there (`One/Infrastructure/DependencyInjection.cs` 62: `services.AddScoped<IAuditRecorder, AuditRecorder>()`). ASP.NET DI will inject it when the host uses `AddOneModule`. The footgun is any composition that does not.

### Related files
- `apps/lazuar-api/Modules/One/Contracts/IAuditRecorder.cs` and `Modules/One/Infrastructure/Services/AuditRecorder.cs` — the port and the writer.
- `apps/lazuar-api/Modules/One/Infrastructure/DependencyInjection.cs` — the one registration that keeps production honest.
- `apps/lazuar-api/Modules/One/Application/Commands/{InviteUserToWorkspace,RemoveWorkspaceMember,AcceptWorkspaceInvitation,GenerateApiCredential,RevokeApiCredential}Command.cs` — optional ctors.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/{RecordRefund,CancelAdminSubscription,RecordSubscriberPayment}CommandHandler.cs` — same pattern on money/PII writes.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/{InviteUserToWorkspaceCommandHandlerTests,AcceptWorkspaceInvitationCommandHandlerTests,AuditRecorderTests}.cs` — inject a substitute; they do not fail if you omit it.
- Issue 176 (accept-invite membership + optional audit); 115 (same accept-invite family).

### Tests
- Existing tests that touch this path: `InviteUserToWorkspaceCommandHandlerTests.Invite_RecordsAuditWithoutSecrets` (constructs with `Substitute.For<IAuditRecorder>()` and asserts `RecordAsync`); accept-invite tests that now pass a substitute at line 132 of `AcceptWorkspaceInvitationCommandHandlerTests.cs`; `AuditRecorderTests` (persistence when the real recorder is used).
- Whether any test would fail if the bug is still there: **no**. Every happy-path test that cares about audit **supplies** the dependency. A handler built with two arguments still compiles and silently skips `one.AuditEvents`.
- First regression test: construct `InviteUserToWorkspaceCommandHandler(repo, tokens)` with **no** recorder (or register handlers without `AddOneModule`) and assert either (a) the ctor no longer compiles / DI throws, or (b) if optional is kept, a composition test that `GetRequiredService<InviteUserToWorkspaceCommandHandler>()` from a host missing `IAuditRecorder` fails closed. Prefer making the parameter required and deleting `= null`.

### Reproduction today
Arrange: unit-test host that registers `InviteUserToWorkspaceCommandHandler` + fakes for repo/tokens, **not** `IAuditRecorder`. Act: send a valid ADMIN invite. Assert: invitation row is saved; `one.AuditEvents` has no `member.invited`. Repeat for refund/cancel/accept-invite. On the real API process (`AddAllModules` / `AddOneModule`) the same commands **do** write audit — production is fine until someone composes a slimmer host.

### Blast radius
Compliance / forensics, not money. Invite/remove/refund/cancel/accept are the events a merchant or PDPA request will ask for. Frequency: zero on today’s API host; 100% on any future worker/test/extract that news up the handlers. Fail-open is the wrong default for those actions.

### Suggested fix
Smallest correct change: drop `= null` and the `?` on every handler that already calls `_auditRecorder.RecordAsync`. Keep `IAuditRecorder` in `AddOneModule`. Update test ctors to pass the substitute (they mostly already do). Do not invent a second recorder. Do not TypeSpec-regen. Accept-invite already records when the recorder is present (176) — this issue is the optionality, not the missing call.

### Evaluation notes
Still P2 (composition footgun, not today’s prod). 176 closed “accept never took the dependency”; it left the `?`. Not a duplicate of 176’s membership check. 268 (revoke has no audit) is a sibling if revoke is still optional. Do not mark resolved while `= null` remains on invite/remove/refund/cancel.


