# C11 — Named HttpClient to One (no routes)

**Track:** Whoami · **Depends:** C10  
**Analysis:** [03](../03-pay-host-seams.md)  
**Goal:** A typed client exists and is test-replaceable. **No `MapGet` whoami yet.**

---

## C11.1 Registration

- [x] `AddHttpClient("one")` (or typed client) with `BaseAddress` from `OneOptions`
- [x] Timeout from `OneOptions.TimeoutSeconds`
- [x] No retry policy that hammers `GET /me` (One `/me` can write)
- [x] No Polly “retry 3 times on 401”

## C11.2 Shape (fight C# gravity)

- [x] Client lives under `apps/lazuar-pay/src/Lazuar.Pay/` (e.g. `One/` folder of plain types)
- [x] **No** MediatR, **no** `IWhoamiQuery`, **no** extra `.csproj`
- [x] **No** `ProjectReference` to `apps/lazuar-api` or `packages/api-types-dotnet`

## C11.3 Test seam

- [x] HttpMessageHandler can be replaced from tests (typed client `ConfigurePrimaryHttpMessageHandler`, or `IHttpClientFactory` with a test handler)
- [x] Document the seam in a one-line comment on the registration, not a design doc

## C11.4 Exit

- [x] Solution builds
- [x] `task pay:test` still green
- [x] No new public HTTP routes
- [x] Unblocked for C12
