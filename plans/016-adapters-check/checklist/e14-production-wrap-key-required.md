# E14 — Production missing wrap key throws

**Track:** Env secrets · **Depends:** E13  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) SecretBoxTests  
**IDs:** H16  
**Goal:** `Protect` cannot run on the git string in Production.

---

## E14.1 New `SecretBoxTests.cs`

- [ ] `Production_missing_wrap_key_throws` — `IHostEnvironment` Production, empty `Pay:WrapKey`, `Protect("x")` throws
- [ ] Message contains `Pay:WrapKey`

## E14.2 Exit

- [ ] Green
