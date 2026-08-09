# R43 — Retire LHDN fire-and-forget sender

**Track:** Webhooks · **Depends on:** R42 verified in staging (and prod cutover plan)  
**Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Notes:** `../r43-notes.md`

---

## R43.1 Stop dual delivery

- [x] Remove calls to `WebhookSenderService` for migrated events (R42 already; R43 deletes service)
- [x] Keep `DispatchExternalWebhookCommand` as pure One publish path (not removed)
- [x] Ensure only One path remains

## R43.2 Cleanup

- [x] Delete unused service + interface (`WebhookSenderService`, `IWebhookSenderService`)
- [x] Metrics: no pure Lhdn `RecordWebhookFailed("lhdn")` call sites (method retained for `outbound` / `payment`)
- [x] Optional later: drop Lhdn webhook subscription table after façade period — **not this pass** (table kept)

## R43.3 Docs

- [x] Lhdn README freeze section removed; One path documented
- [x] Integrator changelog if signing/payload broke — covered in R40/R42 notes + module READMEs (breaking vs legacy HMAC)
- [x] FUTURE-WORK FW-2 done

## R43.4 Exit

- [x] No customer LHDN webhook uses fire-and-forget
- [ ] Staging/prod ops confirmation (shared with R42.4) when ready
