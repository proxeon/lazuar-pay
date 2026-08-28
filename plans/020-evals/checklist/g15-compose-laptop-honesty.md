# G15 — `--profile apps` is laptop, not prod

**Track:** G · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.3  
**Goal:** Operators do not ship Development + empty WrapKey as production.

**Why:** `--profile apps` sets `ASPNETCORE_ENVIRONMENT` default Development, empty WrapKey, laptop CORS/VITE. Images exist (080) but the documented `up --profile apps` is still a laptop.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/docker-compose.pay.yml` | Comments + env |
| `apps/lazuar-pay/Dockerfile` | API image |
| `apps/lazuar-pay-merchant/Dockerfile` | Vite args |
| `docker-bake.hcl` | Group `pay` (080) |
| H12–H14 | Fail-boot if someone sets Production empty |

**Current (`6d730d15`):** Comments already say Production CORS must be HTTPS. Env still laptop-shaped.

---

## G15.1

- [ ] Compose comments: profile `apps` = laptop containers
- [ ] Either a separate `docker-compose.pay.prod.yml` **or** fail boot when Production + empty WrapKey (H12)
- [ ] Document Production CORS / VITE HTTPS origins
- [ ] Do not set `ASPNETCORE_ENVIRONMENT=Production` in profile apps **with** laptop CORS unless H12/H14 catch it

## G15.2 Must not

- [ ] Do not retarget root Hub compose onto 8081

## G15.3 Exit

- [ ] Unblocked for G16
