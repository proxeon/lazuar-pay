# C21 — Authz error mapping

**Track:** Authz · **Depends:** C20  
**Analysis:** [07](../07-authz-roles.md)  
**Goal:** Deny and One-down never look like ready.

---

## C21.1 Token

- [x] Missing Bearer → **401** (no One call)

## C21.2 One membership façade

- [x] One **403** (caller is not in that tenant) → Pay **403**, no `{ ready: true }`
- [x] One **200** `{ "allowed": false }` → Pay **403** (not 200)

## C21.3 Fail closed

- [x] One **400** (bad type — should not happen if we send tenant/member) → Pay **503**
- [x] One **5xx** / timeout / transport → Pay **503**
- [x] Unparseable body → Pay **503**

## C21.4 Exit

- [x] No path returns 200 unless `allowed: true`
- [x] Unblocked for C22
