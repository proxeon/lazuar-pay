# C11 — Named HttpClient to One (no routes)

**Track:** Whoami · **Depends:** C10  
**Analysis:** [03](../03-pay-host-seams.md)  
**Goal:** A typed client exists and is test-replaceable. **No `MapGet` whoami yet.**

---

## C11.1 Registration

- [ ] `AddHttpClient("one")` (or typed client) with `BaseAddress` from `OneOptions`
- [ ] Timeout from `OneOptions.TimeoutSeconds`
- [ ] No retry policy that hammers `GET /me` (One `/me` can write)
- [ ] No Polly “retry 3 times on 401”

## C11.2 Shape (fight C# gravity)

- [ ] Client lives under `apps/lazuar-pay/src/Lazuar.Pay/` (e.g. `One/` folder of plain types)
- [ ] **No** MediatR, **no** `IWhoamiQuery`, **no** extra `.csproj`
- [ ] **No** `ProjectReference` to `apps/lazuar-api` or `packages/api-types-dotnet`

## C11.3 Test seam

- [ ] HttpMessageHandler can be replaced from tests (typed client `ConfigurePrimaryHttpMessageHandler`, or `IHttpClientFactory` with a test handler)
- [ ] Document the seam in a one-line comment on the registration, not a design doc

## C11.4 Exit

- [ ] Solution builds
- [ ] `task pay:test` still green
- [ ] No new public HTTP routes
- [ ] Unblocked for C12
