# C12 — One `/me` projection types

**Track:** Whoami · **Depends:** C11  
**Analysis:** [01](../01-one-http-surface.md) `/me` fields, [04](../04-pay-spec-contract.md) projection rule, [06](../06-tenant-org.md)  
**Goal:** Deserialize One JSON and map to Pay whoami DTO. **Still no endpoint.**

---

## C12.1 One wire types (internal)

- [x] Types for One `GET /me` snake_case: `user_id`, `email`, `is_platform_admin`, `active_tenant_id`, `active_role`, `tenants[]` with `id`, `slug`, `name`, `role`, `status`
- [x] Do **not** deserialize Zitadel `urn:zitadel:iam:org:project:roles`
- [x] Do **not** require `permissions[]` on the Pay DTO (One may send them; Pay whoami omits invite inbox and permission catalogs)

## C12.2 Pay whoami DTO (public)

- [x] `user_id`
- [x] `email`
- [x] `is_platform_admin`
- [x] `active_org_id` ← One `active_tenant_id` (same string; One tenant id **is** org_id)
- [x] `tenants[]`: `id`, `slug`, `name`, `role`, `status` (`id` is org_id)
- [x] No `AuthUser`, no Hub cookie fields, no `CLIENT`/`ADMIN` dual vocab

## C12.3 Mapping rules

- [x] Missing `user_id` from One → treat as One failure (do not invent a Pay user id)
- [x] Empty `tenants` is valid (Ada with no workspace yet)
- [x] `role` copied as One sent it (`owner` / `admin` / `member`) — do not map to Hub `VIEWER`

## C12.4 Exit

- [x] Mapping is a function in the host project, unit-testable without HTTP if cheap; otherwise covered in C16
- [x] Unblocked for C13
