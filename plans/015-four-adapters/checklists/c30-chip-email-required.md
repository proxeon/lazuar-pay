# C30 — CHIP start requires email

**Track:** CHIP · **Depends:** P19, P20, C17  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-BUY-001  
**Goal:** Hub `TryResolveEmail` for CHIP.

---

## C30.1

- [ ] `POST /v1/pay/{token}/start` with active chip and missing/blank email → 400
- [ ] Placeholder `customer@example.com` → 400 (P20)
- [ ] Name: Hub used local-part of email if missing — may require name too or derive; **do not** send empty client

## C30.2 Exit

- [ ] Covered by C17
