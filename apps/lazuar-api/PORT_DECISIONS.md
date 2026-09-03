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
| D008 | 2026-09-03 | No InMemory provider: every test runs on real Postgres in a unique per-test database | The C# suite's 96%-InMemory coverage was its weakest point (plans/023-evals/03); the port does not inherit it | `tests/Lazuar.Pay.Tests/Infrastructure/PayPostgres.cs` (Assert.Ignore pattern) |
| D009 | 2026-09-03 | Outbound HTTP = `ureq` (sync); non-2xx statuses are responses, not transport errors | Rails and the dispatcher branch on status codes; a 5xx-after-send must reach them as the ambiguous case (issue 001) | `Program.cs` named HttpClients |
| D010 | 2026-09-03 | Webhook envelope `created_at` is RFC 3339 UTC | .NET default serializer emits the same ISO-8601 instant with numeric offset; consumers parse both | `PayWebhookEnvelope.Serialize` |
| D011 | 2026-09-03 | JSON amounts are true numbers via `serde_json/arbitrary_precision` + `rust_decimal` | .NET System.Text.Json emits `decimal` as a JSON number; string-encoded amounts would break wire parity | `Results.Json(View(...))` |
