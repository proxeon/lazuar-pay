# S15 — Strengthen Xendit PAID+SETTLED

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.6  
**IDs:** X16  
**Goal:** Edit `Xendit_paid_and_settled`. Replay of PAID is **fx15**, not this method.

---

## S15.1 After PAID

- [ ] `RCPT-`, debit=credit, checkout `paid`

## S15.2 After SETTLED

- [ ] Body contains `settled` or `ignored`
- [ ] Still one document

## S15.3 Must not

- [ ] Do not treat SETTLED-after-PAID as PAID replay (different EventId)

## S15.4 Exit

- [ ] Green
- [ ] Unblocked for fx15
