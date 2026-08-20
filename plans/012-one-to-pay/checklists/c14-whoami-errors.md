# C14 — Whoami error mapping (fail closed)

**Track:** Whoami · **Depends:** C13  
**Analysis:** [10](../10-dogfood-and-tests.md), [02](../02-one-authn-tokens.md)  
**Goal:** One failure is never a Pay 200 with an empty user.

---

## C14.1 Client / token

- [ ] Missing or empty `Authorization` → **401**
- [ ] Header present but not `Bearer ` → **401** (do not send garbage to One)
- [ ] Do not special-case `id_token` by parsing JWT `typ`; if One 401s, Pay 401s

## C14.2 One responses

- [ ] One **401** → Pay **401**
- [ ] One **403** → Pay **403** (do not coerce to 200)
- [ ] One **404** on `/me` → Pay **503** (misconfigured BaseUrl / not One)
- [ ] One **5xx** → Pay **503**
- [ ] Transport failure / timeout → Pay **503**
- [ ] One **200** with unparseable body → Pay **503**

## C14.3 Bodies

- [ ] Error JSON is boring (status + title/detail). Do not leak One internals
- [ ] Do not return `{ user_id: null, tenants: [] }` on One 401

## C14.4 Exit

- [ ] Each bullet has a test in C16 or this commit
- [ ] Unblocked for C15
