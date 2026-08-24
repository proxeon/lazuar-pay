# C26 — CHIP empty body 400

**Track:** CHIP · **Depends:** P23, C18  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-GW-005  
**Goal:** Empty CHIP POST is 400, not 500.

---

## C26.1

- [x] `POST /v1/webhooks/chip/{orgId}` empty / whitespace → 400 `"empty body"`
- [x] Rail configured (C11 seed)
- [x] Shared P23 check is enough if it runs before provider switch

## C26.2 Exit

- [x] Test green
