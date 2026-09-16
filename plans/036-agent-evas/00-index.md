# 036 — Five-agent review of `lazuar-pay` CLI

**Date:** 2026-09-10 · **Branch:** `feat/pay-rs-cli` · **Target:** `apps/lazuar-pay-rs/crates/{cli,client}`

Five independent explore passes after 035/02–03 (HTTP CLI + `gateway put --file`).
Not an execution checklist. `/plans/` is gitignored on `main`; these notes stay on disk.

**Verdict in one line:** mint / poll / list / BYOK-file work; the CLI is not yet a Stripe-CLI-like
or MCP-ready agent tool — missing wait/refund/payment-link, `PAY_*` env aliases, CI test job,
and `--help` leaking `LAZUAR_PAY_API_KEY`.

| File | Lens | One-line |
|---|---|---|
| [001-flags-and-ux.md](001-flags-and-ux.md) | Flags, clap, env, exit codes, output | Env names diverge from `pay-node`; pretty-JSON only; `payments` vs `receipts list` |
| [002-rest-dashboard-parity.md](002-rest-dashboard-parity.md) | TypeSpec + merchant SPA vs commands | Next slice: `payment-link create` + `refund create`; poll already exists |
| [003-honesty-and-secrets.md](003-honesty-and-secrets.md) | Vault, argv, validation vs host | `--file` is good; `--help` can print the API key; client PEM/env checks thin |
| [004-tests-docs-ci.md](004-tests-docs-ci.md) | Tests, Taskfile, GitHub Actions | `pay-rs` CI never runs `cargo test -p pay-cli`; CHIP/Xendit/… file PUT untested |
| [005-agent-readiness.md](005-agent-readiness.md) | Other AI agents / MCP | Can mint; cannot wait-for-paid or refund as a product; no MCP crate |
| [006-priority-backlog.md](006-priority-backlog.md) | Merged P0–P2 list + slices | What to implement, in order; locks we will not build |
| [007-p0-cli-slice.md](007-p0-cli-slice.md) | How P0 #1–9 land in crates | hide_env, CI, PAY_*, wait, refund, payment-link, idempotency, error JSON, rail file tests |

Predecessor: [035-evals](../035-evals/00-index.md).
