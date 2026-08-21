# D17 — `checkouts` table

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Replace `CheckoutStore` memory `_byId`. Not a ledger.

---

## D17.1 Table

- [x] Table `checkouts` in the D14 schema
- [x] Columns at least: `id`, `org_id`, `amount`, `currency`, `status`, `success_url`, `cancel_url`, `created_at`, `public_token` (nullable until K10)
- [x] `org_id` is One tenant id (copy of the uuid/text). No FK to `organizations`
- [x] `status` still **`open`** until F11 — do not add paid/expire here
- [x] Do not port Hub `commerce.CheckoutSessions` / `payments.IntegrationCheckoutSessions`

## D17.2 Store

- [x] Replace `_byId` `ConcurrentDictionary` with this table (concrete class, not MediatR)
- [x] Class comment stays honest: **not a ledger**
- [x] DI: not a process-local dictionary of sessions

## D17.3 Tests

- [x] Existing `CheckoutTests` pass against a **test double** or **Testcontainers**
- [x] If Testcontainers: add the package on **Pay test csproj only**. Do **not** import Hub `Directory.Packages.props`
- [x] IsolationTests still ban MediatR

## D17.4 Exit

- [x] Create/get still 201/200; restart no longer wipes `_byId`
- [x] Unblocked for D18
