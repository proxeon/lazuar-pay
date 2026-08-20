# 012 connect — locked decisions

**Filled by:** [c00-align-freeze.md](./c00-align-freeze.md)  
**Evidence:** [`../01`](../01-one-http-surface.md)–[`../10`](../10-dogfood-and-tests.md)  
**Do not change a row without amending C00.**

| Topic | Lock |
|-------|------|
| Host | `apps/lazuar-pay` only. Not `apps/lazuar-api`. Not a Go tree in this program. |
| Listen | **8081**. Never 8080. |
| One API | `http://localhost:8080/api/v1` locally. Env `One:BaseUrl` (no trailing slash on host; client appends `/me`). |
| First door | `GET /v1/whoami` on Pay. Forwards `Authorization` to One `GET /me`. |
| Dummy admin | `GET /v1/orgs/{orgId}/ready`. One `POST /tenants/{orgId}/authz/check` with `relation=member`, `object.type=tenant`, `object.id={orgId}`. |
| JSON | snake_case. Pay whoami is a **projection**, not a clone of One `MeResponse` and not Hub `AuthUser`. |
| Whoami body (minimum) | `user_id`, `email`, `is_platform_admin`, `active_org_id` (One `active_tenant_id`), `tenants[]` of `{ id, slug, name, role, status }` where `id` **is** `org_id`. |
| Auth | Caller Bearer only (`access_token` or later `lzr_sk_`). No Pay password, no cookie JWT, no `id_token` as Bearer. |
| `/me` usage | **Endpoint only.** Not global middleware. Not on `/health`. One `/me` can write (JIT); do not hammer. |
| Path vs header | `{orgId}` in path is SoT. `X-Lazuar-Tenant-Id` is a hint Pay may forward to One; it must not authorize. |
| Tests | `WebApplicationFactory` + fake `HttpMessageHandler`. `task pay:test` does not boot One. |
| TypeSpec | `packages/pay-spec` only. Do not hook `task gen` / old honesty allowlist. |
| One repo | **No product change** in C-phases. |
| Old UIs | `lazuar-ops` / `lazuar-portal` stay on Hub 8080. Do not set `VITE_API_URL` to 8081. |
| VIEWER | One membership is `owner` \| `admin` \| `member`. Do not implement NP-ONE-021 as `check(member)`. |
| Secrets | Pay never holds Zitadel PAT, login PAT, or OpenFGA admin. C-phases: BaseUrl + Timeout only. |
| Cathedral | No `ProjectReference` / MediatR / `Modules.*` / `BuildingBlocks` / `Lazuar.Api`. Stay off `Lazuar.slnx`. |
