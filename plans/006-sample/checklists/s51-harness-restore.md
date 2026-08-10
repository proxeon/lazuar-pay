# S51 — Restore curl harness

**Track:** Runbook · **Analysis:** `../10` D05  
**Depends on:** S42–S45 for accurate commands  
**Goal:** Living curl twin of sample (not deleted path).

---

## S51.1 Location (pick and document)

- [x] Preferred: `plans/006-sample/harness/second-app-proof.md`
- [ ] And/or `scripts/second-app-proof.md` (plural scripts/) — **not used** (plans/ preferred)
- [x] Do **not** revive misleading `script/` singular without note

## S51.2 Content

- [x] Prerequisites (Hub, provision secret, BYOK)
- [x] Provision curl → store sk_ / whsec_
- [x] Create checkout curl with Idempotency-Key + metadata.order_id
- [x] Get checkout curl
- [x] Fake signed webhook curl (python HMAC) against sample
- [x] Notes: real sandbox pay needs public Hub hop1
- [x] Link sample app path + run-sample-app docs
- [x] Redaction guidance for evidence

## S51.3 Link updates

- [x] engineer quickstart points here
- [x] second-app-checklist harness section
- [x] run-sample-app page

## S51.4 Exit

- [x] Harness runnable as text; sample optional for curl-only path of handler tests
