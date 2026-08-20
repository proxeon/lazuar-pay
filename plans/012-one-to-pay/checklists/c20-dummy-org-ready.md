# C20 — Dummy `GET /v1/orgs/{orgId}/ready` + authz/check

**Track:** Authz · **Depends:** C16  
**Analysis:** [07](../07-authz-roles.md)  
**Goal:** Path tenant is SoT. One `member` check before any fixture.

---

## C20.1 Route

- [x] `GET /v1/orgs/{orgId}/ready`
- [x] `{orgId}` is the One tenant id (same string as whoami `tenants[].id`)
- [x] Require Bearer (same rules as whoami)
- [x] **Not** a checkout, not BYOK, not a real admin surface

## C20.2 One call

- [x] `POST {BaseUrl}/tenants/{orgId}/authz/check`
- [x] JSON body: `{ "relation": "member", "object": { "type": "tenant", "id": "<orgId>" } }`
- [x] Forward `Authorization` verbatim
- [x] Do **not** send `user_id` when the caller is a user JWT (One uses `sub`)
- [x] Do **not** call `authz/write`
- [x] Do **not** use object type `payment` or `document`

## C20.3 Success

- [x] One 200 and `{ "allowed": true }` → Pay 200 fixture e.g. `{ "org_id": "{orgId}", "ready": true }`
- [x] Do not return One’s raw authz body as the product contract (Pay JSON is the door)

## C20.4 Exit

- [x] Happy path only is enough if C21 is next commit
- [x] Unblocked for C21
