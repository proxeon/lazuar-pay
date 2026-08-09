# R43 — Retire LHDN fire-and-forget sender

**Track:** Webhooks · **Depends on:** R42 verified in staging (and prod cutover plan)  
**Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`

---

## R43.1 Stop dual delivery

- [ ] Remove calls to `WebhookSenderService` / `DispatchExternalWebhookCommand` for migrated events
- [ ] Ensure only One path remains

## R43.2 Cleanup

- [ ] Delete or gut unused service
- [ ] Metrics: re-tag or remove pure Lhdn failure counter if obsolete
- [ ] Optional later: drop Lhdn webhook subscription table after façade period

## R43.3 Docs

- [ ] Lhdn README freeze section removed; One path documented
- [ ] Integrator changelog if signing/payload broke
- [ ] FUTURE-WORK FW-2 done

## R43.4 Exit

- [ ] No customer LHDN webhook uses fire-and-forget
