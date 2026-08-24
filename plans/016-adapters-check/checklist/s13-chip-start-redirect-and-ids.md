# S13 — Strengthen CHIP start asserts

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.4; C17  
**IDs:** C14, C17  
**Goal:** Edit `Chip_start_and_paid_webhook`.

---

## S13.1 After start 200

- [ ] JSON `redirect_url` equals stub URL
- [ ] `db.Checkouts.Single().Provider == "chip"`
- [ ] `ProviderSessionId == "purch_1"` (or stub id)
- [ ] `LastBody` contains `"org_id"`

## S13.2 Must not

- [ ] Do not split into a new method unless the file is unreadable — prefer edit

## S13.3 Exit

- [ ] Green
