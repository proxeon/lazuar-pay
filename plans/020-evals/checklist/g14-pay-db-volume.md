# G14 — Named volume on pay-db

**Track:** G · **Depends:** K00  
**Analysis:** [`../06-host-production.md`](../06-host-production.md) §13.2.4  
**Goal:** Compose dogfood survives restart.

**Why:** `pay-db` has ports and healthcheck, **no** `volumes:`. `docker compose down` or recreate drops `lazuar_pay`. Fine for disposable laptop; a lie if someone charges a test card against it.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/docker-compose.pay.yml` | `pay-db` service, no volume |
| `apps/lazuar-pay/.env.example` | Host 5435 |
| Root `docker-compose.yml` | Hub museum — do not steal its volume as Pay’s |

**Current (`6d730d15`):** Ephemeral Postgres.

---

## G14.1

- [x] `docker-compose.pay.yml` `pay-db` has a named volume
- [x] Do not claim this is production backup
- [x] Comment: live cards need real Postgres + backup

## G14.2 Must not

- [x] Do not retarget Hub compose volumes onto 5435 as the Pay story

## G14.3 Exit

- [x] Unblocked for G15
