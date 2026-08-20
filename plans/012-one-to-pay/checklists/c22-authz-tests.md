# C22 — Hermetic authz tests

**Track:** Authz · **Depends:** C21  
**Analysis:** [07](../07-authz-roles.md) § tests  
**Goal:** Fake One handler covers allow and deny.

---

## C22.1 Fake

- [ ] Handler distinguishes `GET /me` vs `POST …/authz/check` if both used
- [ ] Assert check body: `relation=member`, `object.type=tenant`, `object.id` equals path `orgId`

## C22.2 Cases (one test each)

- [ ] Allow: One 200 `{allowed:true}` → Pay 200 `ready: true` and `org_id`
- [ ] Deny allowed false: One 200 `{allowed:false}` → Pay 403
- [ ] Deny 403: One 403 → Pay 403
- [ ] One 500 → Pay 503
- [ ] No bearer → Pay 401; check endpoint **not** called

## C22.3 Exit

- [ ] `task pay:test` green
- [ ] Unblocked for C23
