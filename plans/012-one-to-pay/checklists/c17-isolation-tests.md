# C17 — IsolationTests (still no cathedral)

**Track:** Whoami · **Depends:** C11 (code exists to scan)  
**Analysis:** [03](../03-pay-host-seams.md) IsolationTests widen  
**Goal:** New One client cannot smuggle Hub modules in.

---

## C17.1 Existing bans (must still pass)

- [ ] Host csproj does not contain `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`

## C17.2 Widen (from paper 03)

- [ ] Scan **test** csproj for the same cathedral strings
- [ ] Scan `apps/lazuar-pay/src/**/*.cs` for `MediatR`, `Modules.One`, `BuildingBlocks`
- [ ] Fail if any `.csproj` under `apps/lazuar-pay` references `apps/lazuar-api`

## C17.3 Do not ban

- [ ] Do not ban the word `One` (folder `One/` is allowed)
- [ ] Do not ban `HttpClient`

## C17.4 Exit

- [ ] IsolationTests green on the whoami tree
- [ ] Unblocked for C18
