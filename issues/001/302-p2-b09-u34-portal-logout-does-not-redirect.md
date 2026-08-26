---
number: "302"
id: B09-U34
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 302 — B09-U34 — Portal logout does not redirect

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U34 — Portal logout does not redirect (P2)

`portal/layout.tsx` 35–45. Cookie dies; chrome stays.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Portal header Logout is a server-action form that `POST`s `/one/auth/logout` and then does nothing: no `redirect`, no `revalidatePath`, no `cookies()` refresh. The product cookie is cleared on the API, but the layout already rendered with `authData` and stays on screen — name + Logout remain until a manual refresh. A merchant previewing `/{slug}/portal` with a Hub cookie (or a buyer who somehow has `lazuar_auth`) clicks Logout and thinks they are still signed in. Token-only buyers do not see Logout (`authData` falsy), so they are unaffected.

### Still present?
**STILL BROKEN**

```34:48:apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx
            {authData && (
              <>
                <div className="h-4 w-px bg-border hidden sm:block"></div>
                <form action={async () => {
                  "use server";
                  await serverClient.POST("/one/auth/logout");
                }}>
                  <button 
                    type="submit"
                    className="text-xs font-bold uppercase tracking-widest text-foreground hover:text-red-600 transition-colors flex items-center gap-1.5"
                  >
                    Logout
                  </button>
                </form>
              </>
            )}
```

Sibling cancel/keep actions on `portal/page.tsx` now `redirect` on error and `revalidatePath` on success (13–38). Logout did not get the same treatment.

### Related files
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` — the action.
- `apps/lazuar-portal/src/modules/core/lib/server-client.ts` — cookie-forwarding client used for logout.
- `apps/lazuar-api` One `POST /one/auth/logout` — actually clears `lazuar_auth` (assume 116 if stamp mismatch).

### Tests
- Existing: none for portal logout.
- Would any test fail if the bug is still there? No.
- First regression: after the action, the response must navigate (e.g. `redirect(\`/${tenantSlug}/portal\`)` or the magic-link page) so the next RSC render has no name and no Logout.

### Reproduction today
Sign in to Hub Ops (cookie on the shared host). Open `/{tenantSlug}/portal`. Assert: header shows your name and Logout. Click Logout. Assert: URL and chrome are unchanged; name and Logout still visible. Refresh. Assert: name becomes “Member” (U33) and Logout is gone; cookie is dead.

### Blast radius
Session-chrome lie. The cookie is actually gone, so a later navigation is logged out. Risk is a human who walks away thinking Logout no-op’d, or retries. Low frequency (cookie-on-portal is already a weird path after U02). Not money.

### Suggested fix
After a successful logout POST, `redirect(\`/${tenantSlug}/portal\`)` (or `/` if you want off the tenant). Optionally `revalidatePath` first. Ignore POST errors the same way as today or surface them; do not leave the authed chrome up. Do not implement a full cookie portal session.

### Evaluation notes
Still P2. Same file as U33. Production logout stamp mismatch (116 / I06) can make the cookie linger — if that is still open, redirect-after-POST is still the right UI even when the API is sticky. Not blocked by U33.
