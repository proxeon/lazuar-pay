# Parked — Hub cutover (paper 02 phases B–D)

**Do not start until B99.**  
**Analysis:** [02](../02-replace-old-cutover.md)

---

- [ ] Phase B: staging **new stack only** (no Hub containers on that DNS)
- [ ] Phase C: production DNS / integrator docs print Pay `/v1`, not `hub.lazuar.com` money
- [ ] Phase D: Hub processes stopped; then delete `lazuar-ops` / portal / api when grep is allowed to be empty
- [ ] Never strangler: Caddy `/api` → Pay while `/` still ops
- [ ] Never both APIs on host 8080
- [ ] Rollback plan remains: Hub on, Pay off (or Pay still on 8081 for engineering), One keeps 8080 on dogfood laptops
