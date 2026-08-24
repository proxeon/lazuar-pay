# H24 — Unique violation on webhook is 200 duplicate

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3  
**IDs:** NP-GW-006  
**Goal:** Two concurrent Stripe deliveries of the same event_id must not 500 or double-journal.

---

## H24.1 Live

- [x] PK is already `(OrgId, Provider, EventId)` on `psp_webhook_events`
- [x] Catch unique violation (Postgres `23505`; InMemory may throw differently)
- [x] Return 200 `{ duplicate: true }`
- [x] Do not fulfill on unique violation
- [x] First winner fulfills once (H12)

## H24.2 Must not

- [x] Do not 500 on duplicate
- [x] Do not delete the unique index to “fix” races

## H24.3 Exit

- [x] Comment names 23505
- [x] Replay test still green (serial duplicate)
- [x] Concurrent test optional if InMemory cannot provoke 23505
