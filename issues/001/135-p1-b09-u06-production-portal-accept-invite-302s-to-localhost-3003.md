---
number: "135"
id: B09-U06
severity: P1
status: resolved
resolved_branch: fix/135-portal-accept-invite-ops-url
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 135 — B09-U06 — Production portal `/accept-invite` 302s to `localhost:3003`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/135-portal-accept-invite-ops-url`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U06 — Production portal `/accept-invite` 302s to `localhost:3003` (P1)

**Where:** `lazuar-portal/src/app/accept-invite/page.tsx` 11–16; `docker-bake.hcl` 76–87; `lazuar-portal/Dockerfile` 21–24; `docker-compose.yml` 66–83.  
**What:** `NEXT_PUBLIC_OPS_URL` is not baked. Default is `http://localhost:3003`.  
**Walk:** Old email or bookmark `https://hub.lazuar.com/portal/accept-invite?token=…` (ClientUrl era). 302 to a host that is not the Hub. New mail uses OpsUrl (297ba98) and is fine. The compatibility page is the landmine.

