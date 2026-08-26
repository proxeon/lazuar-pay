---
number: "277"
id: B07-I37
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 277 — B07-I37 — Invite token in the query string

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I37 — P2 — Invite token in the query string

**Where.** Mail URL; `AcceptInvitePage` reads `searchParams`.

**What.** Server access logs, browser history, Referer if the success page ever loads a third party. Accept is first-party today. Prefer POST-only after a fragment, or a one-time exchange. P2.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The invite secret is still a URL-safe token in the query string: `{OpsUrl}/accept-invite?token={plain}`. `AcceptInvitePage` reads `searchParams.get("token")`, trims it, and POSTs it to `/one/workspaces/invites/accept`. That puts the bearer capability in access logs (Caddy, CDN, reverse proxies), browser history, Referer if any third-party script/image is ever added to the success state, and the login `returnUrl` (`/login?returnUrl=/accept-invite?token=…`). Accept is first-party today (ops origin, no third-party pixels on that page). The same pattern is used for reset and verify (`?email=&token=`), now pointed at ops. `OneLinkServiceTests` **locks** the query-string invite URL, so a fragment/`#token=` change would fail those tests on purpose.

### Still present?
**STILL BROKEN**

```65:67:apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs
    public async Task Handle(WorkspaceInvitationCreatedDomainEvent notification, CancellationToken ct)
    {
        var acceptLink = $"{_linkService.GetOpsBaseUrl()}/accept-invite?token={notification.PlainToken}";
```

```70:72:apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = (searchParams.get("token") ?? "").trim();
```

Login bounce rebuilds the query (`AcceptInvitePage.tsx:19–20`). Portal leftover still forwards `?token=` (`apps/lazuar-portal/src/app/accept-invite/page.tsx:14`). Tests: `OneLinkServiceTests.GetOpsBaseUrl_UsesOpsUrl_AndInviteUrlDoesNotContainClientUrl` and `InviteEmail_UsesOpsAcceptUrl_NotClientUrl` assert `http://localhost:3003/accept-invite?token=invite-token`. Reset/verify also query-string (`NotificationDispatchDomainEventHandlers.cs:31, 50`; `ResetPasswordPage.tsx` / `VerifyEmailPage.tsx`).

### Related files
- `apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` — mint the URL.
- `apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx` — read + POST + login returnUrl.
- `apps/lazuar-ops/src/components/LoginPage.tsx` — preserves `returnUrl` including the token.
- `apps/lazuar-portal/src/app/accept-invite/page.tsx` — 302 keeps `token`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OneLinkServiceTests.cs` — currently pins `?token=`.
- `apps/lazuar-ops/src/pages/ResetPasswordPage.tsx` / `VerifyEmailPage.tsx` — same class of leak (112’s pages now exist).
- `apps/lazuar-api/Modules/One/Domain/WorkspaceInvitation.cs` — hash-at-rest is fine; the leak is the mail URL.

### Tests
- Existing: `OneLinkServiceTests` invite URL assertions; accept handler tests hash the presented token (not the transport).
- Those URL tests would **fail** if you moved the secret to a fragment (good). Nothing fails because the token is in the query today.
- First regression: built HTML must not contain `?token=`; Accept page still accepts the token from `location.hash` (or a one-time exchange) and POSTs it; old `?token=` links can be accepted for one release.

### Reproduction today
Arrange: invite mail (or read the outbox HTML). Act: open the link. Assert: address bar is `/accept-invite?token=…`; `/one/auth/me` 401 redirects to `/login?returnUrl=%2Faccept-invite%3Ftoken%3D…`. Inspect access logs / history. Success navigates to `/commerce/dashboard` (token drops from the URL only after redirect).

### Blast radius
The invite is a 7-day capability bound to the invited email (accept still checks `user.Email != invitation.Email`). A leaked URL without that inbox is a 400, not a membership steal. Risk is logs/history/Referer to a third party if the page ever grows analytics. Same for reset tokens (worse: they reset the password). Frequency: every invite.

### Suggested fix
Prefer `{OpsUrl}/accept-invite#token=…` (fragment is not sent to the server) or a one-time `POST /one/workspaces/invites/exchange` that swaps the mail secret for a short cookie/session. Update `AcceptInvitePage` to read `hash` first, then `search` for back-compat. Stop putting the token in `returnUrl` (stash in `sessionStorage` after first paint). Update `OneLinkServiceTests` to assert **no** `?token=`. Do not TypeSpec-regen unless you add the exchange route. Do not weaken email-bind or hash-at-rest.

### Evaluation notes
Still P2. Not a duplicate of 018 (delivery) or 268 (resend). Reset/verify query tokens are the same class; fix invite first, copy the pattern. Residual after 161–200. 112 added ops pages but kept query strings.

