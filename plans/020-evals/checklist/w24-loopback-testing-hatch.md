# W24 — Testing loopback hatch

**Track:** W · **Depends:** W13, W14  
**Goal:** Sample and tests can listen on 127.0.0.1.

**Why:** One inbound SSRF blocks loopback (Pay cannot receive One pause on laptop without a tunnel). Plane C **from** Pay **to** the sample must allow loopback in Testing or E14 cannot run.

**Related files**

| Path | Role today |
|------|------------|
| W13 validator | Env switch |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | `EnvironmentName` |
| `examples/pay-node` | E11–E14 consumer |

**Current (`6d730d15`):** N/A.

---

## W24.1

- [ ] Environment Testing or Development: register `http://127.0.0.1:{port}/hook` **allowed**
- [ ] Production-shaped factory (`EnvironmentName = Production`) still 400s loopback (W25)

## W24.2 Tests

- [ ] Testing PUT `http://127.0.0.1:9/x` is 200 if other fields ok (or 400 only for empty host — **allow**)

## W24.3 Exit

- [ ] Unblocked for W25
