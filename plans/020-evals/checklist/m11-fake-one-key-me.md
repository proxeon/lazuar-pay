# M11 — Fake One `/me` for a bound key

**Track:** M · **Depends:** M10  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4  
**Goal:** Tests can distinguish JWT `/me` vs key `/me` without sibling One.

**Why:** Every hermetic test today uses `"Bearer tok"` and a human `/me` with `role: owner`. Without a key-shaped fixture, M12–M19 will accidentally keep the owner overlay and ship a false “keys work” test.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs` | Default responder |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` | `Owner` `/me` JSON |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` | Whoami mapping |
| Sibling One `GetMeForApiKey` (read-only) | Shape: `user_id` = key id, `role: member`, one tenant |

**Current (`6d730d15`):** Fake One does not inspect `lzr_sk_`. `PayTest.Owner` is a human JWT `/me`.

---

## M11.1 Fixture

- [x] Helper JSON: `user_id` is a **key id GUID** (not `u1`)
- [x] `is_platform_admin`: false
- [x] `tenants`: **exactly one** `{ id: "t1", role: "member", status: "active" }`
- [x] No `permissions` required (One ROLE-03)

## M11.2 Fake One

- [x] When `Authorization` remainder starts `lzr_sk_`, `GET …/me` returns the key fixture
- [x] When JWT-shaped / `"Bearer tok"`, keep owner/member fixtures already used
- [x] `POST …/authz/check` for a key **without** `user_id` may still 400 — M12 must stop sending that

## M11.3 Tests

- [x] Fixture unit or whoami test: key Bearer → Pay whoami `tenants[0].id == t1`, `user_id` is the key id
- [x] Do not assert `role == owner` for keys

## M11.4 Must not

- [x] Do not import One test factory / One csproj
- [x] Do not hit live `:8080` in CI

## M11.5 Exit

- [x] Unblocked for M12
