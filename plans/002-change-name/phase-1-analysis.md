# Phase 1 — Analysis & implement brief (move apps + package identity)

**Status:** Analysis only — **do not implement in this file’s authoring step**; implementers follow §5–§8.  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`phase-0-analysis.md`](./phase-0-analysis.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 1, inventory [`10-master-reference-inventory.md`](./10-master-reference-inventory.md)

---

## 1. Phase 1 goal

Make **folder basenames** and **pnpm package `"name"` fields** match the locked `lazuar-*` convention.

| # | In scope | Out of scope (later phases) |
|---|----------|------------------------------|
| 1 | `git mv` the four frontend app directories | Dockerfiles, `docker-bake.hcl`, compose, GHCR workflow |
| 2 | Update each moved app’s `package.json` `"name"` | `mprocs-dev.yaml`, root/docs filter commands |
| 3 | **Optional:** path-header comments + backend C# comment-only `ops-page` wording | `pnpm-lock.yaml` regen, living docs, ADRs/gaps |

Workspace discovery stays `apps/*` / `packages/*` — **no** `pnpm-workspace.yaml` edit.

**PR strategy (locked Phase 0):** Phases 1–4 land in **one atomic PR**. Phase 1 alone leaves Docker/mprocs/lockfile pointing at old paths — that is expected mid-PR; do not merge Phase 1 by itself.

---

## 2. Decisions locked (restate)

| Current dir | Current `"name"` | **Target dir** | **Target `"name"`** |
|-------------|------------------|----------------|---------------------|
| `apps/developers-page` | `developers-page` | **`apps/lazuar-developers`** | **`lazuar-developers`** |
| `apps/ops-page` | `ops-page` | **`apps/lazuar-ops`** | **`lazuar-ops`** |
| `apps/portal-page` | `portal-page` | **`apps/lazuar-portal`** | **`lazuar-portal`** |
| `apps/superadmin-page` | `superadmin-page` | **`apps/lazuar-admin`** | **`lazuar-admin`** |

Do **not** use historical draft name `lazuar-spec` for developers (collides with `packages/api-spec` / `@repo/api-spec`).

Unchanged by Phase 1 (and entire rename): GHCR images `lazuar-hub-*`, deploy/prod services, public paths `/` `/portal` `/docs` `/admin`, dev ports 3002–3005, cookies/localStorage keys, backend modules/routes.

---

## 3. Pre-flight (run before any edit)

From repo root:

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

git branch --show-current
# expect: chore/rename-frontend-apps-lazuar-prefix

# Confirm old dirs exist and targets do not
ls -d apps/developers-page apps/ops-page apps/portal-page apps/superadmin-page
test ! -e apps/lazuar-developers && test ! -e apps/lazuar-ops \
  && test ! -e apps/lazuar-portal && test ! -e apps/lazuar-admin \
  && echo "targets free"

# Confirm package names still old
node -e '
const fs=require("fs");
for (const p of [
  "apps/developers-page/package.json",
  "apps/ops-page/package.json",
  "apps/portal-page/package.json",
  "apps/superadmin-page/package.json",
]) console.log(p, JSON.parse(fs.readFileSync(p,"utf8")).name);
'
# expect: developers-page, ops-page, portal-page, superadmin-page
```

Optional hygiene (do **not** commit build junk):

```bash
# Local-only clean; ignore if dirs absent
rm -rf apps/developers-page/.next apps/portal-page/.next \
  apps/ops-page/dist apps/superadmin-page/dist \
  apps/developers-page/tsconfig.tsbuildinfo 2>/dev/null || true
```

---

## 4. Exact inventory for Phase 1

### 4.1 Must-change — directories (`git mv`)

| From | To |
|------|-----|
| `apps/developers-page` | `apps/lazuar-developers` |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

After move, `ls apps/` should include (among others):  
`lazuar-api`, `lazuar-docs`, `lazuar-developers`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`  
and **no** `developers-page`, `ops-page`, `portal-page`, `superadmin-page`.

### 4.2 Must-change — `package.json` `"name"` only

Edit **only** the `"name"` field (leave scripts, deps, private, version alone).

| File (after `git mv`) | Old | New |
|-----------------------|-----|-----|
| `apps/lazuar-developers/package.json` | `"name": "developers-page"` | `"name": "lazuar-developers"` |
| `apps/lazuar-ops/package.json` | `"name": "ops-page"` | `"name": "lazuar-ops"` |
| `apps/lazuar-portal/package.json` | `"name": "portal-page"` | `"name": "lazuar-portal"` |
| `apps/lazuar-admin/package.json` | `"name": "superadmin-page"` | `"name": "lazuar-admin"` |

Verified current line 2 of each file (pre-move paths):

```text
apps/developers-page/package.json  →  "name": "developers-page",
apps/ops-page/package.json         →  "name": "ops-page",
apps/portal-page/package.json      →  "name": "portal-page",
apps/superadmin-page/package.json  →  "name": "superadmin-page",
```

### 4.3 Optional — path-header comments (line 1 of source files)

These are **banner comments only** (not imports). Prefer a full-string path replace so you never touch bare tokens like `ops` / `portal`.

#### Ops → `apps/lazuar-ops` (7 files under moved tree)

| File (post-move path) | Line 1 today | Target line 1 |
|-----------------------|--------------|---------------|
| `apps/lazuar-ops/src/hooks/use-chat-stream.ts` | `// apps/ops-page/src/hooks/use-chat-stream.ts` | `// apps/lazuar-ops/src/hooks/use-chat-stream.ts` |
| `apps/lazuar-ops/src/hooks/use-debounce.ts` | `// apps/ops-page/src/hooks/use-debounce.ts` | `// apps/lazuar-ops/src/hooks/use-debounce.ts` |
| `apps/lazuar-ops/src/components/OpsChatWorkspace.tsx` | `// apps/ops-page/src/components/OpsChatWorkspace.tsx` | `// apps/lazuar-ops/src/components/OpsChatWorkspace.tsx` |
| `apps/lazuar-ops/src/components/chat/ChatMessageBubble.tsx` | `// apps/ops-page/src/components/chat/ChatMessageBubble.tsx` | `// apps/lazuar-ops/src/components/chat/ChatMessageBubble.tsx` |
| `apps/lazuar-ops/src/components/chat/MarkdownContent.tsx` | `// apps/ops-page/src/components/chat/MarkdownContent.tsx` | `// apps/lazuar-ops/src/components/chat/MarkdownContent.tsx` |
| `apps/lazuar-ops/src/components/forms/AutoForm.tsx` | `// apps/ops-page/src/components/chat/AutoForm.tsx` | `// apps/lazuar-ops/src/components/forms/AutoForm.tsx` |
| `apps/lazuar-ops/src/types/chat.ts` | `// apps/ops-page/src/types/chat.ts` | `// apps/lazuar-ops/src/types/chat.ts` |

**Note on AutoForm:** header today is **wrong path segment** (`components/chat/` vs real `components/forms/`). Optional Phase 1 should fix both the app basename **and** the folder segment to match the real file path.

#### Portal → `apps/lazuar-portal` (6 files)

| File (post-move path) | Replace prefix |
|-----------------------|----------------|
| `apps/lazuar-portal/src/modules/checkout/components/PromoCodeInput.tsx` | `// apps/portal-page/` → `// apps/lazuar-portal/` |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx` | same |
| `apps/lazuar-portal/src/modules/checkout/components/IdentityBanner.tsx` | same |
| `apps/lazuar-portal/src/modules/core/lib/server-client.ts` | same |
| `apps/lazuar-portal/src/app/not-found.tsx` | same |
| `apps/lazuar-portal/src/app/page.tsx` | same |

#### Superadmin (stale copy-from-ops headers — 2 files)

These still say `apps/ops-page/...` even under superadmin. After move, correct them to **admin** paths (not ops):

| File (post-move path) | Today | Target |
|-----------------------|-------|--------|
| `apps/lazuar-admin/src/hooks/use-debounce.ts` | `// apps/ops-page/src/hooks/use-debounce.ts` | `// apps/lazuar-admin/src/hooks/use-debounce.ts` |
| `apps/lazuar-admin/src/types/chat.ts` | `// apps/ops-page/src/types/chat.ts` | `// apps/lazuar-admin/src/types/chat.ts` |

#### Developers

No `// apps/developers-page` path-header hits under `apps/developers-page` — nothing optional here.

### 4.4 Optional — backend comment-only `ops-page` (product wording)

Not path headers; English comments. Safe optional rewrite for consistency:

| File | Approx. line | Today | Suggested |
|------|--------------|-------|-----------|
| `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | ~263 | `// Platform superadmins can operate any active workspace (ops-page requires ≥1 entitlement).` | `... (lazuar-ops / ops UI requires ≥1 entitlement).` **or** keep product wording “ops UI” without folder string |
| `apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` | ~82 | `// Ensure superadmin can open ops-page (memberships drive /me/entitlements for non-global paths).` | `// Ensure superadmin can open lazuar-ops (memberships ...)` **or** `// ... open the ops app (...)` |

Recommend **minimal** wording: replace token `ops-page` → `lazuar-ops` only if you want folder alignment; otherwise leave alone (cosmetic; zero runtime).

### 4.5 Nested app lockfiles (do not treat as workspace SoT)

May exist under old/new trees:

- `apps/developers-page/pnpm-lock.yaml` → moves with dir; **do not** hand-edit; root `pnpm-lock.yaml` is Phase later
- `apps/portal-page/pnpm-lock.yaml` → same

Leave nested lockfiles as-is for Phase 1 (they ride along with `git mv`).

---

## 5. Exact implement steps (shell + edits)

Run from repo root. Order matters: **move first**, then edit package.json under **new** paths.

### 5.1 Directory moves (preserve history)

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

git mv apps/developers-page apps/lazuar-developers
git mv apps/ops-page apps/lazuar-ops
git mv apps/portal-page apps/lazuar-portal
git mv apps/superadmin-page apps/lazuar-admin
```

Verify:

```bash
ls apps/
# must include: lazuar-developers lazuar-ops lazuar-portal lazuar-admin
# must NOT include: developers-page ops-page portal-page superadmin-page

git status --short | head -50
# expect R  (rename) entries for the four trees
```

If `git mv` fails because of untracked local noise, either remove/ignore that noise or use:

```bash
# only if needed — last resort, still prefer git mv for tracked files
git mv -k apps/developers-page apps/lazuar-developers
# etc.
```

Do **not** use plain `mv` + `git add` unless `git mv` is impossible; history renames are preferred.

### 5.2 Package `"name"` fields

**Option A — precise one-line edits** (any editor / `jq`):

```bash
# jq in-place pattern (macOS: needs jq installed)
for pair in \
  "lazuar-developers:lazuar-developers" \
  "lazuar-ops:lazuar-ops" \
  "lazuar-portal:lazuar-portal" \
  "lazuar-admin:lazuar-admin"
do
  dir="${pair%%:*}"
  name="${pair##*:}"
  f="apps/${dir}/package.json"
  tmp=$(mktemp)
  jq --arg n "$name" '.name = $n' "$f" > "$tmp" && mv "$tmp" "$f"
done
```

**Option B — manual** (if avoiding jq): open each file and change only line 2:

| File | Set to |
|------|--------|
| `apps/lazuar-developers/package.json` | `"name": "lazuar-developers",` |
| `apps/lazuar-ops/package.json` | `"name": "lazuar-ops",` |
| `apps/lazuar-portal/package.json` | `"name": "lazuar-portal",` |
| `apps/lazuar-admin/package.json` | `"name": "lazuar-admin",` |

Verify:

```bash
node -e '
const fs=require("fs");
const expect={
  "apps/lazuar-developers/package.json":"lazuar-developers",
  "apps/lazuar-ops/package.json":"lazuar-ops",
  "apps/lazuar-portal/package.json":"lazuar-portal",
  "apps/lazuar-admin/package.json":"lazuar-admin",
};
for (const [p,n] of Object.entries(expect)) {
  const got=JSON.parse(fs.readFileSync(p,"utf8")).name;
  console.log(got===n?"OK":"FAIL", p, got);
}
'
```

### 5.3 Optional — path-header comments

**Safe bulk approach** (only full path prefixes; run after `git mv`):

```bash
# Ops headers
rg -l '// apps/ops-page/' apps/lazuar-ops --glob '!node_modules/**' --glob '!dist/**' \
  | while read -r f; do
      # AutoForm special-case: fix chat→forms if present
      if [[ "$f" == *"/forms/AutoForm.tsx" ]]; then
        sed -i '' '1s|^// apps/ops-page/src/components/chat/AutoForm.tsx$|// apps/lazuar-ops/src/components/forms/AutoForm.tsx|' "$f" \
          || sed -i '' 's|// apps/ops-page/|// apps/lazuar-ops/|g' "$f"
      else
        sed -i '' 's|// apps/ops-page/|// apps/lazuar-ops/|g' "$f"
      fi
    done

# Portal headers
rg -l '// apps/portal-page/' apps/lazuar-portal --glob '!node_modules/**' --glob '!.next/**' \
  | while read -r f; do
      sed -i '' 's|// apps/portal-page/|// apps/lazuar-portal/|g' "$f"
    done

# Superadmin stale ops headers → admin paths (do NOT leave as lazuar-ops)
sed -i '' 's|// apps/ops-page/src/hooks/use-debounce.ts|// apps/lazuar-admin/src/hooks/use-debounce.ts|' \
  apps/lazuar-admin/src/hooks/use-debounce.ts
sed -i '' 's|// apps/ops-page/src/types/chat.ts|// apps/lazuar-admin/src/types/chat.ts|' \
  apps/lazuar-admin/src/types/chat.ts
```

Linux note: drop the `''` after `sed -i` (GNU sed). On macOS BSD sed, keep `sed -i ''`.

**Manual fallback for AutoForm only:**

```text
// apps/lazuar-ops/src/components/forms/AutoForm.tsx
```

Verify headers gone for old prefixes inside the four apps:

```bash
rg -n '// apps/(ops|portal|developers|superadmin)-page/' \
  apps/lazuar-ops apps/lazuar-portal apps/lazuar-admin apps/lazuar-developers \
  --glob '!node_modules/**' --glob '!dist/**' --glob '!.next/**'
# expect: no matches if optional headers done
```

### 5.4 Optional — backend comments

```bash
# Review only first:
rg -n 'ops-page' \
  apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs \
  apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs
```

If updating:

| File | Replace |
|------|---------|
| `Endpoints.cs` | `ops-page` → `lazuar-ops` **inside that one comment** only |
| `SystemGenesisBootstrapperJob.cs` | same |

Do **not** bulk-replace `ops-page` across all of `lazuar-api` (only these two comment hits are known).

### 5.5 Explicitly do **not** touch in Phase 1

| Path / area | Why deferred |
|-------------|--------------|
| `apps/*/Dockerfile` (all four) | Phase 2 — `COPY` / `--filter ./apps/...` / Next `CMD` |
| `docker-bake.hcl` | Phase 2 — targets + dockerfile paths |
| `docker-compose.yml` | Phase 2 — service keys + dockerfile paths |
| `docker-compose.ghcr.yml` | Phase 2 |
| `.github/workflows/ghcr.yml` | Phase 2 — matrix `dockerfile:` only |
| `mprocs-dev.yaml` | Later phase — `cd apps/*-page` shells |
| `pnpm-lock.yaml` (root) | Later phase — regenerate via `pnpm install` after path + name settle |
| `pnpm-workspace.yaml` | No change ever needed (`apps/*`) |
| Root `package.json`, `turbo.json`, `Taskfile.yml` | No `*-page` package strings for these four apps |
| `deploy/**`, `scripts/**` | Non-goals entire rename |
| `apps/lazuar-docs/**`, root `README.md`, `docs/**` | Later docs phase (note: `openapi.md` still has `pnpm --filter developers-page dev` — **not** Phase 1) |
| `plans/**` inventory text | Leave historical analysis as-is unless you are documenting completion |
| Runtime UI strings, cookies, API routes, GHCR image names | Non-goals |

---

## 6. Mid-PR breakage (expected)

After Phase 1 **only**, these still reference **old** paths/names and will fail until Phase 2+:

| Consumer | Still says |
|----------|------------|
| Four `Dockerfile`s | `apps/*-page` |
| `docker-bake.hcl` | targets + dockerfile paths `*-page` |
| Compose files | `ops-page` / `portal-page` / `superadmin-page` services |
| `mprocs-dev.yaml` | `cd apps/*-page` |
| Root `pnpm-lock.yaml` importers | `apps/developers-page:` etc. |
| Living doc | `pnpm --filter developers-page` in `apps/lazuar-docs/docs/reference/openapi.md` |

**Still OK after Phase 1:**

- `pnpm-workspace.yaml` discovers packages under new dirs via `apps/*`
- App-internal relative imports / TS paths (no monorepo path in source runtime)
- Shared workspace deps `@repo/api-types-ts` / etc. (names unchanged)
- Apps are **leaves** (nothing depends on package name `ops-page` via `workspace:` reverse dep)

**Avoid until later phases:**

```bash
# Do NOT run lockfile regen in Phase 1 if following strict phase isolation
# pnpm install   # → Phase with lockfile + Docker/mprocs updates

# Do NOT expect these to work until Phase 2+:
# docker buildx bake
# mprocs -c mprocs-dev.yaml
```

Optional smoke (workspace sees new package names without rewriting lockfile — may warn):

```bash
pnpm m ls --depth -1 2>/dev/null | rg 'lazuar-(developers|ops|portal|admin)' || true
# If pnpm complains about missing importers, ignore until lockfile phase
```

---

## 7. Verification checklist (Phase 1 exit)

Run all of these before marking Phase 1 done:

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

# 1) Dirs
test -d apps/lazuar-developers && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal && test -d apps/lazuar-admin \
  && test ! -e apps/developers-page && test ! -e apps/ops-page \
  && test ! -e apps/portal-page && test ! -e apps/superadmin-page \
  && echo "dirs OK"

# 2) package names
node -e '
const e={
 "apps/lazuar-developers/package.json":"lazuar-developers",
 "apps/lazuar-ops/package.json":"lazuar-ops",
 "apps/lazuar-portal/package.json":"lazuar-portal",
 "apps/lazuar-admin/package.json":"lazuar-admin"};
let bad=0;
for (const [p,n] of Object.entries(e)) {
  const g=JSON.parse(require("fs").readFileSync(p,"utf8")).name;
  if (g!==n) { console.error("FAIL",p,g); bad++; }
}
process.exit(bad);
' && echo "names OK"

# 3) No accidental Phase-2 file edits
git diff --name-only | rg -n 'Dockerfile|docker-bake|docker-compose|mprocs|pnpm-lock|ghcr\.yml|Taskfile' \
  && echo "UNEXPECTED Phase-2 paths in diff — review" || echo "no Phase-2 paths in unstaged diff OK"

# 4) Optional headers (if you chose to do them)
rg -n '// apps/(ops|portal|developers|superadmin)-page/' \
  apps/lazuar-ops apps/lazuar-portal apps/lazuar-admin apps/lazuar-developers \
  --glob '!node_modules/**' --glob '!dist/**' --glob '!.next/**' || true
# empty = headers cleaned

# 5) Optional backend
rg -n 'ops-page' apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs \
  apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs || true
# empty if comments updated; 2 hits if skipped
```

| Criterion | Pass when |
|-----------|-----------|
| Four new dirs exist | `lazuar-developers`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin` |
| Four old frontend dirs gone | no `*-page` under `apps/` for those four |
| Four package names match folders | table in §2 |
| No Docker/compose/bake/mprocs/lockfile/docs edits | `git diff` limited to moved trees + package.json (+ optional comments) |
| Workspace leaves intact | no reverse `workspace:` dep on old names |
| Git history | renames recorded via `git mv` (status shows renames, not delete+add, for tracked files) |

---

## 8. Suggested commit shape (only if committing Phase 1 alone mid-PR)

Prefer **one commit with Phases 1–4** at end of PR. If intermediate commits are needed:

```text
chore(apps): git mv *-page → lazuar-* and rename package.json names

- developers-page → lazuar-developers
- ops-page → lazuar-ops
- portal-page → lazuar-portal
- superadmin-page → lazuar-admin
Optional: path-header comments + backend ops-page comment wording.
Docker/compose/bake/mprocs/lockfile intentionally unchanged (next phase).
```

Stage only Phase 1 paths:

```bash
git add apps/lazuar-developers apps/lazuar-ops apps/lazuar-portal apps/lazuar-admin
# if optional backend comments:
# git add apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs \
#         apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs

git status   # must not include docker-bake, compose, mprocs, lockfile, docs
```

---

## 9. Explicit out of scope for this Phase 1 deliverable

- No Dockerfile / bake / compose / CI / mprocs / lockfile / docs implementation  
- No GHCR or deploy renames  
- No bulk historical ADR/gap rewrites  
- This analysis file is instructions only; **authoring it is not the rename**

**Next phase:** Phase 2 — Docker, bake, compose, CI paths (`11-implementation-checklist.md` § Phase 2), keeping image tags `lazuar-hub-*` unchanged.

---

## 10. Quick copy-paste implementer sequence

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

# --- must ---
git mv apps/developers-page apps/lazuar-developers
git mv apps/ops-page apps/lazuar-ops
git mv apps/portal-page apps/lazuar-portal
git mv apps/superadmin-page apps/lazuar-admin

# set package names (jq) — or edit manually
for pair in lazuar-developers:lazuar-developers lazuar-ops:lazuar-ops \
            lazuar-portal:lazuar-portal lazuar-admin:lazuar-admin; do
  dir="${pair%%:*}"; name="${pair##*:}"
  tmp=$(mktemp)
  jq --arg n "$name" '.name = $n' "apps/${dir}/package.json" > "$tmp" && mv "$tmp" "apps/${dir}/package.json"
done

# --- optional headers ---
# (see §5.3)

# --- verify ---
# (see §7)
```

**Do not implement beyond this brief without proceeding to Phase 2 tooling paths in the same PR.**
