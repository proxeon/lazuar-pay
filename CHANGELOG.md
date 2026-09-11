# Changelog

All notable changes to Lazuar-Pay are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
once a first tag exists.

## [Unreleased]

Rust payment host, HTTP client, `lazuar-pay` CLI, and `lazuar-pay-mcp` on
`feat/pay-rs-cli`. Production money of record remains .NET `apps/lazuar-pay`
until strangler Stage 6. CLI and MCP are HTTP clients of TypeSpec `/v1` only;
they never call `storage::apply`, mint `lzr_sk_`, or expose buyer
`/v1/pay/{token}/start|confirm`.

### Added

#### Rust host (`apps/lazuar-pay-rs`)

- New engine (Payment / Attempt / Settlement, `propose()`, schema `pay_rs`):
  domain fold, apply TX, rails (test, Stripe, CHIP, Billplz, Xendit, Razorpay,
  Solana receive-only USDC), SKIP LOCKED workers, TypeSpec `/v1` adapter.
- Official document numbers from `document_sequences` (Malaysia year, UTC+8):
  Take settle allocates `RCPT-{year}-{n}`; refund settle allocates `REF-{year}-{n}`.
- `GET /v1/orgs/{org}/events?after=` — Plane C delivery cursor (newer than
  `event_id`, oldest first). Now in pay-spec; .NET Map* at cutover.
- Dockerfile and compose overlay on `pay-db` (`:8081`). Does not replace .NET `pay`.

#### CLI (`lazuar-pay`, crate `pay-cli`)

HTTP client of focused Pay `:8081`. Canonical env `LAZUAR_PAY_*`; pay-node
aliases `PAY_API_KEY` / `PAY_ORG_ID` / `PAY_API_URL` also work.

- `whoami`, `ready`
- `checkout create|get|list|wait` — mint requires `--idempotency-key`; wait polls
  GET until `paid|failed|expired|open`; `--success-url` / `--cancel-url` /
  `--product-id`; `-p` / `-a` short flags
- `payment-link create|list` — occupancy mint; `--product-id`; `-p` / `-a`
- `product create|list` — MYR one-off catalog
- `payments list`, `receipts list|get` — `--limit` / `--after`
- `refund create|list|resolve` — create requires `--idempotency-key`; resolve is
  an ops hatch (`--status succeeded|failed`), not an MCP tool
- `gateway put --file|get|list` — BYOK vault; no `--secret` flags
- `webhook put --url|get|rotate|test` — Plane C org webhook
- `one-webhook put --file|get` — One inbound HMAC secret
- `events list --after`, `listen --forward-to` — listen is Testing loopback
  `http://127.0.0.1` / `localhost` only; polls events and POSTs envelopes
- `subscription list` — honest empty (`items: []`; recurring is not offered)
- `--compact` / `--quiet` / `--table`; `--config` / `--profile`
  (`~/.config/lazuar-pay/config.json`; file must not contain `api_key`)
- Default mint currency: `--currency`, else `LAZUAR_PAY_CURRENCY`, else
  `PAY_CURRENCY`, else MYR (product create stays MYR)
- Redacted gateway example JSON under `apps/lazuar-pay-rs/examples/gateway/`
- Root README documents the CLI and `PAY_*` vs `LAZUAR_PAY_*`

#### MCP (`lazuar-pay-mcp`, crate `pay-mcp`)

stdio JSON-RPC (Content-Length). Ten tools; secrets stay in env. No
`pay_put_gateway`. Idempotency required on mint and refund.

- `pay_whoami`, `pay_ready`
- `pay_create_checkout`, `pay_get_checkout`
- `pay_wait_checkout` — poll GET until `paid|failed|expired|open` (not buyer start);
  terminal mismatch is 409
- `pay_list_events` — Plane C cursor (`after` = event_id, newer, oldest first)
- `pay_list_payments`, `pay_list_receipts`
- `pay_create_refund`, `pay_create_payment_link`

### Changed

- Client `validate_gateway_put` matches the host: CHIP PEM (RSA ≥2048), Billplz
  `environment` required, Razorpay `key_id:key_secret`, Solana on-curve address
  and `devnet|mainnet`.
- `payments` is a subcommand (`payments list`) to match `receipts list`.
- GitHub `pay-rs` job runs `cargo test -p pay-client -p pay-cli` and
  `cargo test -p pay-mcp`; clippy `-D warnings` on those crates; `pay` job runs
  OpenAPI ↔ Rust axum honesty (`scripts/check-pay-rs-openapi-honesty.mjs`).
- `checkout wait` / `pay_wait_checkout` return 409 when status is already
  terminal and does not match `--until` (no long timeout).

### Security

- `--help` uses clap `hide_env_values` on `--api-key` (does not print the key).
- Warn when `--api-key` or `--api-key=` is on argv (`ps` / shell history);
  `--quiet` silences the hint. Prefer `LAZUAR_PAY_API_KEY` / `PAY_API_KEY`.
- Authorization must be `lzr_sk_…` or Testing `test-writer` / `test-member`.
  Stripe/Hub `sk_` and SPA JWTs are rejected locally.
- Gateway and One-webhook `--file` must not be group/world-readable (Unix
  `chmod 600`). Errors name the path, never file bytes.
- Gateway stdout is allowlisted (no `secret` / PEM). `Cli` Debug prints `***`
  for `api_key`.
- `listen --forward-to` is loopback http only: `127.0.0.1`, `localhost`, and
  `[::1]`. Userinfo forms such as `http://127.0.0.1:80@evil.example/hook` are
  rejected.

### Fixed

- `--api-key=value` (clap equals form) now triggers the env-only hint.
- Receipts/payments `--after` is always sent in tests (host `next_cursor` is
  null on a one-row page).
- Mint `--currency` from `PAY_CURRENCY` is resolved per invocation (clap
  `default_value_t` was baked on the first parse in the process).
- `solana` + default MYR fails closed as config (`USDC` required), not a host 400.
- Billplz: public GET retrieves the bill if the callback never arrives (success
  URL is still not paid). `Pay__LiveHttp=1` also starts PSync even with `--api-only`.

[Unreleased]: https://github.com/proxeon/lazuar-pay/compare/main...feat/lazuar-pay-rs
