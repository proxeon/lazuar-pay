# S16 — Strengthen Razorpay captured RCPT + balance

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.7; count==2 is necessary not sufficient  
**IDs:** R21  
**Goal:** Edit `Razorpay_captured`. Replay is fr15.

---

## S16.1 Add

- [ ] `Number` starts with `RCPT-`
- [ ] Debit sum == credit sum (not only `JournalLines.Count() == 2`)
- [ ] Keep tax/fee in fixture

## S16.2 Must not

- [ ] Do not book tax/fee to make D==C of net amount

## S16.3 Exit

- [ ] Green
