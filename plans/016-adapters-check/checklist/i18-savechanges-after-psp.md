# I18 — Persist-after-PSP (document, do not invert)

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) P1-10  
**IDs:** —  
**Goal:** First start still talks to the PSP then saves. Do not “fix” by saving a fake URL first.

---

## I18.1 Live today

- [ ] Order: HTTP to PSP → mutate row → `SaveChanges` → return URL
- [ ] If SaveChanges throws: processor has an unpaid session, buyer gets 500, retry would have created another — I10 now returns nothing stored, so retry **will** create another (same family as P0-A, rare)

## I18.2 This phase

- [ ] Leave order as PSP-then-save
- [ ] Add a 5-line comment on `Start` that a SaveChanges failure after PSP create is accepted and retry may mint a second session
- [ ] Do **not** insert a placeholder `PspRedirectUrl` before HTTP

## I18.3 Must not

- [ ] Do not add an outbox / repair worker
- [ ] Do not ACK 200 before SaveChanges

## I18.4 Exit

- [ ] Comment exists
- [ ] I10 still holds for the common double-click
