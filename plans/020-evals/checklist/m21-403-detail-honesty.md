# M21 — 403 detail honesty

**Track:** M · **Depends:** M12  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) hole 5  
**Goal:** Do not map One’s scope 403 to “Not a member of this org.”

**Why:** `MemberGate` 403 uses `SuspendedDetail` or the sentence `Not a member of this org`. One’s `API key lacks required scope authz:check.` is swallowed. Fail-closed is correct; the copy is a lie to the second-app author.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | 403 arm + `SuspendedDetail` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneCallResult.cs` | `Detail` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs` | `Ready_403_passes_through_suspended_detail` |

**Current (`6d730d15`):** Suspend copy is honest. Scope 403 is not.

---

## M21.1 Mapping

- [x] JWT `authz/check` 403: keep suspend pass-through; generic “Not a member” only when One has no better detail
- [x] If One detail contains `scope` / `API key lacks`, pass that sentence (or a fixed Pay sentence that is not “not a member”)
- [x] After M12 keys skip `authz/check`, this mostly hits leftover JWT/key mistakes — still fix the mapper

## M21.2 Tests

- [x] Fake One 403 `{ "detail": "API key lacks required scope authz:check." }` → Pay 403 body does **not** contain `Not a member`
- [x] Suspend 403 still contains `suspend`

## M21.3 Exit

- [x] Unblocked for M22
