---
number: "322"
id: B09-U54
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 322 — B09-U54 — Admin “wrong console” is silent

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U54 — Admin “wrong console” is silent (P2)

Product cookie on admin → login, no explanation.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Hub Admin (`lazuar-admin`, port 3005) is the platform super-admin console. A merchant who is logged into Hub Ops (`lazuar_auth`) and then hits Admin is not a system admin. `SuperadminLayout` calls `GET /platform/auth/me`, which only accepts `lazuar_admin_auth` and only returns users with `IsSystemAdmin`. Failure navigates to `/login?returnUrl=…` with **no query flag and no copy** that this is the wrong console. The login page title is “Platform Admin” / “global control plane,” but it never says “you are signed into Hub Ops; this site is staff-only.” The merchant sees a blank credential form and assumes their product password should work (it will 401 unless they are a system admin).

### Still present?
**STILL BROKEN**

Auth failure is still a silent bounce:

```37:48:apps/lazuar-admin/src/App.tsx
  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/platform/auth/me");
        if (error || !data) {
          navigate(`/login?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
          return;
        }

        setUser(data);
      } catch {
        navigate("/login");
      }
```

`LoginPage` has no `reason`, no “wrong console” banner, and does not distinguish 401-from-me vs a cold visit:

```50:53:apps/lazuar-admin/src/components/LoginPage.tsx
          <div className="text-center mb-8">
            <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Platform Admin</h1>
            <p className="text-[13px] text-[#71717a] mt-1.5">Sign in to the global control plane.</p>
          </div>
```

Cookies are dual-realm. Platform routes read `lazuar_admin_auth` only (`AuthAndCorsExtensions.cs:54–56`). Product `lazuar_auth` is ignored on `/api/v1/platform/*`. `/platform/auth/me` 401s unless the JWT names a live system admin (`PlatformAuthEndpoints.cs:73–90`); a non-admin id is looked up with `GetSystemAdminByIdAsync` which filters `IsSystemAdmin` (`PlatformAdminAuthQuery.cs:49–65`) and returns null → 401 + delete admin cookie.

Later fixes on this page: `isSafeReturnUrl` (`LoginPage.tsx:6–8,30–31`) closed the open redirect (issue 136). `returnUrl` now includes search (`App.tsx:42`). Footer still hard-codes “Super Admin” (`Sidebar.tsx:204`) instead of `user.email` — adjacent honesty, not the silent bounce.

### Related files
- `apps/lazuar-admin/src/App.tsx` — `SuperadminLayout` verifySession bounce.
- `apps/lazuar-admin/src/components/LoginPage.tsx` — no reason copy; safe returnUrl (136).
- `apps/lazuar-admin/src/components/Sidebar.tsx` — “Super Admin” subtitle.
- `apps/lazuar-admin/src/lib/api-client.ts` — `credentials: "include"`; still only the admin cookie is consumed on platform routes.
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` — cookie name by path prefix.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs` — `/auth/me` system-admin only.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/PlatformAdminAuthQuery.cs` — non-admin email/id → null.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/PlatformAdminAuthQueryTests.cs` — `GetSystemAdminByEmail_Returns_Null_For_Non_System_Admin`, `GetSystemAdminById_Returns_Null_When_Not_System_Admin`.

### Tests
- API: `PlatformAdminAuthQueryTests` proves a product user is not a platform `/me`. That does **not** assert the admin SPA shows an explanation.
- Admin has no frontend tests (325). `isSafeReturnUrl` is untested in admin (136’s bug cannot regress-fail in this app).
- First regression test: visit `/platform/gateways` with only `lazuar_auth` set → land on `/login?…` and assert visible copy that this is not Hub Ops (e.g. “Staff console — merchant accounts sign in at Hub”). Optional: `?reason=wrong-console` from the 401 branch.

### Reproduction today
Arrange: merchant session on `:3003` (cookie `lazuar_auth`). Act: open admin `:3005/` or `:3005/platform/gateways` in the same browser. Assert: Network `GET /api/v1/platform/auth/me` is 401 (no `lazuar_admin_auth`); SPA navigates to `/login?returnUrl=%2Fplatform%2Fgateways`; the form does not mention Hub Ops, cookies, or “wrong console.” Act: submit that user’s product email/password → 401 “Invalid credentials or unauthorized access.” Superadmin with `lazuar_admin_auth` still gets in.

### Blast radius
Support / ops confusion: merchants bookmark the wrong host (3005 vs 3003) and think they are locked out. No money, no extra PII leak (product cookie is not sent as the platform credential). Frequency: low (admin is staff-only) but every mistaken visit is silent. Still P2. Open redirect is already fixed (136).

### Suggested fix
On 401 from `/platform/auth/me`, navigate to `/login?returnUrl=…&reason=wrong-console` (or always show a static line on `LoginPage`: “This is Lazuar staff admin. Merchants use Hub Ops on port 3003 / hub.lazuar.com.”). Do not accept `lazuar_auth` on platform routes. Do not paint a product login on admin. No TypeSpec.

### Evaluation notes
Still P2. 136 (open redirect) and returnUrl-search (U24 / admin `location.search`) are already fixed on this login page — do not re-open them. Duplicate-adjacent: Sidebar “Super Admin” string. 325: no admin test would catch a silent bounce.

