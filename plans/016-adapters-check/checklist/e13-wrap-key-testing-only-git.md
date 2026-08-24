# E13 — Git wrap string only in Testing

**Track:** Env secrets · **Depends:** A00  
**Analysis:** live `SecretBox` hashes `"lazuar-pay-dev-wrap-key"` for every non-Production; 015 H16 asked outside Testing  
**IDs:** H16  
**Goal:** Staging/Development without `Pay:WrapKey` must not silently wrap with a git-known key.

---

## E13.1 Live today

- [ ] Production missing → throw
- [ ] Else SHA-256 of `"lazuar-pay-dev-wrap-key"`

## E13.2 Change

- [ ] Git default **only** when `IsEnvironment("Testing")`
- [ ] Any other environment missing `Pay:WrapKey` → throw `"Pay:WrapKey is required in Production"` **or** `"Pay:WrapKey is required"` (prefer the latter so Development is honest)

## E13.3 Must not

- [ ] Do not commit a real wrap key
- [ ] `.env.example` keeps `Pay__WrapKey` commented

## E13.4 Exit

- [ ] Unblocked for E14, E15
