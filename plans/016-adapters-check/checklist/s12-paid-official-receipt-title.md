# S12 — Strengthen Stripe paid: title, paid, SST null

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.3; T16 over-claim  
**IDs:** T16  
**Goal:** Edit `Completed_session_writes_receipt_and_replay_is_noop`.

---

## S12.1 Add

- [ ] `Documents.Single().Title == "Official Receipt"`
- [ ] Checkout `Status == paid`
- [ ] First response does **not** contain `SST registration unknown`
- [ ] After PUT/seed, `OrgSettings.Single().SstRegistered` is null

## S12.2 Must not

- [ ] Do not add SST math to make this fail

## S12.3 Exit

- [ ] Green
