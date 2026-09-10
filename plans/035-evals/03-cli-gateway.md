# 035/03 — CLI `gateway put --file` (BYOK, no secret flags)

**Date:** 2026-09-10 · Follows [01](01-rest-cli-mcp.md) §7 and the agent-credentials note.
**What this is:** how an agent/operator pastes fiat + Solana vault rows through `lazuar-pay`
without putting `sk_` / PEM in argv or MCP tool args.
**What this is not:** MCP `pay_put_gateway`. Not `--secret`. Not Pay-minted keys.

---

## Commands

```text
lazuar-pay gateway put --file ./secrets/stripe.json
lazuar-pay gateway get --provider stripe
lazuar-pay gateway list
```

`--file` is the only write door. Clap must not grow `--secret` / `--webhook-secret`.
Stdout is the host **view** (`configured`, `last4`, `environment`) — never the blob.

`PUT /v1/orgs/{orgId}/gateway` is the existing TypeSpec door. Client validates a few
honesty rules before send (test forbidden; Solana refuses API secrets); the host remains
the oracle for PEM / `key_id:key_secret` / cluster match.

Tests: clap has no secret flags; file PUT stripe + solana; GET never echoes `sk_`;
`provider=test` and Solana+secret fail before or at the host.
