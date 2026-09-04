# lazuar-api (sync Rust port)

Sync Rust port of `apps/lazuar-pay`. The C# service stays the production process
until cutover (`plans/027-checklist/00-index.md` Phase 8).

**Single replica.** In-process limiter, start/fulfill gates, and whoami cache.
Scaling out silently disables issue 016 rate limiting. Workers (outbound
dispatch, Solana watcher, pending-refund logger) live in this process — do not
split them out for v1.

Rust **does not migrate** the Pay schema. Schema owner is C# until cutover.
Apply the C# EF migrations (or `migrations/0001_reference_schema.sql`) before
boot. `/ready` fails closed if `checkouts` / `org_settings` are missing.
`HEALTHCHECK` curls `/ready`, not `/health`.

## Run

```sh
cd apps/lazuar-api
cp .env.example .env            # then fill secrets
cargo test                       # real Postgres per test (PAY_TEST_POSTGRES or localhost:5435)
ConnectionStrings__Pay='host=localhost port=5435 user=postgres password=postgres dbname=lazuar_pay' \
LISTEN_ADDR=127.0.0.1:8095 cargo run
```

Port `8081` belongs to the C# service until cutover; this host defaults to `8095`.

See `.env.example` for every `Config::from_env` key. Do **not** use
`Pay__ConnectionString` — the C# key is `ConnectionStrings__Pay`.

Side-by-side with C# on the same `pay-db`:

```sh
docker compose -f apps/lazuar-pay/docker-compose.pay.yml --profile rust up -d --build
# C# :8081 (`--profile apps`), Rust :8095 (`--profile rust`). Do not scale pay-api.
```

Image: `docker buildx bake lazuar-pay-api` (alias `pay-api`). Do not delete the
C# `lazuar-pay` image until Phase 8 + 30 days.

## Observability

Production/Staging request logs are one JSON object per line: `method`, `route`
pattern, `status`, `org`, `request_id`, `duration_ms`. Set `RUST_LOG=info`.
Webhook dispatch failures log at error. Stale pending refunds log at warn.
Solana watcher errors log at error. Prometheus is later — not a cutover
blocker if those lines are greppable.

## Residual risk (inherited C# holes)

| Hole | Production decision |
|---|---|
| Subscriptions never recur (004) | **A (keep).** Accept `mo`/`yr`, mint `incomplete`, never rebill. Do not 400 `interval`. |
| `mail_outbox` has no writer | Do not add a writer. Leave the table alone. |
| No in-repo backups | Out of crate scope. Cutover runbook must name the Postgres backup (Neon PITR / `pg_dump`). **Do not cut over without that name.** |
| No paging / alerting | Closed-beta: read logs. Unattended: pending-refund age + dispatch error rate must page. |
| Single replica | Keep. In-process limiter/gates/cache. Put it in the runbook. |
| No live PSP sandbox in CI | Keep D004. Manual dogfood per rail before inviting strangers. |

`examples/pay-node` mints a checkout and verifies Pay’s outbound HMAC. Against
Rust: `PAY_API_URL=http://localhost:8095`. The host keeps 300s timestamp skew;
the sample does not enforce it.

## Cutover

Human-gated. See [`CUTOVER.md`](CUTOVER.md). Do not bind this process to `:8081`
until every pre-flight box there is signed. Rollback is stop Rust, start C# on
the same database and WrapKey.

```sh
bash scripts/pay-cutover-preflight.sh
```
