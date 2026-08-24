# M14 — PUT keys shows host detail

**Track:** Merchant · **Depends:** A00  
**Analysis:** live `keys ${status}`; host `PayErrors` `{status,title,detail}`  
**IDs:** —  
**Goal:** 400 “environment is required” is visible.

---

## M14.1

- [ ] On PUT !ok, parse JSON `detail` if present
- [ ] `setError(detail ?? \`keys ${status}\`)`

## M14.2 Must not

- [ ] Do not dump stack traces
- [ ] Do not log the PUT body

## M14.3 Exit

- [ ] Shared helper OK if M15/M16 reuse it
