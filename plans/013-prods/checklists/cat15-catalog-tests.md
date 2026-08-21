# CAT15 — Hermetic catalog tests

**Track:** Catalog · **Depends:** CAT12  
**Analysis:** [01](../01-production-ready-bar.md) §2.7, [04](../04-merchant-frontend.md)  
**Goal:** `task pay:test` proves create/list/authz without One/Zitadel. Health still skips One.

---

## CAT15.1 Factory

- [x] `WebApplicationFactory<Program>` + fake One `HttpMessageHandler` (same pattern as C16/C22)
- [x] Fake covers `/me` and `authz/check` if both are used
- [x] Tests do not read live network; do not skip on “One not running”

## CAT15.2 Cases (one test each)

- [x] **201:** owner/admin Bearer → create with `name` → body has `org_id` + `name`
- [x] **401:** no Authorization → 401; fake **not** called
- [x] **403 member-cannot-write:** `member` (M24) POST create (and price if CAT11 landed) → 403; no row
- [x] **403 other-org:** list/create for an org One does not allow → 403
- [x] **200 list:** member **can** GET list for an org they belong to
- [x] **Health:** `/health` 200 while fake throws if called (C15 still true)

## CAT15.3 Hygiene

- [x] `task pay:test` runs them
- [x] IsolationTests still ban cathedral strings

## CAT15.4 Exit

- [x] All CAT15.2 cases green
- [x] Catalog track complete pending B99
- [x] Unblocked for B99 catalog cell (not Hub dark)
