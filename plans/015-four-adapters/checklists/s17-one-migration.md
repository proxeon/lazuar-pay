# S17 — One EF migration for S10–S15

**Track:** Schema · **Depends:** S10, S11, S12, S13, S14, S15  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** —  
**Goal:** One migrator, public schema, `task pay:db:migrate`.

---

## S17.1

- [x] One new migration on existing `PayDbContext` covering:
  - `gateway_credentials.webhook_ciphertext`
  - `gateway_credentials.public_merchant_id`
  - `gateway_credentials.environment`
  - `org_settings.active_provider`
  - `checkouts.provider`
  - `checkouts.provider_session_id`
- [x] Default schema remains `public`
- [x] `task pay:db:migrate` applies it (`dotnet ef database update --context PayDbContext`)
- [x] `PayApiFactory` InMemory `EnsureCreated` still works for tests

## S17.2 Must not

- [x] Do not add a second DbContext
- [x] Do not create `organizations` / `users` / `members` tables
- [x] Do not hand-edit Hub `payments` schema
- [x] Do not drop `sst_registered` in this migration (T15: leave the column)

## S17.3 Exit

- [x] Migration file under `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/`
- [x] Snapshot updated
- [x] Unblocked for H10 / P11
