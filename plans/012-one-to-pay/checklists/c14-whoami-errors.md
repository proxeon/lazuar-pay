# C14 — Whoami error mapping (fail closed)

**Track:** Whoami · **Depends:** C13  
**Analysis:** [10](../10-dogfood-and-tests.md), [02](../02-one-authn-tokens.md)  
**Goal:** One failure is never a Pay 200 with an empty user.

---

## C14.1 Client / token

- [x] Missing or empty `Authorization` → **401**
- [x] Header present but not `Bearer ` → **401** (do not send garbage to One)
- [x] Do not special-case `id_token` by parsing JWT `typ`; if One 401s, Pay 401s

## C14.2 One responses

- [x] One **401** → Pay **401**
- [x] One **403** → Pay **403** (do not coerce to 200)
- [x] One **404** on `/me` → Pay **503** (misconfigured BaseUrl / not One)
- [x] One **5xx** → Pay **503**
- [x] Transport failure / timeout → Pay **503**
- [x] One **200** with unparseable body → Pay **503**

## C14.3 Bodies

- [x] Error JSON is boring (status + title/detail). Do not leak One internals
- [x] Do not return `{ user_id: null, tenants: [] }` on One 401

## C14.4 Exit

- [x] Each bullet has a test in C16 or this commit
- [x] Unblocked for C15
