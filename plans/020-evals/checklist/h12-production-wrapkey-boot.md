# H12 — Production empty WrapKey fails boot

**Track:** H · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.5  
**Goal:** Host that cannot vault does not listen.

**Why:** SecretBox throws on PUT if WrapKey missing outside Testing. Production can still **listen** and take public starts until someone PUTs vault. Fail boot.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` | Required outside Testing |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs` | Production throws on Protect |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | No ValidateOnStart |
| `apps/lazuar-pay/.env.example` | WrapKey commented |
| `apps/lazuar-pay/docker-compose.pay.yml` | `Pay__WrapKey: ${Pay__WrapKey:-}` empty |

**Current (`6d730d15`):** PUT 503; process still up.

---

## H12.1

- [ ] Outside Testing: missing/invalid `Pay:WrapKey` → throw at startup (`ValidateOnStart` or check after Build)
- [ ] Testing: existing dev wrap fallback may remain
- [ ] PUT-503 on missing WrapKey in Development can stay as belt; Production should not reach PUT

## H12.2 Tests

- [ ] WebApplicationFactory `EnvironmentName=Production` without WrapKey fails `CreateClient` / host start
- [ ] Do not weaken SecretBoxTests

## H12.3 Exit

- [ ] Unblocked for H13
