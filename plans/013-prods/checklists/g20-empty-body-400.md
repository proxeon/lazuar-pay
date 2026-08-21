# G20 — Empty webhook body → 400

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §5.2 / §5.5  
**IDs:** NP-GW-005  
**Goal:** Empty is poison, not 500. `NP-GW-005`.

---

## G20.1 Behavior

- [ ] `POST /v1/webhooks/{provider}/{orgId}` with empty or whitespace body → **400**
- [ ] Do **not** 500 (Hub B04-P18 used to). Do not 200 `{ received: true }`
- [ ] Do not insert a D23 row. Do not call fulfill
- [ ] Health `GET /v1/health` still does not POST here
- [ ] Steal live Hub 400, not 008's 500

## G20.2 Test

- [ ] Hermetic test: empty body → 400 (this commit or G25)
- [ ] No live Stripe/CHIP. `task pay:test`

## G20.3 Exit

- [ ] `NP-GW-005` may move when the test is green
- [ ] Unblocked for G25
