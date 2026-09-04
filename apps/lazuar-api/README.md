# lazuar-api (sync Rust port)

Sync Rust port of `apps/lazuar-pay`. The C# service stays the production process
until cutover (`plans/027-checklist/00-index.md`).

**Single replica.** In-process limiter, start/fulfill gates, and whoami cache.
Scaling out silently disables issue 016 rate limiting. Workers live in this process.

Rust **does not migrate** the Pay schema. Apply the C# EF migrations (or
`migrations/0001_reference_schema.sql`) before boot. `/ready` fails if
`checkouts` / `org_settings` are missing.

## Run

```sh
cd apps/lazuar-api
cargo test                       # real Postgres per test (PAY_TEST_POSTGRES or localhost:5435)
ConnectionStrings__Pay='host=localhost port=5435 user=postgres password=postgres dbname=lazuar_pay' \
Pay__WrapKey='<32-byte base64>' \
LISTEN_ADDR=127.0.0.1:8095 cargo run
```

Port `8081` belongs to the C# service until cutover; this host defaults to `8095`.

See `.env.example` for every `Config::from_env` key. Do **not** use
`Pay__ConnectionString` — the C# key is `ConnectionStrings__Pay`.

## Honest capability (inherited from C#)

- Subscriptions: `interval` `mo`/`yr` is accepted and mints an `incomplete` row.
  Nothing rebills (issue 004). Same as C#.
- `mail_outbox` has no writer. Leave the table alone.
- Rails without a refund API (Billplz/Xendit/Razorpay/Solana) leave late-pay
  rows `pending`. Never fake `succeeded`.

## Cutover

Follow `plans/027-checklist/00-index.md` Phase 8. Rollback is stop Rust, start
C# on the same database and WrapKey.
