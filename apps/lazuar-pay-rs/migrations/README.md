# pay_rs migrations

Empty until 032/08. sqlx migrate, one folder forever.

- Schema name: `pay_rs` (same Postgres instance as .NET `public`).
- snake_case, unquoted identifiers.
- Amounts: `amount_minor bigint` + `currency text` + `exponent smallint`.
- No `INSERT` into `public.checkouts`. No backfill of Open sessions.
