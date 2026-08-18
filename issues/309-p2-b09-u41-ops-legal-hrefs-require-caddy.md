---
number: "309"
id: B09-U41
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 309 — B09-U41 — Ops legal hrefs require Caddy

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U41 — Ops legal hrefs require Caddy (P2)

`LoginPage.tsx` 9–10.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops signup requires accepting Terms and Privacy. The anchors are root-absolute `/portal/legal/terms` and `/portal/legal/privacy`. That path only exists when a gateway (Caddy) serves ops at `/` and portal (Next `basePath=/portal`) at `/portal*`. Vite ops on `:3003` has no `/portal/...` route; after 156 the catch-all is a 404 page, so the new-merchant legal click dies. Portal’s own footer uses Next `<Link href="/legal/terms">`, which is correct *inside* the portal app (and becomes `/portal/legal/terms` when `NEXT_BASE_PATH=/portal`). Ops cannot use that helper; it hard-codes the Caddy-shaped prefix.

### Still present?
**STILL BROKEN**

```9:10:apps/lazuar-ops/src/components/LoginPage.tsx
const LEGAL_TERMS_HREF = "/portal/legal/terms";
const LEGAL_PRIVACY_HREF = "/portal/legal/privacy";
```

```302:310:apps/lazuar-ops/src/components/LoginPage.tsx
                  <span className="text-[12px] text-[#71717a] leading-relaxed">
                    I agree to the{" "}
                    <a href={LEGAL_TERMS_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Terms of Service
                    </a>{" "}
                    and{" "}
                    <a href={LEGAL_PRIVACY_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Privacy Policy
                    </a>
```

Ops Vite is `/` on 3003 (`apps/lazuar-ops/vite.config.ts` 8–9, 18–21). Unknown paths 404:

```254:311:apps/lazuar-ops/src/App.tsx
function NotFoundPage() {
  return (
    ...
      <p className="text-[13px] text-[#52525b]">That page is not here.</p>
...
      <Route path="*" element={<NotFoundPage />} />
```

Caddy is what makes `/portal*` work:

```26:29:deploy/dev/Caddyfile
	# Portal (Next basePath=/portal)
	handle /portal* {
		reverse_proxy host.docker.internal:3004
	}
```

(`deploy/prod/Caddyfile` 17–20 is the same map.) Portal pages exist at `apps/lazuar-portal/src/app/legal/terms/page.tsx` and `privacy/page.tsx`. `next.config.ts` `basePath` is `process.env.NEXT_BASE_PATH || ""` — local `next dev` on :3004 without the prefix serves `/legal/terms`, not `/portal/legal/terms`.

### Related files
- `apps/lazuar-ops/src/components/LoginPage.tsx` — the two hrefs and the required checkbox.
- `apps/lazuar-ops/src/App.tsx` — no legal routes; `*` is 404 (156).
- `apps/lazuar-ops/vite.config.ts` — ops at `/`.
- `apps/lazuar-portal/src/app/legal/terms/page.tsx` / `privacy/page.tsx` — real documents.
- `apps/lazuar-portal/src/app/layout.tsx` — portal footer `/legal/*`.
- `apps/lazuar-portal/next.config.ts` — optional `/portal` prefix.
- `deploy/dev/Caddyfile`, `deploy/prod/Caddyfile` — `/portal*` → portal.
- Issue 149 (`fix/149-legal-copy`) — rewrote legal *copy*, not the ops hrefs.

### Tests
- Existing tests that touch this path: none. No ops LoginPage test. No Caddy/href contract test.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: `LEGAL_*_HREF` is either `VITE_PORTAL_URL + "/legal/terms"` (dev :3004) or the baked portal origin + `/legal/terms` (prod already has `VITE_PORTAL_URL=https://hub.lazuar.com/portal`). A unit test that the constants are not host-relative `/portal/...` unless a documented gateway is assumed.

### Reproduction today
`task dev` / `vite --port=3003` without the 9080 proxy. Open `http://localhost:3003/signup`, click Terms of Service. Assert: ops 404 (“That page is not here.”). Repeat through `http://localhost:9080/signup` (Caddy): `/portal/legal/terms` should load portal. Repeat on a portal-only :3004 without `NEXT_BASE_PATH`: `/portal/legal/terms` 404s there too; `/legal/terms` works.

### Blast radius
Every new merchant who reads legal before creating a workspace on a direct ops port (local, some staging). Production behind Caddy/`hub.lazuar.com` works. Not money, not PII; it is a required-checkbox link that 404s. Frequency: every local signup, plus any deploy that serves ops without the portal prefix.

### Suggested fix
Point the two `<a>` tags at an absolute portal origin: `(import.meta.env.VITE_PORTAL_URL || "http://localhost:3004").replace(/\/$/, "") + "/legal/terms"`. That matches how the dashboard already builds pay links (`DashboardPage.tsx` 95–96). Do not mount duplicate legal pages inside ops. Do not depend on Caddy path shape. No TypeSpec, no WhatsApp, no legal rewrite (149 already did copy).

### Evaluation notes
Audit cited only lines 9–10; still accurate. 156 made the miss a 404 instead of a silent dashboard redirect — slightly more honest, still broken. 149 is related copy, not this href. Severity still P2. Not blocked.

