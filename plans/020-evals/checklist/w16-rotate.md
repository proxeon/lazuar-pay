# W16 — Rotate Plane C secret

**Track:** W · **Depends:** W14  
**Goal:** New secret once; old secret cannot verify new deliveries.

**Why:** Compromised sample env. One has rotate. Pay vault has no rotate-only door (re-PUT). Plane C should rotate without changing URL.

**Related files**

| Path | Role today |
|------|------------|
| W14 register | Replace vs rotate |
| `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` | New wrap |
| Sibling One rotate webhook | Judgment: secret once |

**Current (`6d730d15`):** N/A.

---

## W16.1

- [x] `POST /v1/orgs/{orgId}/webhooks/rotate` **or** PUT with `{ "rotate": true }` — **pick POST rotate**
- [x] Writer
- [x] No endpoint → 404
- [x] New wrap; 200 includes new `webhook_secret` once
- [x] URL unchanged

## W16.2 Tests

- [x] After rotate, worker signatures verify only with new secret (can wait for W21)
- [x] GET never shows new full secret

## W16.3 Exit

- [x] Unblocked for W17
