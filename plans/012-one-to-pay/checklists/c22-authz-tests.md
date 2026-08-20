# C22 — Hermetic authz tests

**Track:** Authz · **Depends:** C21  
**Analysis:** [07](../07-authz-roles.md) § tests  
**Goal:** Fake One handler covers allow and deny.

---

## C22.1 Fake

- [x] Handler distinguishes `GET /me` vs `POST …/authz/check` if both used
- [x] Assert check body: `relation=member`, `object.type=tenant`, `object.id` equals path `orgId`

## C22.2 Cases (one test each)

- [x] Allow: One 200 `{allowed:true}` → Pay 200 `ready: true` and `org_id`
- [x] Deny allowed false: One 200 `{allowed:false}` → Pay 403
- [x] Deny 403: One 403 → Pay 403
- [x] One 500 → Pay 503
- [x] No bearer → Pay 401; check endpoint **not** called

## C22.3 Exit

- [x] `task pay:test` green
- [x] Unblocked for C23
