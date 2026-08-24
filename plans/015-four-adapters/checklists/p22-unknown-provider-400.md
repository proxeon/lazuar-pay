# P22 — Unknown {provider} is 400

**Track:** Provider door · **Depends:** P10, P21  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** `fiuu` / `STRIPE` (if you forget to normalize — actually STRIPE should normalize) / `paypal` do not 500.

---

## P22.1

- [ ] PUT unknown provider → 400
- [ ] POST `/v1/webhooks/fiuu/{orgId}` → 400 `"unknown provider"` even with a body
- [ ] Empty body still 400 (P23) — either unknown or empty; unknown can win first
- [ ] Do not 401 this route

## P22.2 Test

- [ ] Hermetic POST webhook path `paypal` → 400

## P22.3 Exit

- [ ] Test green
