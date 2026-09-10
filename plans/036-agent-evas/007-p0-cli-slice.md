# 036/007 — P0 CLI slice (implement)

**Date:** 2026-09-10 · Backlog [006](006-priority-backlog.md) items 1–9.

| # | How |
|---|---|
| 1 | clap `hide_env_values = true` on `--api-key` |
| 2 | `pay-rs` CI: `cargo test -p pay-client -p pay-cli` |
| 3 | `Config` / clap fallbacks: `PAY_API_KEY`, `PAY_ORG_ID`, `PAY_API_URL` |
| 4 | `checkout wait` polls `GET /v1/checkouts/{id}` (client method + CLI) |
| 5 | `POST /v1/orgs/{org}/refunds` via `refund create --checkout` |
| 6 | `POST /v1/payment-links` via `payment-link create` |
| 7 | `--idempotency-key` required on mint and refund |
| 8 | `Error::to_json` always on stderr |
| 9 | Client HTTP `gateway put` for chip/billplz/xendit/razorpay; CHIP PEM from rails fixture |
