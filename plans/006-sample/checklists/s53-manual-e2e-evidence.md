# S53 — Manual e2e evidence

**Track:** Runbook · **Analysis:** `../10` §5  
**Depends on:** S42–S45, Hub local available  
**Goal:** Prove green once; leave template for operators.

---

## S53.1 Template

- [x] Create `plans/006-sample/evidence/local-e2e.md` template with:
  - [x] Date, branch, Hub port, sample port
  - [x] Provision redacted (sk_***/whsec_***)
  - [x] Checkout id (ok to store)
  - [x] Delivery id if known
  - [x] Tunnel notes (hop1/hop2)
  - [x] Pass/fail checklist rows

## S53.2 Curl path (handler + optional)

- [x] Provision (or use existing keys) — skipped (no Hub); dummy secrets for handler
- [x] Create checkout with valid sk + BYOK — residual (Hub); local draft order created instead
- [x] Fake signed payment.completed → order paid
- [x] Bad signature → 401
- [x] Replay delivery → single unlock

## S53.3 Browser path (when sandbox reachable)

- [x] Create order in UI → redirect to gateway — **blocked** (no Hub); documented
- [x] Complete sandbox pay **or** document blocked (no tunnel) and pass via fake webhook only
- [x] Success page alone does not pay — code-reviewed
- [x] Cancel path does not pay — code-reviewed

## S53.4 Negative spots

- [x] PAYMENTS_NOT_CONFIGURED observed or documented when BYOK off
- [x] Missing scope/key documented

## S53.5 Exit

- [x] At least one evidence file filled (or explicit “sandbox blocked; fake webhook path green”)
- [x] Gaps listed as residual ops (tunnel), not as open code debt if sample correct
