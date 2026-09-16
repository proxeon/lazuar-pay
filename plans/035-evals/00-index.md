# 035 — Agent surfaces: REST, CLI, MCP

**Date:** 2026-09-10 · **Branch:** `feat/lazuar-pay-rs` · **Host:** `apps/lazuar-pay-rs`
(TypeSpec catalog is shared with .NET `apps/lazuar-pay` until Stage 6)

Triggered by: *expose the Rust backend as REST, CLI, and MCP so other AI agents can
connect. Today we run the backend + frontend and the human configures them in the
dashboard.* `/plans/` is gitignored on `main`; these notes stay on disk.

**Verdict in one line:** REST is already the product (`/v1` + `lzr_sk_`); CLI and MCP
must be thin HTTP clients of that same TypeSpec contract — not a second money API, not
in-process `apply()`, and not a replacement for the dashboard’s One-key + BYOK bootstrap.

| File | What |
|---|---|
| [01-rest-cli-mcp.md](01-rest-cli-mcp.md) | Evaluation: existing surface, adapter split, REST gaps, CLI, MCP tool list, bootstrap, order, risks |
| [02-cli.md](02-cli.md) | CLI implementation plan (`pay-client` + `lazuar-pay` binary) |
| [03-cli-gateway.md](03-cli-gateway.md) | `gateway put --file` / list / get — BYOK, no secret flags |

**Spec this sits on:** [032/13 identity](../032-hyperswitch-btcpay/13-identity.md),
[032/17 API surface](../032-hyperswitch-btcpay/17-api-surface.md),
[032/09 TypeSpec adapter](../032-hyperswitch-btcpay/09-typespec-adapter.md),
[032/19 capability crates](../032-hyperswitch-btcpay/19-capability-crates.md).
Catalog: `packages/pay-spec/main.tsp`. Sample hatch: `examples/pay-node`.

**What this is not:** an execution checklist (that would be a 036). Not x402 / Pay.sh
(README: not offered). Not permission to mint Pay-owned API keys or `Proof::Admin`.
Not remote Streamable-HTTP MCP in `serve`.

`.NET` `apps/lazuar-pay` stays production SoT until Stage 6. CLI/MCP can target `:8081`
on either host; TypeSpec is the contract.
