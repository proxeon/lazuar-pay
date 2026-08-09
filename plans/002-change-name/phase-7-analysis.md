# Phase 7 — Analysis: optional follow-ups (minimal safe)

**Status:** Analysis only — **do not implement in this phase** (analyzer).  
**Date:** 2026-08-09  
**Branch / base:** `main` (rename shipped via PR [#12](https://github.com/proxeon/lazuar-pay/pull/12), merge `ea2d5d9`)  
**Related:** [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 7, [`phase-6-done.md`](./phase-6-done.md)

---

## 1. Goal

Recommend a **minimal, safe Phase 7** after the rename is live on production:

1. Close remaining **DX / archaeology** gaps that confuse humans or agents navigating old `*-page` paths.
2. **Skip** anything that touches GHCR package names, prod compose, Caddy, or deploy.
3. Prefer **one small PR** (or even a docs-only micro-PR) over bulk history rewrites.

Phase 7 is **optional**. The rename project’s definition of done is already met (Phases 0–6). Nothing here is a production risk if left undone.

---

## 2. Current state (post Phase 6)

| Surface | Status after merge `ea2d5d9` |
|---------|------------------------------|
| App dirs | `apps/lazuar-{developers,ops,portal,admin}` — no `*-page` frontends |
| Living docs | Root `README.md`, `apps/lazuar-docs/**`, `docs/contracts/**`, `plans/001-backend/**` — **clean** of old filter/folder tokens (Phase 4) |
| Docker / bake / compose / `ghcr.yml` | New dockerfile paths; **GHCR still `lazuar-hub-*`** |
| Local compose developers service | **Already present** (Phase 2) — `lazuar-developers` on `3002`, profile `full` |
| `docker-compose.ghcr.yml` developers | **Already present** (Phase 2) |
| Prod / Caddy / remote-deploy | Untouched; hub healthy on `sha-ea2d5d9` |
| Historical `docs/001-gaps/**` | Still say `*-page` (and often `lazuar-hub` absolute paths) — intentional Phase 4 skip |
| Active-SOP ADRs (`013`, `017`, partly `007`) | Still say `apps/ops-page` / `apps/portal-page` / `developers-page` |
| `tunnel:fe` | Still describes **community-page :3020** (pre-existing Aura leftover) |
| GHCR `lazuar-hub-superadmin` vs app `lazuar-admin` | **By design** — keep |

---

## 3. Recommendation summary (do / skip)

| Item | Checklist § | Recommend now? | Why |
|------|-------------|----------------|-----|
| **Docs banners** (gaps index + 2–3 SOP ADRs) | 7.1 | **YES — small** | Highest DX value; zero runtime risk; no bulk rewrite |
| Bulk rewrite every gap report | 7.1 | **NO** | Snapshots; paths already stale (`lazuar-hub`); noise |
| Rename `04-developers-page-dx.md` | 7.1 | **NO** | Breaks internal links; historical filename is fine |
| Path-refresh every path in ADR bodies | 7.1 | **NO** (banner only) | History + decision narrative; banner is enough for SOPs |
| Local compose developers parity | 7.2 | **SKIP — done** | Phase 2 already added both compose files |
| GHCR image rebrand | 7.3 | **SKIP forever (this plan)** | Explicit non-goal; dual-tag + prod cutover is a separate project |
| Fix `tunnel:fe` | 7.4 | **YES — optional micro** | One Taskfile task; prevents mprocs surprise |
| README multi-domain → hub paths | 7.4 | **Optional polish only** | Cosmetic in tree comments; not rename-blocking |
| Document naming debt that stays | 7.5 | **YES — in same PR** | Short table in README or this plan; stops “should we rename?” churn |

**Minimal Phase 7 PR scope (recommended):**

1. Banners on 3–4 doc files (exact text below).  
2. Optional: fix `tunnel:fe` in `Taskfile.yml`.  
3. Optional: 4–6 line naming-debt blurb (README glossary or `plans/002-change-name/README.md`).  
4. **Do not** touch Docker, GHCR, deploy, package names, or gap body content.

---

## 4. 7.1 Documentation archaeology

### 4.1 Policy (locked)

| Doc class | Treatment |
|-----------|-----------|
| Living onboarding / commands | Already fixed (Phase 4) — **do not re-open** |
| `docs/001-gaps/**` report bodies | **Leave as historical evidence** |
| `docs/001-gaps/README.md` | **Add one rename map banner** (index is the navigator) |
| ADR pure history (`014`, `016`, `018`, `022`, `023`, …) | **Leave** (or no banner) |
| ADR active SOP with open-file paths (`013`, `017`) | **Banner only** — do not rewrite steps wholesale |
| ADR 007 (Developer Hub SOP steps) | **Banner only** (still used when adding Scalar products) |
| Gap filename `04-developers-page-dx.md` | **Keep filename** |

### 4.2 Exact file edits if doing docs banners

Apply these **only** if implementing the minimal Phase 7 PR. Insert **after the title / status header**, before the first substantive section.

#### A. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/README.md`

Insert after the intro block (after line ~7 “reports listed below”), before `## How to use`:

```markdown
> **Frontend path rename (plan 002, 2026-08):** monorepo apps moved
> `developers-page` → `lazuar-developers`, `ops-page` → `lazuar-ops`,
> `portal-page` → `lazuar-portal`, `superadmin-page` → `lazuar-admin`.
> Reports below still use historical `*-page` paths (and often `lazuar-hub`
> absolute roots) as evidence snapshots — map mentally when opening code.
> Living commands and app folders use the new names; see root `README.md`.
```

**Do not** rename `04-developers-page-dx.md` or update table links.

#### B. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/013-frontend-module-implementation.md`

Insert after the status/context blurb (after line ~7), before `## 1. Directory Structure Rule`:

```markdown
> **Path note (plan 002, 2026-08):** `apps/ops-page` → **`apps/lazuar-ops`**.
> Package / filter: `lazuar-ops`. GHCR image remains `lazuar-hub-ops`.
> Read every `apps/ops-page/...` path in this SOP as `apps/lazuar-ops/...`.
```

Optional (title only, if desired): keep title historical  
`# ADR 013: Frontend Module Implementation (ops-page)`  
— or soft-retitle to  
`# ADR 013: Frontend Module Implementation (lazuar-ops, formerly ops-page)`  
**Prefer keep title + banner** (less history rewrite).

#### C. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/017-portal-frontend-architecture.md`

Insert after Status/Context lines (~5–6), before `## Context & Problem Statement`:

```markdown
> **Path note (plan 002, 2026-08):** `apps/portal-page` → **`apps/lazuar-portal`**.
> Package / filter: `lazuar-portal`. GHCR image remains `lazuar-hub-portal`.
> Public path still `/portal`. Read every `apps/portal-page/...` as `apps/lazuar-portal/...`.
```

#### D. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/007-product-scoped-api-references.md`

Insert after Date line (~5), before `## Context`:

```markdown
> **Path note (plan 002, 2026-08):** `apps/developers-page` → **`apps/lazuar-developers`**
> (not `lazuar-spec` — avoids clash with `packages/api-spec`).
> Production mount is **`hub.lazuar.com/docs`**, not `developers.lazuar.com`.
> TypeSpec SSoT remains `packages/api-spec`. Read SOP paths under the new app folder.
```

### 4.3 Explicitly do **not** edit (7.1)

| Path / action | Reason |
|---------------|--------|
| Bulk find-replace inside `docs/001-gaps/0*.md` | Historical audit; already dual-stale (`lazuar-hub` + `*-page`) |
| `git mv` gap filename `04-developers-page-dx.md` | Churn + link breaks in chat/PRs |
| Rewrite ADR 014 / 016 / 018 / 022 / 023 bodies | Pure decision history |
| Touch `plans/002-change-name/01`–`10` inventories | Pre-rename investigation archive |
| Re-edit living docs already cleaned in Phase 4 | No residual functional hits |

### 4.4 Optional “extra light” (not required)

If someone frequently opens the densest gap report, a **one-line** banner on  
`docs/001-gaps/04-developers-page-dx.md` only — same rename map as gaps README.  
Still **no body path rewrite**.

---

## 5. 7.2 Local compose parity — **DONE; skip**

Phase 2 already landed:

| File | Service | Port | Image |
|------|---------|------|-------|
| `docker-compose.yml` | `lazuar-developers` | `3002:3000` | `lazuar-hub-developers:local` (build), profile `full` |
| `docker-compose.ghcr.yml` | `lazuar-developers` | `3002:3000` | `lazuar-hub-developers:${TAG:-latest}` |

Header comment on local compose already documents:

```text
docker compose up -d --build                 # db + api
docker compose --profile full up -d --build  # + all frontends
```

**Phase 7 action:** mark 7.2 complete; **no code change**.

---

## 6. 7.3 GHCR image rebrand — **DO NOT implement now**

### 6.1 Explicit non-goal

| Layer | Today | Phase 7 |
|-------|--------|---------|
| GHCR packages | `lazuar-hub-{api,ops,portal,superadmin,developers}` | **Keep forever for this plan** |
| Prod compose images | same names | **Do not change** |
| Bake `tags` / matrix `name:` | same names | **Do not change** |
| App folder `lazuar-admin` vs image `lazuar-hub-superadmin` | intentional mismatch | **Document only (7.5)** |

### 6.2 Why skip (even “later”)

- Rename already shipped with **path-only** coupling; prod pull surface stable.
- Rebrand requires dual-tag window + atomic prod compose edit + bake/ghcr matrix `name:` + docs — separate high-risk playbook.
- Checklist §7.3 remains a **future product-branding project**, not incomplete rename debt.

### 6.3 If product ever demands rebrand (out of Phase 7)

Not recommendations to start now — archive only:

1. Dual-tag push old+new for N releases.  
2. Update `deploy/prod/docker-compose.yml` images in one deploy.  
3. Update bake tags + `ghcr.yml` matrix `name:`.  
4. Retire old tags after cutover.  

**Do not open that PR as Phase 7.**

---

## 7. 7.4 DX nits

### 7.4.1 `tunnel:fe` (recommended optional micro-fix)

**Today** (`Taskfile.yml`):

```yaml
tunnel:fe:
  desc: Start ngrok tunnel for Next.js community-page on port 3020
  cmds:
    - ngrok http 3020
```

- Community app **removed** (ADR 022).  
- `mprocs-dev.yaml` still exposes `ngrok-fe-tunnel` → `task tunnel:fe` (autostart false).  
- Port **3020** is not used by any current monorepo FE (ports 3002–3005).

**Minimal safe options (pick one):**

| Option | Change | Prefer? |
|--------|--------|---------|
| **A. Retarget to portal** | `ngrok http 3004`, desc: portal (`lazuar-portal`) for public FE callbacks | **Recommended** if FE tunnel still useful |
| **B. Retarget to API only** | Delete/alias `tunnel:fe` → document “use `tunnel:api`” | If FE tunnel unused |
| **C. Stub with message** | `echo "community-page removed; use tunnel:api or ngrok http 3004 (portal)"` + exit 1 | Safest if unsure of usage |

**Exact edit if Option A** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml`):

```yaml
  tunnel:fe:
    desc: Start ngrok tunnel for lazuar-portal (Next.js) on port 3004
    cmds:
      - ngrok http 3004
```

No mprocs change required (still calls `task tunnel:fe`).

**Do not** invent a new community-page. **Do not** change `tunnel:api` (8080) — that one is real for Billplz/Stripe.

### 7.4.2 README domain story (optional polish — low priority)

Root README tree still mixes legacy hostnames with hub reality:

```text
lazuar-ops/     -> ops.lazuar.com      # marketing / historical
lazuar-portal/  -> portal.lazuar.com
lazuar-admin/   -> admin.lazuar.com
lazuar-developers/ -> hub …/docs      # already path-style
```

Prod Caddy is **single host** `hub.lazuar.com` with paths `/`, `/portal`, `/docs`, `/admin`.

**Minimal polish** (only if already editing README for 7.5):

```text
│   ├── lazuar-ops/          # Back-office (Vite)     -> hub.lazuar.com/
│   ├── lazuar-portal/       # Checkout (Next)        -> hub.lazuar.com/portal
│   ├── lazuar-admin/        # Platform admin (Vite)  -> hub.lazuar.com/admin
│   ├── lazuar-developers/   # Scalar OpenAPI hub     -> hub.lazuar.com/docs
```

Architecture diagram line with `portal.lazuar.com` can stay as product marketing or get a one-line footnote — **not required for Phase 7**.

### 7.4.3 Pre-existing CI (out of Phase 7 rename scope)

Phase 6 noted: `ci.yml` contracts red — `pnpm/action-setup` `version: 9` vs `packageManager: pnpm@11.5.2`.  
**Do not** fold into Phase 7 rename archaeology unless explicitly wanted; track as separate CI hygiene.

---

## 8. 7.5 Naming debt that stays by design

Document once (recommend short table under root README glossary blurb **or** append to `plans/002-change-name/README.md`). **No renames.**

| Layer A | Layer B | Why OK |
|---------|---------|--------|
| Backend `Modules/Ops`, routes `/api/v1/ops` | App `lazuar-ops` | Different domains (backend module vs FE package) |
| Public path `/docs` (Developer Hub UI) | App `lazuar-docs` (VitePress guides) | Different products; `/docs` is Scalar hub |
| GHCR `lazuar-hub-superadmin` | App `lazuar-admin` | Image name frozen; folder matches UI “Admin” |
| Prod containers `hub-*` / compose services `ops`, `portal`, … | Local compose `lazuar-*` / containers `lazuar-*` | Prod short names + Caddy DNS; local monorepo names |
| GHCR `lazuar-hub-developers` | App `lazuar-developers` | Aligned product; image prefix `lazuar-hub-` is deploy brand |
| Package `lazuar-developers` | `packages/api-spec` | Spec SSoT vs Scalar UI — do **not** rename app to `lazuar-spec` |

**Suggested README addition** (after existing monorepo glossary lines ~113–114):

```markdown
**Intentional naming layers (do not “fix” by renaming):**
- Backend `Modules/Ops` ≠ need to match folder `lazuar-ops` one-for-one.
- Public `/docs` = `lazuar-developers` (Scalar); `lazuar-docs` = VitePress product guides.
- GHCR stays `lazuar-hub-*` (incl. `lazuar-hub-superadmin` for `lazuar-admin`).
- Prod containers `hub-*`; local compose services/containers `lazuar-*`.
```

---

## 9. Proposed Phase 7 PR (if implemented)

### 9.1 Title

```text
docs: optional rename archaeology banners + tunnel:fe nit
```

### 9.2 Files (max set)

| File | Edit |
|------|------|
| `docs/001-gaps/README.md` | Banner §4.2 A |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | Banner §4.2 B |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | Banner §4.2 C |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | Banner §4.2 D |
| `Taskfile.yml` | Optional `tunnel:fe` Option A |
| `README.md` | Optional naming-debt blurb + optional hub path tree polish |

**Hard exclusions:** `deploy/**`, `docker-bake.hcl`, `docker-compose*.yml`, `.github/workflows/ghcr.yml`, all app source, GHCR tags.

### 9.3 Risk

| Risk | Level |
|------|-------|
| Runtime / deploy | **None** (docs + optional Taskfile desc/port) |
| Wrong tunnel port if Option A unused | Low — mprocs FE tunnel is manual (`autostart: false`) |
| Banner noise | Negligible |

### 9.4 Verification

```bash
# banners present
rg -n 'plan 002|Path note \(plan 002' \
  docs/001-gaps/README.md \
  docs/architecture-decision-log/007-product-scoped-api-references.md \
  docs/architecture-decision-log/013-frontend-module-implementation.md \
  docs/architecture-decision-log/017-portal-frontend-architecture.md

# no accidental GHCR / deploy edits
git diff --stat
# expect only docs (+ optional Taskfile/README)

# tunnel not pointing at 3020 if fixed
rg -n '3020|community-page' Taskfile.yml
# → no matches (if Option A/B/C applied)
```

### 9.5 Exit criteria (minimal Phase 7)

- [ ] Gaps index has rename map banner  
- [ ] ADR 013 + 017 (and preferably 007) have path notes  
- [ ] GHCR rebrand **not** started  
- [ ] Compose parity confirmed already done (no commit needed)  
- [ ] Naming debt documented (README or plan README)  
- [ ] (Optional) `tunnel:fe` no longer references community-page :3020  

---

## 10. What “done enough” looks like without Phase 7

If Phase 7 is **skipped entirely**:

- Production remains healthy; living commands already use `lazuar-*`.  
- Agents/humans may occasionally open ADR 013 and `cd apps/ops-page` → fail once, then check README.  
- `task tunnel:fe` remains a dead Aura leftover (only if someone starts the optional mprocs tunnel).  

That is **acceptable**. Phase 7 is polish, not completeness of the rename.

---

## 11. Analyzer non-actions

- Did **not** edit any banners, Taskfile, or README.  
- Did **not** open a PR.  
- Did **not** recommend GHCR dual-tag / image rename work.  

---

## 12. Implementer checklist (copy if executing)

- [ ] Create branch e.g. `docs/phase-7-rename-archaeology` from `main`  
- [ ] Apply banners A–D (§4.2)  
- [ ] Optionally apply `tunnel:fe` Option A (§7.4.1)  
- [ ] Optionally add naming-debt blurb (§8)  
- [ ] Confirm `git diff` excludes `deploy/`, bake, compose, ghcr workflow  
- [ ] Open small docs PR; merge anytime (no special deploy risk if Taskfile-only + docs)  
- [ ] Record `phase-7-done.md` if the team wants a closed loop  

---

*End of Phase 7 analysis. Prefer banners + optional tunnel nit; skip GHCR rebrand and bulk history rewrites.*
