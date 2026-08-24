# G10 — InMemory is not one-TX proof

**Track:** Prove Beat 1 · **Depends:** A00  
**Analysis:** `PayApiFactory` ignores `TransactionIgnoredWarning`; 015 H25 ticked without a test  
**IDs:** H12, H25  
**Goal:** Stop selling “same transaction” as a tested property of `task pay:test`.

---

## G10.1

- [ ] Comment on `PayApiFactory` **and** `WebhookTests` class: InMemory `BeginTransaction` is a no-op; H25 needs G11
- [ ] Do not tick H25 in 015 retroactively — 016 G12 is the proof

## G10.2 Must not

- [ ] Do not remove `BeginTransaction` from the handler
- [ ] Do not fake 5xx by SaveChanges-then-throw in production code

## G10.3 Exit

- [ ] Comments exist
- [ ] Unblocked for G11
