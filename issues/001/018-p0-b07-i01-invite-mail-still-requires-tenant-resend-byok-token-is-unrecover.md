---
number: "018"
id: B07-I01
severity: P0
status: resolved
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
resolved_branch: fix/018-invite-mail-platform-resend
---

# 018 — B07-I01 — Invite mail still requires tenant Resend BYOK; token is unrecoverable

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/018-invite-mail-platform-resend`

Workspace invite mail now dispatches as the system tenant, same as password reset, so platform Resend can send it before BYOK exists.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I01 — P0 — Invite mail still requires tenant Resend BYOK; token is unrecoverable

**Where.** `NotificationDispatchDomainEventHandlers.cs:78–79` publishes `DispatchMessageIntegrationEvent` with `notification.OrganizationId`. `ResendEmailService.cs:47–68` refuses platform fallback for non-system tenants.

**What.** `297ba98` fixed the URL host. It did not fix delivery. A new Hub tenant inviting a bookkeeper has no Email Provider. The invite row commits. The outbox retry throws `"No platform fallback allowed for tenant emails…"`. Team toasts “Invitation sent” (`TeamPage.tsx:38`). GET invites (unused by the page) would show PENDING without a token. There is no resend. The only secret is gone.

**Why P0.** After the accept page shipped, this is the remaining break in the staff-onboarding loop 008 called the largest product hole. The page cannot run without the mail. Password reset uses `Guid.Empty` and *can* use platform Resend; invite deliberately does not.

**Not a test gap only.** No test asserts delivery, BYOK, or system-tenant dispatch for invites. `OneLinkServiceTests` only asserts the URL string inside the HTML payload of a **substituted** `IEventBus`.

