# M19 — Revoked key is 401

**Track:** M · **Depends:** M12  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4 test 17  
**Goal:** One 401 on the secret → Pay 401. No cache yet (P13).

**Why:** One `DELETE` of the key makes `/me` 401. Pay must not keep treating a previously seen secret as valid. Job A does **not** cache; this test locks uncached fail-closed.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs` | Maps One 401 → Pay 401 |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | Same 401 arm |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` | JWT 401 |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs` | Stateful responder |

**Current (`6d730d15`):** JWT 401 mapping exists. No revoked-key sequence.

---

## M19.1

- [ ] Fake One: first `/me` 200, then 401
- [ ] Second Pay whoami / mint → 401
- [ ] Do not keep allowing after One 401

## M19.2 Must not

- [ ] Do not cache `/me` in this phase
- [ ] Do not subscribe to `api_key.revoked` yet (P13)

## M19.3 Tests

- [ ] `Revoked_key_is_401`
- [ ] Existing JWT 401 mapping unchanged

## M19.4 Exit

- [ ] Unblocked for M20
