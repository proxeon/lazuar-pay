# pay_rs migrations

sqlx migrate, one folder forever. First file: `20260906120000_pay_rs_init.sql`.

- Schema name: `pay_rs` (same Postgres instance as .NET `public`).
- snake_case, unquoted identifiers.
- `tenant_id text` (tests use `"t1"`).
- Amounts: `amount_minor bigint` + `currency text` + `exponent smallint`.
- No `INSERT` into `public.checkouts`. No backfill of Open sessions.
- Terminal copy (`paid`/`failed`/`expired`) is `lazuar-pay-rs backfill`, not a migration.
- No `apply.rs` in this folder. Uniques in the init migration *are* the money locks.

```sh
cargo test -p storage   # needs Docker (Postgres 16)
```
