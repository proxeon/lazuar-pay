# W10 — `webhook_endpoints` table

**Track:** W · **Depends:** K00  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.2  
**Goal:** One active Plane C endpoint per org. Not `mail_outbox`. Not Plane A/B tables.

**Why:** Pay has nowhere to store “POST this URL when we fulfill.” `mail_outbox` is unused leftover. Plane A (`one_webhook_events`) and Plane B (`psp_webhook_events`) are inbound. Mixing secrets is how Hub confused three `whsec_` families.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `OneWebhookEventRow`, `PspWebhookEventRow`, `MailOutboxRow` |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | Sets + `OnModelCreating` |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/` | Last: `20260828093000_OrgOneWebhookCiphertext` |
| `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` | Wrap Plane C secret like vault |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | Plane A — do not reuse |

**Current (`6d730d15`):** Grep of outbound dispatcher / `webhook_endpoints` in focused Pay is empty.

---

## W10.1 Schema

- [x] Table `webhook_endpoints` public schema
- [x] Columns: `Id`, `OrgId`, `Url`, `SecretCiphertext`, `SecretPrefix` (e.g. last 4), `Status` (`active`/`disabled`), `EnabledEvents` (empty = catalog default), `CreatedAt`, `UpdatedAt`
- [x] Unique: one **active** row per `OrgId` (filter unique or replace-in-place singular)
- [x] EF migration + snapshot
- [x] `DbSet` on `PayDbContext`

## W10.2 Must not

- [x] Do not reuse `one_webhook_events` or `psp_webhook_events`
- [x] Do not reuse `mail_outbox`
- [x] Do not store plaintext secret

## W10.3 Exit

- [x] Unblocked for W11
