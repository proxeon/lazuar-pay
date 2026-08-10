# S51 — Restore curl harness

**Track:** Runbook · **Analysis:** `../10` D05  
**Depends on:** S42–S45 for accurate commands  
**Goal:** Living curl twin of sample (not deleted path).

---

## S51.1 Location (pick and document)

- [ ] Preferred: `plans/006-sample/harness/second-app-proof.md`
- [ ] And/or `scripts/second-app-proof.md` (plural scripts/)
- [ ] Do **not** revive misleading `script/` singular without note

## S51.2 Content

- [ ] Prerequisites (Hub, provision secret, BYOK)
- [ ] Provision curl → store sk_ / whsec_
- [ ] Create checkout curl with Idempotency-Key + metadata.order_id
- [ ] Get checkout curl
- [ ] Fake signed webhook curl (python HMAC) against sample
- [ ] Notes: real sandbox pay needs public Hub hop1
- [ ] Link sample app path + run-sample-app docs
- [ ] Redaction guidance for evidence

## S51.3 Link updates

- [ ] engineer quickstart points here
- [ ] second-app-checklist harness section
- [ ] run-sample-app page

## S51.4 Exit

- [ ] Harness runnable as text; sample optional for curl-only path of handler tests
