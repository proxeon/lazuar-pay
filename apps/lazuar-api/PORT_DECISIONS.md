# PORT_DECISIONS — lazuar-api (Rust port)

Every ambiguity encountered during the port is logged here with the chosen default
and the C# reference. Reviewed at phase gates (see `plans/023-evals/04-rust-port-spec.md`).

| # | Date | Decision | Rationale | C# reference |
|---|------|----------|-----------|--------------|
| D001 | 2026-09-03 | Sync Rust: `rouille` (thread-per-request), `postgres` + `r2d2` pool, no Tokio anywhere | User decision + session analysis: Pay's concurrency is modest; blocking world removes coloring/Send/drop-cancellation entirely | — |
| D002 | 2026-09-03 | Dev listen port `8095`; port `8081` is claimed only at human cutover | `.NET` service must keep running untouched on 8081 during the whole port | `Properties/launchSettings.json` |
| D003 | 2026-09-03 | Raw SQL via `postgres` crate, no ORM | The CAS transitions (`UPDATE … WHERE "Status" = …`), `FOR UPDATE SKIP LOCKED` claims, and filtered-unique-index semantics port verbatim in intent; an ORM would re-derive them | `Checkouts/CheckoutTransitions.cs`, `Webhooks/Outbound/OutboundWebhookDispatch.cs` |
| D004 | 2026-09-03 | Parity tests are fixtures-only (no live PSP sandboxes) | User decision | — |
| D005 | 2026-09-03 | `apps/lazuar-pay/**` is frozen: reference implementation, never edited during the port | User decision; G6 of the port spec | — |
| D006 | 2026-09-03 | Money = `rust_decimal::Decimal` everywhere; never `f64` | C# `decimal` parity; zero-decimal-currency incident (issue 003) came from the arithmetic layer | `Money/CurrencyValidation` |
| D007 | 2026-09-03 | Checkout status is writable only through `domain/checkouts/transitions.rs`; the module keeps the column's SQL private | Enforces the CAS invariant by module privacy — the Rust analogue of C# tracker hygiene | `Checkouts/CheckoutTransitions.cs:37-64` |
