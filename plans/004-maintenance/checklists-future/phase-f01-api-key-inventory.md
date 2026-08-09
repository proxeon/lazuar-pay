# F01 — API key inventory (FW-1 prep)

**Goal:** Know how many legacy LHDN keys exist and whether cutover can accelerate.  
**Depends on:** F00 keys track selected  
**Design:** `../api-key-cutover-design.md`

---

## F01.1 Code inventory (refresh)

- [ ] Confirm dual-read still in `ApiKeyAuthenticationMiddleware` (One then Lhdn)
- [ ] Confirm mint paths write only One
- [ ] Confirm dual revoke subscriptions still present
- [ ] List Lhdn key-related endpoints (façades only?)

## F01.2 Data inventory

- [ ] SQL count active `lhdn."DeveloperApiKeys"` — staging: ________
- [ ] SQL count active `lhdn."DeveloperApiKeys"` — prod: ________
- [ ] Count hashes already present in `one."ApiCredentials"`: ________
- [ ] Sample non-migratable candidates (unknown scopes, bad org): ________

## F01.3 Decision

- [ ] If prod active legacy = 0: note “accelerate cutover” (F02 may be no-op migrate)
- [ ] If prod active legacy > 0: keep calendar; plan F02 migrator capacity
- [ ] Write go / no-go for F02

## F01.4 Exit

- [ ] Counts recorded in `../api-key-cutover-design.md` or phase-f01-done note
- [ ] No middleware change in this phase (inventory only)
