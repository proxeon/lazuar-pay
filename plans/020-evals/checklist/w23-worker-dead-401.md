# W23 — Worker 401/403 dead-letters

**Track:** W · **Depends:** W20  
**Goal:** Wrong secret at the app does not retry forever.

**Why:** 401 means the app rejected HMAC (wrong secret, or they copied Stripe verify). Infinite retry is a stampede. Dead-letter the **row**, keep the endpoint so rotate (W16) can be followed by a test ping (W30).

**Related files**

| Path | Role today |
|------|------------|
| W11 `Status` | `pending` / `succeeded` / `dead` |
| W16 rotate | New secret for new rows |
| One dispatcher dead-letter | Judgment only |

**Current (`6d730d15`):** N/A.

---

## W23.1

- [x] 401 or 403 → `Status = dead` (or `disabled` endpoint — **pick dead row, keep endpoint**)
- [x] Do not mark the org’s endpoint disabled automatically (rotate is human)
- [x] 410 may also dead

## W23.2 Tests

- [x] Handler 401 → dead, second ProcessBatch does not POST again

## W23.3 Exit

- [x] Unblocked for W24
