# M16 — Missing Bearer never uses env key

**Track:** M · **Depends:** M14  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4 test 7; standing law no god-key  
**Goal:** Interactive routes stay caller-forward only.

**Why:** The failure mode of “M2M” is Pay putting one `lzr_sk_` in `.env` and attaching it to every outbound One call, or using it when the request has no Bearer. That is a platform PAT. Freeze: interactive whoami/member/writer **forward the caller**. Mode M is P12 only.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs` | `BaseUrl` + `TimeoutSeconds` only |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | No default Authorization |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | `AddOptions<OneOptions>().BindConfiguration` |
| `apps/lazuar-pay/.env.example` | No `One__ApiKey` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` | Missing Bearer skips One |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs` | `Ready_401_without_bearer_skips_one` |

**Current (`6d730d15`):** No env key exists. Keep it that way in Job A.

---

## M16.1

- [x] Do **not** add `One:ApiKey` / `Pay:OneApiKey` to `OneOptions` in this program (P12 parked)
- [x] `OneClient` must not set `DefaultRequestHeaders.Authorization` from config
- [x] Missing Bearer on member/writer doors → 401, Fake One send count 0

## M16.2 Tests

- [x] Factory `UseSetting("Pay:OneApiKey", "lzr_sk_env")` if the key exists as a trap — still 401 without request Bearer
- [x] Whoami without Bearer still skips One (existing)

## M16.3 Exit

- [x] Unblocked for M17
