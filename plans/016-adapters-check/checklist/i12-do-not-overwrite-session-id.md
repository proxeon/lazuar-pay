# I12 — Do not overwrite ProviderSessionId on resume

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** P0-A; Razorpay join needs the first `plink_` (J11)  
**IDs:** —  
**Goal:** The first processor object id is the join key. A second start must not clobber it.

---

## I12.1

- [ ] Resume branch does not assign `row.ProviderSessionId`
- [ ] Resume branch does not assign `row.Provider` (already set)
- [ ] Resume branch does not `SaveChanges` unless payer name/email were updated **and** you still do not touch session id
- [ ] If you persist payer fields on resume, that is allowed; session id and redirect URL stay

## I12.2 Must not

- [ ] Do not clear `ProviderSessionId` when email is re-posted
- [ ] Do not switch `row.Provider` on resume even if `active_provider` changed (Y10 owns webhook bind)

## I12.3 Exit

- [ ] After two starts, `ProviderSessionId` equals the first create’s id
- [ ] Unblocked for J11 and G14
