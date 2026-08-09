# R23 — Billing signed PDF honesty

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`

---

## R23.1 Decision

- [x] Final signed PDF is public/admin API surface? yes/no: **no**
- [x] If yes: add to TypeSpec billing routes/models *(N/A)*
- [x] If no: allowlist as internal + document; ensure not advertised in product OpenAPI

## R23.2 Implement

- [x] TSP + gen **or** allowlist entry for R25 *(allowlist: `packages/api-spec/honesty-allowlist.yaml`)*
- [x] Endpoint uses generated types if exposed *(N/A — not product-exposed; 302 redirect)*

## R23.3 Exit

- [x] Decision implemented and documented *(see `../r23-notes.md`)*
