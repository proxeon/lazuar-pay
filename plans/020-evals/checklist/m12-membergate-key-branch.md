# M12 — MemberGate branch for `lzr_sk_`

**Track:** M · **Depends:** M11  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.1 steps 1–6; [`../05-identity-authz-tenancy.md`](../05-identity-authz-tenancy.md)  
**Goal:** Keys never hit `authz/check` with omitted `user_id` (live One 400).

**Why:** This is the kernel door. Live One `POST /tenants/{id}/authz/check` **requires** `user_id` when the Bearer is an API key, and **rejects** the key id as that subject. Pay’s `CheckMemberAsync` body is only `{ relation: member, object: { type: tenant, id } }`. A valid `lzr_sk_` therefore 400s on every org-gated door. Whoami (`GET /me`) already works for keys.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | `RequireMemberAsync` always `CheckMemberAsync` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | `CheckMemberAsync` omits `user_id`; `GetWhoamiAsync` → `/me` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs` | Already forwards any Bearer to `/me` |
| Sibling One `AuthzEndpoints` / `RejectApiKeyAuthzSubject` | Live 400 rule (do not copy the type; read as HTTP) |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs` | Must grow a key branch |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs` | JWT `authz/check` path to keep green |

**Current (`6d730d15`):** JWT `"Bearer tok"` + Fake `{allowed:true}` is the only hermetic member path. No test sends `lzr_sk_`.

---

## M12.1 Branch

- [ ] `RequireMemberAsync`: if Bearer is `lzr_sk_`, **do not** call `CheckMemberAsync`
- [ ] Instead call One `GET /me` with the same Authorization (reuse whoami client)
- [ ] One 401 → Pay 401
- [ ] One timeout / 5xx → Pay 503 (existing transport mapping)
- [ ] JWT path **unchanged**: still `authz/check` omit `user_id`, then writer overlay on `/me` for writers

## M12.2 Must not

- [ ] Do not send key id as `authz/check` `user_id` (One 400 impersonation)
- [ ] Do not require the second app to pass Ada’s Zitadel sub
- [ ] Do not skip One entirely for keys

## M12.3 Tests (expect still not writer until M14)

- [ ] Key `/v1/orgs/t1/ready` after M13 will be 200 — until M13, may 403 if bound-tenant not wired
- [ ] JWT `"Bearer tok"` still POSTs `authz/check` (assert Fake One last path)

## M12.4 Exit

- [ ] Unblocked for M13
