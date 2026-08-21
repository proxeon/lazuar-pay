# G25 — Hermetic webhook tests

**Track:** Rails · **Depends:** G19, G20, G21, G22  
**Analysis:** [06](../06-money-rails.md) §5.5, [10](../10-ci-observability-decommission.md)  
**IDs:** NP-GW-004, NP-GW-005, NP-GW-006, NP-GW-008  
**Goal:** `task pay:test` proves Plane B. No live Stripe in CI.

---

## G25.1 Factory

- [x] Hermetic `FakeHttp` **or** stub signature — no network to Stripe/CHIP
- [x] Fixture wrap key + fixture webhook secret (G11)
- [x] Health still 200 if the PSP handler would throw

## G25.2 Cases (one test each)

- [x] Bad / missing signature → **4xx** (G19)
- [x] Empty body → **400** (G20)
- [x] Two posts, same `event_id` → second **200** no-op; fulfill once (G21)
- [x] Setup-intent / amount≤0 / skip_capture-without-token → fulfill **not** called (G22)

## G25.3 Hygiene

- [x] Tests do not skip on “Stripe not configured”
- [x] No live Stripe/CHIP keys in CI
- [x] `task pay:test` runs them. IsolationTests still green

## G25.4 Exit

- [x] All G25.2 cases green
- [x] `NP-GW-004` / `005` / `006` / `008` may move if not already
- [x] Unblocked for F10 (if still open) and G26
