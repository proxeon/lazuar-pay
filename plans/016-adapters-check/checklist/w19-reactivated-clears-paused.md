# W19 — `tenant.reactivated` clears pause

**Track:** One HMAC · **Depends:** W18  
**Analysis:** live handler already clears on `tenant.reactivated`  
**IDs:** —  
**Goal:** Keep unsuspend. Do not invent `tenant.activated` unless One sends it.

---

## W19.1

- [ ] Keep `tenant.reactivated` → `ChargesPaused = false` when settings row exists
- [ ] If One also sends `tenant.unsuspended` / `tenant.activated`, accept those **only** after reading live One payload names (comment the source)
- [ ] Missing settings row: do not create one just to set false

## W19.2 Must not

- [ ] Do not auto-fulfill paused in-flight payments on unsuspend (PSP retry does, because W22 did not consume)

## W19.3 Exit

- [ ] Hermetic: suspend then reactivate → `ChargesPaused == false`
