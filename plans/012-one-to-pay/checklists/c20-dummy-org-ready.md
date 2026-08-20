# C20 — Dummy `GET /v1/orgs/{orgId}/ready` + authz/check

**Track:** Authz · **Depends:** C16  
**Analysis:** [07](../07-authz-roles.md)  
**Goal:** Path tenant is SoT. One `member` check before any fixture.

---

## C20.1 Route

- [ ] `GET /v1/orgs/{orgId}/ready`
- [ ] `{orgId}` is the One tenant id (same string as whoami `tenants[].id`)
- [ ] Require Bearer (same rules as whoami)
- [ ] **Not** a checkout, not BYOK, not a real admin surface

## C20.2 One call

- [ ] `POST {BaseUrl}/tenants/{orgId}/authz/check`
- [ ] JSON body: `{ "relation": "member", "object": { "type": "tenant", "id": "<orgId>" } }`
- [ ] Forward `Authorization` verbatim
- [ ] Do **not** send `user_id` when the caller is a user JWT (One uses `sub`)
- [ ] Do **not** call `authz/write`
- [ ] Do **not** use object type `payment` or `document`

## C20.3 Success

- [ ] One 200 and `{ "allowed": true }` → Pay 200 fixture e.g. `{ "org_id": "{orgId}", "ready": true }`
- [ ] Do not return One’s raw authz body as the product contract (Pay JSON is the door)

## C20.4 Exit

- [ ] Happy path only is enough if C21 is next commit
- [ ] Unblocked for C21
