# S10 — Strengthen `Zero_amount_session_is_ignored`

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.1  
**IDs:** H20  
**Goal:** Edit the existing method. Do not add a second.

---

## S10.1 After `Documents.Count == 0` add

- [ ] Body contains `ignored`
- [ ] Checkout `Status == open`

## S10.2 Must not

- [ ] Do not add `Zero_amount_session_is_ignored_keeps_checkout_open`

## S10.3 Exit

- [ ] Same method, stronger asserts, green
