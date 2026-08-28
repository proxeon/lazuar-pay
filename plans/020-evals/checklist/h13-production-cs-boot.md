# H13 — Production empty Pay CS fails boot

**Track:** H · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.2  
**Goal:** No silent laptop connection string.

**Why:** Empty `ConnectionStrings:Pay` outside Testing becomes `Host=localhost;Port=5435;…` in `Program.cs`. A Production container without env still talks to the operator’s laptop.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | Lines 49–53 hardcoded fallback |
| `apps/lazuar-pay/.env.example` | CS example |
| `apps/lazuar-pay/docker-compose.pay.yml` | `ConnectionStrings__Pay` to `pay-db` |

**Current (`6d730d15`):** Fallback always if empty, any non-Testing env.

---

## H13.1

- [ ] Outside Development/Testing: empty `ConnectionStrings:Pay` → fail boot
- [ ] Remove or stop using the hardcoded `localhost:5435` fallback in **Production**
- [ ] Development may keep laptop default

## H13.2 Tests

- [ ] Production factory without CS fails start (supply WrapKey/CORS so this is the only failure)

## H13.3 Exit

- [ ] Unblocked for H14
