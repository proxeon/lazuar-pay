# S53 — Manual e2e evidence

**Track:** Runbook · **Analysis:** `../10` §5  
**Depends on:** S42–S45, Hub local available  
**Goal:** Prove green once; leave template for operators.

---

## S53.1 Template

- [ ] Create `plans/006-sample/evidence/local-e2e.md` template with:
  - [ ] Date, branch, Hub port, sample port
  - [ ] Provision redacted (sk_***/whsec_***)
  - [ ] Checkout id (ok to store)
  - [ ] Delivery id if known
  - [ ] Tunnel notes (hop1/hop2)
  - [ ] Pass/fail checklist rows

## S53.2 Curl path (handler + optional)

- [ ] Provision (or use existing keys)
- [ ] Create checkout with valid sk + BYOK
- [ ] Fake signed payment.completed → order paid
- [ ] Bad signature → 401
- [ ] Replay delivery → single unlock

## S53.3 Browser path (when sandbox reachable)

- [ ] Create order in UI → redirect to gateway
- [ ] Complete sandbox pay **or** document blocked (no tunnel) and pass via fake webhook only
- [ ] Success page alone does not pay
- [ ] Cancel path does not pay

## S53.4 Negative spots

- [ ] PAYMENTS_NOT_CONFIGURED observed or documented when BYOK off
- [ ] Missing scope/key documented

## S53.5 Exit

- [ ] At least one evidence file filled (or explicit “sandbox blocked; fake webhook path green”)
- [ ] Gaps listed as residual ops (tunnel), not as open code debt if sample correct
