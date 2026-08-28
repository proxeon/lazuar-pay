# M17 — Whoami forwards `lzr_sk_`

**Track:** M · **Depends:** M11  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4 test 1; stale O13  
**Goal:** `GET /v1/whoami` with a machine key is 200 when One `/me` is 200.

**Why:** 013 `o13-lzr-sk.md` ticked Fake One 200 on `Bearer lzr_sk_…` without a test. Whoami already forwards Authorization; this phase **locks** that path so O13 is not a lie.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs` | Forward to `GetWhoamiAsync` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | `GET me` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` | JWT only |
| `plans/013-prods/checklists/o13-lzr-sk.md` | Stale tick (do not flip 011/11) |

**Current (`6d730d15`):** Whoami does not strip prefixes. Tests never send `lzr_sk_`.

---

## M17.1

- [ ] Whoami already forwards Authorization; confirm key prefix is not stripped
- [ ] Response projection same snake_case as JWT whoami
- [ ] `is_platform_admin` false for keys

## M17.2 Tests

- [ ] `Whoami_forwards_machine_key_shape` — Fake One last Authorization contains `lzr_sk_`
- [ ] Whoami 401 when Fake One 401s the key

## M17.3 Must not

- [ ] Do not JIT-create Pay rows on whoami

## M17.4 Exit

- [ ] O13 stale tick is now true; do not flip 011/11 from here
