# M20 — Isolation: Pay is not an IdP

**Track:** M · **Depends:** M14  
**Analysis:** IsolationTests; [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) refuse  
**Goal:** Kernel door does not grow a second key table.

**Why:** Hub minted `sk_test_` / `sk_live_` in-process. 012 refused that for new Pay. IsolationTests already ban org/user tables and `Modules.One`. M14 can tempt a `pay_api_keys` table; this phase makes that CI-red.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | `Banned` / `BannedSrc`; no org/user tables |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | No api_keys set |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | No key row |
| Hub museum `GenerateApiCredentialCommand` | Contrast only |

**Current (`6d730d15`):** No Pay key table. Keep it.

---

## M20.1 Bans

- [ ] No table `api_keys` / `pay_api_keys` in `PayDbContext`
- [ ] No pepper config `ApiKeys:Pepper`
- [ ] No hasher of `lzr_sk_` in Pay
- [ ] IsolationTests: add tokens `pay_api_keys`, `GenerateApiCredential` if not already covered
- [ ] Existing bans still red: `Modules.One`, `MediatR`, org/user tables

## M20.2 Tests

- [ ] `Source_does_not_create_org_or_user_tables` still passes
- [ ] Grep in test: Pay src has no `sk_test_` mint

## M20.3 Must not

- [ ] Do not wrap merchant `lzr_sk_` in SecretBox (caller identity, not BYOK)

## M20.4 Exit

- [ ] Unblocked for M21
