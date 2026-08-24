# H24 — Unique violation on webhook is 200 duplicate

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3  
**IDs:** NP-GW-006  
**Goal:** Two concurrent Stripe deliveries of the same event_id must not 500 or double-journal.

---

## H24.1 Live

- [ ] PK is already `(OrgId, Provider, EventId)` on `psp_webhook_events`
- [ ] Catch unique violation (Postgres `23505`; InMemory may throw differently)
- [ ] Return 200 `{ duplicate: true }`
- [ ] Do not fulfill on unique violation
- [ ] First winner fulfills once (H12)

## H24.2 Must not

- [ ] Do not 500 on duplicate
- [ ] Do not delete the unique index to “fix” races

## H24.3 Exit

- [ ] Comment names 23505
- [ ] Replay test still green (serial duplicate)
- [ ] Concurrent test optional if InMemory cannot provoke 23505
