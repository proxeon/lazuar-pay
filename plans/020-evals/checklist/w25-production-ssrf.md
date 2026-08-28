# W25 — Production SSRF

**Track:** W · **Depends:** W13, W14  
**Goal:** Metadata and loopback cannot be registered when env is Production.

**Why:** A Production factory test already exists for CORS throw and Test rail off. Same trick: `EnvironmentName=Production` + WrapKey + CORS so boot succeeds, PUT loopback 400.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs` | Production factory |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs` | `Ready_is_false_without_vault_when_test_is_off` unit |
| W13 / W14 | Validator + door |

**Current (`6d730d15`):** Production CORS empty throws; no webhook URL check.

---

## W25.1

- [ ] Factory `EnvironmentName = Production` + WrapKey set as tests already do for other Production tests
- [ ] PUT `http://127.0.0.1/hook` → 400
- [ ] PUT `http://169.254.169.254/` → 400
- [ ] PUT `https://app.example/hook` → allowed (no DNS in unit test — validate host parser, not live resolve, **or** allow https hostnames without resolving in this phase)

## W25.2 Must not

- [ ] Do not DNS-resolve in a way that TOCTOU-binds to 169.254 after validation without a follow-up; hatch: parse IP literals strictly; hostname allow https

## W25.3 Exit

- [ ] Unblocked for W26
