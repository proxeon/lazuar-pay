---
number: "301"
id: B09-U33
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 301 — B09-U33 — Portal header shows “Member” for guests

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U33 — Portal header shows “Member” for guests (P2)

`portal/layout.tsx` 15. Magic-link page says you are a Member.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The buyer portal layout always paints a header name: `authData?.name || "Member"`. Guests (no `lazuar_auth` cookie) get `authData` undefined, so the chrome says **Member** while the main column is the magic-link form (“Welcome to your Dashboard”). A token-only buyer who is not a Lazuar user also has no cookie; they still see “Member” next to a dashboard that is HMAC-identified, not a membership. The Logout button is correctly gated on `authData`, so the lie is the label, not a fake session control. `PortalDashboardLink` was added (U11 brand 404), but the name fallback was not changed.

### Still present?
**STILL BROKEN**

```14:15:apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx
  const { data: authData } = await serverClient.GET("/one/auth/me");
  const userName = authData?.name || "Member";
```

```31:33:apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx
            <span className="text-xs font-bold uppercase tracking-widest text-muted-foreground hidden sm:inline">
              {userName}
            </span>
```

Guest body is now always the magic-link form when `?token=` is missing (`portal/page.tsx:54–62`) — U02 cookie-404 may have moved, but the header still wraps that form with “Member.” Community leftover `CommunityPortalView.tsx:58` uses the same `"Member"` fallback and is still unimported.

### Related files
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` — the fallback.
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` — guest magic-link vs token dashboard.
- `apps/lazuar-portal/src/modules/portal/components/RequestMagicLinkForm.tsx` — the form shown under the lying label.
- `apps/lazuar-portal/src/modules/core/lib/server-client.ts` — forwards `lazuar_auth` only.

### Tests
- Existing: no portal layout tests. `apps/lazuar-portal` tests are `modules/checkout/i18n/i18n.test.mjs` and `grossBreakdown.test.mjs`.
- Would any test fail if the bug is still there? No.
- First regression: with no cookie and no name, the header must not render the literal “Member” (empty, “Guest”, or hide the name span).

### Reproduction today
Open `/{tenantSlug}/portal` in a private window (no cookie, no token). Assert: main column is the magic-link form; header name on `sm+` is “MEMBER.” Open the same URL with a valid `?token=` and no cookie. Assert: dashboard loads, header still “MEMBER.”

### Blast radius
Buyer-facing honesty. No money, no PII leak. Every guest and every token-only buyer on desktop (`hidden sm:inline` hides it on phones). Support (“I’m not a member”) noise.

### Suggested fix
If `!authData`, do not render the name span (or show nothing / “Guest”). If `authData.name` exists, show it. Do not invent a membership. Do not implement cookie portal sessions here (that is U02).

### Evaluation notes
Still P2. U02/U11/U34 are neighbors on the same layout file. U11’s 404 brand link looks improved via `PortalDashboardLink`; this label is not. Not blocked.
