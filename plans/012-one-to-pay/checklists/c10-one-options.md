# C10 — OneOptions (config only)

**Track:** Whoami · **Depends:** C00  
**Analysis:** [03 §5](../03-pay-host-seams.md), [05](../05-local-topology.md)  
**Goal:** Pay can name where One lives. **No HTTP calls yet.**

---

## C10.1 Bind options

- [ ] Add `One` section: `BaseUrl` (string), `TimeoutSeconds` (int, default e.g. 5)
- [ ] Local default `BaseUrl` = `http://localhost:8080/api/v1` (or host `http://localhost:8080` + path `/api/v1` — pick one in code and document it in README; do not mix)
- [ ] Do **not** add `ClientId`, `PAT`, `ApiKey`, OpenFGA, or Zitadel authority in this phase
- [ ] Bind via `IOptions<OneOptions>` (or equivalent) in the **same** host project — no extra class library

## C10.2 Files

- [ ] `appsettings.json` has the section
- [ ] `appsettings.Development.json` does not put secrets
- [ ] `.env.example` at `apps/lazuar-pay/` **or** README env table — `One__BaseUrl` documented
- [ ] Never commit a real `lzr_sk_` or PAT

## C10.3 Listen URL

- [ ] Confirm `launchSettings.json` still `http://localhost:8081`
- [ ] Do not set `ASPNETCORE_URLS` to 8080

## C10.4 Exit

- [ ] `task pay:test` still green (health + isolation only)
- [ ] IsolationTests still pass (no cathedral strings)
- [ ] Unblocked for C11
