# M10 — Bearer family prefix

**Track:** M · **Depends:** K00  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.1 / hole 6  
**Goal:** Pay knows a machine key by prefix **before** forwarding junk families to One.

**Why:** A second app (or a confused merchant) can send Stripe `sk_live_…` or Hub `sk_test_…` as `Authorization`. `Bearer.TryGet` only checks the `Bearer ` prefix and forwards the rest to One. One then 401s. That is fail-closed but slow, leaky (One sees the wrong secret family), and 012 wanted Pay to reject the family at the door.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs` | `TryGet` — `Bearer ` prefix only |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | Forwards that Authorization to One |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | No family check |
| `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` | Family B: Stripe `sk_` in **JSON body**, not Authorization |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` | Uses `"Bearer tok"` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs` | Counts One calls |

**Current (`6d730d15`):** `Bearer.TryGet` returns true for any non-empty remainder. No `lzr_sk_` helper. Vault PUT still the only legal `sk_test` on the host.

---

## M10.1 Detect

- [ ] `Bearer.TryGet` still extracts the remainder after `Bearer `
- [ ] Add a small helper: remainder that starts with `lzr_sk_` is a **machine key**
- [ ] JWT path is “not `lzr_sk_` and not rejected family”
- [ ] Do not parse JWT claims in Pay

## M10.2 Reject wrong family as Pay caller (fail closed at Pay)

- [ ] Remainder starting `sk_live_` / `sk_test_` / `sk_` (Hub) → **401** Invalid bearer, **do not** call One
- [ ] Remainder looking like a Zitadel PAT (document the prefix you reject; if unknown, still do not invent PAT storage)
- [ ] Stripe vault `sk_test` on **gateway PUT body** is unchanged (Family B)

## M10.3 Tests

- [ ] `Bearer_sk_live_is_401_skips_one` — Fake One send count 0
- [ ] `Bearer_lzr_sk_is_not_rejected_at_parser` — reaches One (whoami 401/200 from Fake)
- [ ] Existing JWT `"Bearer tok"` still reaches One

## M10.4 Must not

- [ ] No hash lookup table
- [ ] No `X-Api-Key` header
- [ ] No wrap of caller secrets in SecretBox

## M10.5 Exit

- [ ] Unblocked for M11
