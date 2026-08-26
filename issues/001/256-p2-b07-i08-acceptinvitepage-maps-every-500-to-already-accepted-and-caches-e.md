---
number: "256"
id: B07-I08
severity: P2
status: resolved
resolved_branch: fix/256-accept-invite-4xx-cache
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 256 — B07-I08 — AcceptInvitePage maps every 500 to “already accepted” and caches errors

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I08 — P2 — AcceptInvitePage maps every 500 to “already accepted” and caches errors

**Where.** `AcceptInvitePage.tsx:17, 40–46, 64, 175–177`.

**What.** Honest for the unique-index 500. Dishonest for everything else. Module-level `Map` is the right Strict-Mode fix for **in-flight** accepts. Leaving a rejected Promise in the Map means a later visit with the same token in the same JS heap does not retry. Wrong-email Sign out deletes (`:120`). The generic “Sign in” link does not.

Out of scope for 09’s *pixels*; this is control-flow that lies about One’s API.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`AcceptInvitePage` POSTs `/one/workspaces/invites/accept` once per token via a module-level `acceptByToken` `Map` so React Strict Mode does not double-accept. The audit’s P2 was not the Map itself: every HTTP 500 was narrated as “this invite may already have been accepted,” and a failed Promise (or a resolved error outcome) stayed in the Map. A later visit with the same token in the same JS heap therefore replayed the lie instead of retrying. Wrong-email Sign out deleted the cache entry; the generic Sign in link did not. Combined with a unique-index 500 on accept (B07-I03 / issue 113) this taught bookkeepers that a down database or a membership collision was a used invite.

### Still present?
**PARTIAL**

Issue **159** (`B09-U30`, `fix/159-accept-invite-5xx`) already rewrote the 5xx branch. Current code no longer says “already accepted” and **evicts** the token before returning a retryable error:

```40:47:apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx
    if (response.status >= 500) {
      acceptByToken.delete(token);
      return {
        kind: "error",
        message: "Something went wrong accepting this invite. Try again.",
        wrongEmail: false,
      };
    }
```

The cache still stores every **non-5xx** outcome (`acceptByToken.set` at `:65`) including 4xx `{ kind: "error" }`. The generic Sign in link still does **not** `acceptByToken.delete`:

```175:179:apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx
              {!view.wrongEmail && (
                <Link to="/login" className="inline-block text-[12px] font-semibold text-[#09090b] hover:underline">
                  Sign in
                </Link>
              )}
```

Wrong-email Sign out still deletes (`:120–124`). Unauth and thrown network errors also delete (`:84`, `:104`). After **176** / `Accept_AlreadyMember_Is400AndDoesNotInsert`, already-member is a 400 `InvalidOperationException`, so the unique-index 500 bandage is mostly gone — but a 400 “invalid or expired” (revoked, raced, or expired) is still cached for the life of the tab.

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx` — SPA control-flow, Map, 5xx vs 4xx, Sign in vs Sign out.
- `apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs` — 400s for invalid/expired, wrong email, already-member.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs:134–138` — `POST /workspaces/invites/accept`.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` — 500s are now generic (`An unexpected error occurred.`) after **122**; SPA must not invent a reason.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/AcceptWorkspaceInvitationCommandHandlerTests.cs` — handler cases; no SPA coverage.
- `issues/159-p1-b09-u30-accept-invite-maps-every-5xx-to-already-accepted.md` — P1 twin that already landed the 5xx copy change.
- `issues/113-p1-b07-i03-double-accept-already-member-second-pending-token-500.md` / `issues/176-p1-b10-x20-accept-invite-does-not-check-existing-membership-and-does-not-au.md` — why 500-as-already-accepted existed.

### Tests
- Existing: `Accept_PendingMatchingEmail_CreatesMembership`, `Accept_ExpiredInvite_Throws`, `Accept_WrongEmail_Throws`, `Accept_AlreadyMember_Is400AndDoesNotInsert`, `Accept_RecordsAuditWithoutToken` in `AcceptWorkspaceInvitationCommandHandlerTests`.
- None of those would fail if the SPA still mapped 5xx to “already accepted” or cached 4xx. Ops has no page tests (issue **325**).
- First regression: a unit/RTL test that (1) a 503 body is shown as “Try again” and a second mount with the same token issues another POST; (2) clicking Sign in after a 400 evicts the Map so a later accept is attempted.

### Reproduction today
Arrange: signed-in user, valid pending invite token, temporarily make `POST /one/workspaces/invites/accept` return 503. Act: open `/accept-invite?token=…`. Assert: copy is “Something went wrong… Try again,” not “already accepted”; refresh in the same tab retries (5xx evicted). Then arrange a 400 “Invitation is invalid or expired.”, click Sign in, navigate back to the same URL without a full document reload. Assert: the cached 400 is shown and no second POST fires — residual bug.

### Blast radius
Invitees and the admin who sent the mail. No money movement. A transient 5xx no longer permanently poisons the token in-tab (159). A 4xx still does, so a revoked-then-reissued token with the same string is unlikely, but a user who hit “invalid or expired” during a deploy and then signed in again in the same SPA heap will keep seeing the stale error until they hard-reload. Frequency: every failed accept that is not 5xx/unauth.

### Suggested fix
Keep the Map for **in-flight** accepts only. On any terminal 4xx, either do not `set` the Promise until success, or `delete` in the error view’s Sign in `onClick` the same way Sign out does. Do not reintroduce “already accepted” for 5xx — already-member is a 400. No TypeSpec regen.

### Evaluation notes
Duplicates **159** (P1, resolved) for the lying 5xx sentence. Residual is cache + Sign in. Severity can stay P2 for the leftover Map; do not re-open 159. **113/176** already fail-closed already-member. **122** means a raw 500 detail is no longer a Postgres unique-violation string.

## Resolution

4xx outcomes evict the token Map. Sign in deletes the same way Sign out does. 5xx stay “Try again.” In-flight accepts still share one Promise.

