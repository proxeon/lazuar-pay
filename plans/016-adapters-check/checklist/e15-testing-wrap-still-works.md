# E15 — Testing still allows the git wrap key

**Track:** Env secrets · **Depends:** E13  
**Analysis:** `PayApiFactory` does not set `Pay:WrapKey`  
**IDs:** H16  
**Goal:** `task pay:test` stays hermetic without a committed key.

---

## E15.1

- [ ] `SecretBoxTests.Testing_allows_dev_wrap_key` — Testing + missing WrapKey, `Protect`/`Unprotect` round-trip `"x"`
- [ ] Existing PUT gateway tests still 200

## E15.2 Must not

- [ ] Do not set `Pay:WrapKey` in the factory to a production-looking value unless E12 needs it locally

## E15.3 Exit

- [ ] Green; suite still 200s PUT
