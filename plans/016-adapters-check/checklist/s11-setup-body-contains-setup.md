# S11 — Strengthen setup ignore body

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.2; live reason `setup_or_zero`  
**IDs:** H19  
**Goal:** Optional `Does.Contain("setup")`. Not a new method.

---

## S11.1

- [ ] `Setup_mode_is_ignored` already contains `ignored` — add `setup` if the JSON reason includes it
- [ ] If reason is only `setup_or_zero`, `Contain("setup")` still passes

## S11.2 Exit

- [ ] Green
