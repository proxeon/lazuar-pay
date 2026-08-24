# S17 — One EF migration for S10–S15

**Track:** Schema · **Depends:** S10, S11, S12, S13, S14, S15  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** —  
**Goal:** One migrator, public schema, `task pay:db:migrate`.

---

## S17.1

- [ ] One new migration on existing `PayDbContext` covering:
  - `gateway_credentials.webhook_ciphertext`
  - `gateway_credentials.public_merchant_id`
  - `gateway_credentials.environment`
  - `org_settings.active_provider`
  - `checkouts.provider`
  - `checkouts.provider_session_id`
- [ ] Default schema remains `public`
- [ ] `task pay:db:migrate` applies it (`dotnet ef database update --context PayDbContext`)
- [ ] `PayApiFactory` InMemory `EnsureCreated` still works for tests

## S17.2 Must not

- [ ] Do not add a second DbContext
- [ ] Do not create `organizations` / `users` / `members` tables
- [ ] Do not hand-edit Hub `payments` schema
- [ ] Do not drop `sst_registered` in this migration (T15: leave the column)

## S17.3 Exit

- [ ] Migration file under `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/`
- [ ] Snapshot updated
- [ ] Unblocked for H10 / P11
