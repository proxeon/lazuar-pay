# Ops.Contracts — intentionally hollow

Ops is an **agent / chat orchestration** module. Cross-module contracts are not needed today:

- Chat commands and repository ports live in `Application`.
- Domain types are Ops-local.
- No other module publishes or consumes Ops integration events.

The `Modules.Ops.Contracts` project exists for **layer symmetry** (same four-project skeleton as other modules) and so host/solution references stay uniform. Add `Commands/` / `Events/` only when another module must call or react to Ops.
