# C23 — Path is SoT; header cannot authorize

**Track:** Authz · **Depends:** C22  
**Analysis:** [02](../02-one-authn-tokens.md), [06](../06-tenant-org.md), [07](../07-authz-roles.md)  
**Goal:** `X-Lazuar-Tenant-Id` is never enough.

---

## C23.1 Behavior

- [ ] `GET /v1/orgs/{orgId}/ready` checks **path** `{orgId}` against One, not the header
- [ ] If header is present and **differs** from path, still check **path** id (header may be forwarded to One as hint only)
- [ ] There is **no** route that authorizes using header alone (no `GET /v1/orgs/ready` without id)

## C23.2 Tests

- [ ] Path tenant A, header tenant B, One allow only if check id is A — fake asserts body.id == A
- [ ] Do not add a header-only success test

## C23.3 Naming

- [ ] Use `X-Lazuar-Tenant-Id` if forwarding (One’s name). Do not invent `X-Tenant-Id` as SoT (that is old Hub ops)

## C23.4 Exit

- [ ] Tests in C23.2 green
- [ ] Unblocked for C24
